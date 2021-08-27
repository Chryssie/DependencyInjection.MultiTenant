// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class CallSiteFactory : IServiceProviderIsService {
		private readonly ServiceDescriptor[] _descriptors;

		private readonly ConcurrentDictionary<ServiceCacheKey, ServiceCallSite> _callSiteCache = new();
		private readonly ConcurrentDictionary<Type, Func<ServiceIdentifier, ServiceCallSite>> callSiteFactories = new();
		private readonly MultiTenantServiceDescriptorLookup descriptorLookup = new();
		private readonly ConcurrentDictionary<ServiceIdentifier, object> _callSiteLocks = new();

		private readonly StackGuard _stackGuard;

		public CallSiteFactory(IEnumerable<ServiceDescriptor> descriptors) {
			this._stackGuard = new StackGuard();
			this._descriptors = descriptors.ToArray();
			this.descriptorLookup = this.Populate();

		}

		private MultiTenantServiceDescriptorLookup Populate() {
			var descriptorLookupBuilder = new MultiTenantServiceDescriptorLookup.Builder();

			foreach (var descriptor in this._descriptors) {
				var serviceType = descriptor.ServiceType;
				if (serviceType.IsGenericTypeDefinition) {
					var implementationType = descriptor.ImplementationType;

					if (implementationType == null || !implementationType.IsGenericTypeDefinition)
						throw new ArgumentException(SR.OpenGenericServiceRequiresOpenGenericImplementation(serviceType), "descriptors");

					if (implementationType.IsAbstract || implementationType.IsInterface)
						throw new ArgumentException(SR.TypeCannotBeActivated(implementationType, serviceType));

					if (serviceType.GetGenericArguments().Length != implementationType.GetGenericArguments().Length)
						throw new ArgumentException(SR.ArityOfOpenGenericServiceNotEqualArityOfOpenGenericImplementation(serviceType, implementationType), "descriptors");
				}
				else if (descriptor.ImplementationInstance == null && descriptor.ImplementationFactory == null) {
					Debug.Assert(descriptor.ImplementationType != null);
					var implementationType = descriptor.ImplementationType;

					if (implementationType.IsGenericTypeDefinition || implementationType.IsAbstract || implementationType.IsInterface)
						throw new ArgumentException(SR.TypeCannotBeActivated(implementationType, serviceType));
				}

				descriptorLookupBuilder.Add(descriptor);
			}

			return descriptorLookupBuilder.Build();
		}

		// For unit testing
		internal Slot? GetSharedSlot(ServiceDescriptor serviceDescriptor)
			=> !this.descriptorLookup.Shared.TryGetDescriptors(serviceDescriptor.ServiceType, out var item) ? null : item.GetSlot(serviceDescriptor);
		internal Slot? GetTenantSlot(ServiceDescriptor serviceDescriptor)
			=> !this.descriptorLookup.Tenanted.TryGetDescriptors(serviceDescriptor.ServiceType, out var item) ? null : item.GetSlot(serviceDescriptor);

		internal ServiceCallSite GetCallSite(ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain)
			=> this._callSiteCache.TryGetValue(new(serviceIdentifier), out var site) ? site : this.CreateCallSite(serviceIdentifier, callSiteChain);

		internal ServiceCallSite? GetCallSite(ServiceDescriptor serviceDescriptor, TenantIdentifier? tenantId, CallSiteChain callSiteChain) {
			if (tenantId is not null) {
				if (this.descriptorLookup.Tenanted.TryGetDescriptors(serviceDescriptor.ServiceType, out var descriptors)) {
					ref readonly var descriptor = ref descriptors.GetInfo(serviceDescriptor, out var slot);
					return this.TryCreateExact(in descriptor, new ServiceIdentifier(serviceDescriptor.ServiceType, tenantId), callSiteChain, slot);
				}
			}
			else {
				if (this.descriptorLookup.Shared.TryGetDescriptors(serviceDescriptor.ServiceType, out var descriptors)) {
					ref readonly var descriptor = ref descriptors.GetInfo(serviceDescriptor, out var slot);
					return this.TryCreateExact(in descriptor, new ServiceIdentifier(serviceDescriptor.ServiceType), callSiteChain, slot);
				}
			}

			Debug.Fail("descriptorLookup didn't contain requested serviceDescriptor");
			return null;
		}

		private ServiceCallSite CreateCallSite(ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain) {
			if (!this._stackGuard.TryEnterOnCurrentStack())
				return this._stackGuard.RunOnEmptyStack((serviceIdentifier, chain) => this.CreateCallSite(serviceIdentifier, chain), serviceIdentifier, callSiteChain);

			// We need to lock the resolution process for a single service type at a time:
			// Consider the following:
			// C -> D -> A
			// E -> D -> A
			// Resolving C and E in parallel means that they will be modifying the callsite cache concurrently
			// to add the entry for C and E, but the resolution of D and A is synchronized
			// to make sure C and E both reference the same instance of the callsite.

			// This is to make sure we can safely store singleton values on the callsites themselves

			var callsiteLock = this._callSiteLocks.GetOrAdd(serviceIdentifier, static _ => new object());

			lock (callsiteLock) {
				callSiteChain.CheckCircularDependency(serviceIdentifier);

				return this.TryCreateOverriden(serviceIdentifier)
					?? this.TryCreateExact(serviceIdentifier, callSiteChain)
					?? this.TryCreateOpenGeneric(serviceIdentifier, callSiteChain)
					?? this.TryCreateEnumerable(serviceIdentifier, callSiteChain);
			}
		}

		private ServiceCallSite? TryCreateExact(ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain) {
			if (!this.descriptorLookup.TryGetDescriptors(serviceIdentifier, out var descriptor))
				return null;

			return this.TryCreateExact(descriptor.Last, serviceIdentifier, callSiteChain, slot: default);
		}

		private ServiceCallSite? TryCreateOverriden(ServiceIdentifier serviceIdentifier) {
			if (!this.callSiteFactories.TryGetValue(serviceIdentifier.Type, out var factory))
				return null;

			var serviceCallSite = factory.Invoke(serviceIdentifier);
			if (serviceCallSite is null)
				return null;

			var callSiteKey = new ServiceCacheKey(serviceIdentifier);

			return this._callSiteCache[callSiteKey] = serviceCallSite;
		}

		private ServiceCallSite? TryCreateOpenGeneric(ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain) {
			if (!(serviceIdentifier.Type is { IsConstructedGenericType: true, } type) || !this.descriptorLookup.TryGetDescriptors(new ServiceIdentifier(type.GetGenericTypeDefinition(), serviceIdentifier.TenantId), out var descriptor))
				return null;

			return this.TryCreateOpenGeneric(in descriptor.Last, serviceIdentifier, callSiteChain, slot: default, throwOnConstraintViolation: true);
		}

		private ServiceCallSite? TryCreateEnumerable(ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain) {
			var callSiteKey = new ServiceCacheKey(serviceIdentifier);
			if (this._callSiteCache.TryGetValue(callSiteKey, out var serviceCallSite))
				return serviceCallSite;

			try {
				callSiteChain.Add(serviceIdentifier);

				if (serviceIdentifier.Type is { IsConstructedGenericType: true, } type && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
					var itemType = type.GenericTypeArguments[0];
					var cacheLocation = CallSiteResultCacheLocation.Root;

					var callSites = ImmutableArray.CreateBuilder<ServiceCallSite>();

					var itemIdentifier = serviceIdentifier with { Type = itemType, };

					// If item type is not generic we can safely use descriptor cache
					if (!itemType.IsConstructedGenericType && this.descriptorLookup.TryGetDescriptors(itemIdentifier, out var descriptors)) {
						for (var i = 0; i < descriptors.Count; i++) {
							var descriptor = descriptors[i];

							// Last service should get slot 0
							var slot = descriptors.GetSlot(i);
							// There may not be any open generics here
							var callSite = this.TryCreateOverriden(in descriptor, in serviceIdentifier, callSiteChain, slot)
								?? this.TryCreateExact(in descriptor, in itemIdentifier, callSiteChain, slot);
							Debug.Assert(callSite != null);

							cacheLocation = this.GetCommonCacheLocation(cacheLocation, callSite!.Cache.Location);
							callSites.Add(callSite);
						}
					}
					else {
						var slot = Slot.Default;
						// We are going in reverse so the last service in descriptor list gets slot 0
						for (var i = this._descriptors.Length - 1; i >= 0; i--) {
							ref readonly var descriptor = ref this.descriptorLookup.GetInfo(itemIdentifier, this._descriptors[i]);

							var callSite
								= this.TryCreateOverriden(in descriptor, in serviceIdentifier, callSiteChain, slot)
								?? this.TryCreateExact(in descriptor, in serviceIdentifier, callSiteChain, slot)
								?? this.TryCreateOpenGeneric(in descriptor, in serviceIdentifier, callSiteChain, slot, false);

							if (callSite != null) {
								slot++;

								cacheLocation = this.GetCommonCacheLocation(cacheLocation, callSite.Cache.Location);
								callSites.Add(callSite);
							}
						}

						callSites.Reverse();
					}


					var resultCache = ResultCache.None;
					if (cacheLocation == CallSiteResultCacheLocation.Scope || cacheLocation == CallSiteResultCacheLocation.Root) {
						resultCache = new ResultCache(cacheLocation, callSiteKey);
					}

					return this._callSiteCache[callSiteKey] = new IEnumerableCallSite(resultCache, new ServiceIdentifier(itemType, serviceIdentifier.TenantId), callSites.ToImmutable());
				}

				return null;
			}
			finally {
				callSiteChain.Remove(serviceIdentifier);
			}
		}

		private CallSiteResultCacheLocation GetCommonCacheLocation(CallSiteResultCacheLocation locationA, CallSiteResultCacheLocation locationB) => (CallSiteResultCacheLocation)Math.Max((int)locationA, (int)locationB);

		private ServiceCallSite? TryCreateOverriden(in MultiTenantServiceDescriptorItem descriptor, in ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain, Slot slot) {
			if (serviceIdentifier.Type != descriptor.Descriptor.ServiceType)
				return null;

			if (slot != default)
				return null;

			if (!this.callSiteFactories.TryGetValue(serviceIdentifier.Type, out var factory))
				return null;

			var serviceCallSite = factory.Invoke(serviceIdentifier);
			if (serviceCallSite is null)
				return null;

			var callSiteKey = new ServiceCacheKey(serviceIdentifier, slot);

			return this._callSiteCache[callSiteKey] = serviceCallSite;
		}
		private ServiceCallSite? TryCreateExact(in MultiTenantServiceDescriptorItem descriptor, in ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain, Slot slot) {
			if (serviceIdentifier.Type != descriptor.Descriptor.ServiceType) // || serviceIdentifier.TenantId.HasTenantId != !descriptor.IsShared)
				return null;

			var callSiteKey = new ServiceCacheKey(serviceIdentifier, slot);

			if (this._callSiteCache.TryGetValue(callSiteKey, out var serviceCallSite))
				return serviceCallSite;

			if (!descriptor.IsTransposedShared)
				return this.CreateExact(in callSiteKey, descriptor.Descriptor, callSiteChain);

			if (!this.descriptorLookup.Shared.TryGetDescriptors(descriptor.Descriptor.ServiceType, out var sharedDescriptors))
				throw new InvalidOperationException(SR.InvalidServiceDescriptor());

			ref readonly var sharedDescriptor = ref sharedDescriptors[descriptor.SharedIndex];

			var sharedCacheKey = new ServiceCacheKey(serviceIdentifier.Type, sharedDescriptors.GetSlot(descriptor.SharedIndex));

			serviceCallSite = this.CreateExact(in sharedCacheKey, sharedDescriptor.Descriptor, callSiteChain);

			var transposedCallSite = new TransposedSharedCallSite(serviceIdentifier, serviceCallSite);

			return this._callSiteCache[callSiteKey] = transposedCallSite;
		}

		private ServiceCallSite CreateExact(in ServiceCacheKey callSiteKey, ServiceDescriptor descriptor, CallSiteChain callSiteChain) {
			ServiceCallSite callSite;

			var lifetime = new ResultCache(descriptor.Lifetime, callSiteKey.ServiceIdentifier, callSiteKey.Slot);

			if (descriptor.ImplementationInstance != null) {
				callSite = new ConstantCallSite(callSiteKey.ServiceIdentifier, descriptor.ImplementationInstance);
			}
			else if (descriptor.ImplementationFactory != null) {
				callSite = new FactoryCallSite(lifetime, callSiteKey.ServiceIdentifier, descriptor.ImplementationFactory);
			}
			else if (descriptor.ImplementationType != null) {
				callSite = this.CreateConstructorCallSite(lifetime, callSiteKey.ServiceIdentifier, descriptor.ImplementationType, callSiteChain);
			}
			else {
				throw new InvalidOperationException(SR.InvalidServiceDescriptor());
			}

			return this._callSiteCache[callSiteKey] = callSite;
		}

		private ServiceCallSite? TryCreateOpenGeneric(in MultiTenantServiceDescriptorItem descriptor, in ServiceIdentifier serviceIdentifier, CallSiteChain callSiteChain, Slot slot, bool throwOnConstraintViolation) {
			if (!serviceIdentifier.Type.IsConstructedGenericType || serviceIdentifier.Type.GetGenericTypeDefinition() != descriptor.Descriptor.ServiceType)
				return null;

			var callSiteKey = new ServiceCacheKey(serviceIdentifier, slot);
			if (this._callSiteCache.TryGetValue(callSiteKey, out var serviceCallSite))
				return serviceCallSite;

			if (!descriptor.IsTransposedShared)
				return this.CreateOpenGeneric(in callSiteKey, in descriptor, callSiteChain, throwOnConstraintViolation);


			if (!this.descriptorLookup.Shared.TryGetDescriptors(descriptor.Descriptor.ServiceType, out var sharedDescriptors))
				throw new InvalidOperationException(SR.InvalidServiceDescriptor());

			ref readonly var sharedDescriptor = ref sharedDescriptors[descriptor.SharedIndex];

			var sharedCacheKey = new ServiceCacheKey(serviceIdentifier.Type, sharedDescriptors.GetSlot(descriptor.SharedIndex));

			serviceCallSite = this.CreateExact(in sharedCacheKey, sharedDescriptor.Descriptor, callSiteChain);

			var transposedCallSite = new TransposedSharedCallSite(serviceIdentifier, serviceCallSite);

			return this._callSiteCache[callSiteKey] = transposedCallSite;
		}

		private ServiceCallSite? TryCreateOpenGeneric(in MultiTenantServiceDescriptorItem descriptor, in ServiceCacheKey callSiteKey, CallSiteChain callSiteChain, bool throwOnConstraintViolation) {
			if (this._callSiteCache.TryGetValue(callSiteKey, out var serviceCallSite))
				return serviceCallSite;

			if (!descriptor.IsTransposedShared)
				return this.CreateOpenGeneric(in callSiteKey, in descriptor, callSiteChain, throwOnConstraintViolation);


			if (!this.descriptorLookup.Shared.TryGetDescriptors(descriptor.Descriptor.ServiceType, out var sharedDescriptors))
				throw new InvalidOperationException(SR.InvalidServiceDescriptor());

			ref readonly var sharedDescriptor = ref sharedDescriptors[descriptor.SharedIndex];

			var sharedCacheKey = new ServiceCacheKey(callSiteKey.ServiceIdentifier.Type, sharedDescriptors.GetSlot(descriptor.SharedIndex));

			serviceCallSite = this.CreateExact(in sharedCacheKey, sharedDescriptor.Descriptor, callSiteChain);

			var transposedCallSite = new TransposedSharedCallSite(callSiteKey.ServiceIdentifier, serviceCallSite);

			return this._callSiteCache[callSiteKey] = transposedCallSite;
		}

		private ServiceCallSite? CreateOpenGeneric(in ServiceCacheKey callSiteKey, in MultiTenantServiceDescriptorItem descriptor, CallSiteChain callSiteChain, bool throwOnConstraintViolation) {
			Debug.Assert(descriptor.Descriptor.ImplementationType is not null, "descriptor.Descriptor.ImplementationType is not null");

			var lifetime = new ResultCache(descriptor.Descriptor.Lifetime, callSiteKey.ServiceIdentifier, callSiteKey.Slot);

			Type closedType;
			try {
				closedType = descriptor.Descriptor.ImplementationType!.MakeGenericType(callSiteKey.ServiceIdentifier.Type.GenericTypeArguments);
			}
			catch (ArgumentException) {
				if (throwOnConstraintViolation)
					throw;

				return null;
			}

			return this._callSiteCache[callSiteKey] = this.CreateConstructorCallSite(lifetime, callSiteKey.ServiceIdentifier, closedType, callSiteChain);
		}

		private ServiceCallSite CreateConstructorCallSite(
			ResultCache lifetime,
			in ServiceIdentifier serviceIdentifier,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
			Type implementationType,
			CallSiteChain callSiteChain) {
			try {
				callSiteChain.Add(serviceIdentifier, implementationType);
				var constructors = implementationType.GetConstructors();

				ServiceCallSite[] parameterCallSites = null;

				if (constructors.Length == 0) {
					throw new InvalidOperationException(SR.NoConstructorMatch(implementationType));
				}
				else if (constructors.Length == 1) {
					var constructor = constructors[0];
					var parameters = constructor.GetParameters();
					if (parameters.Length == 0) {
						return new ConstructorCallSite(lifetime, serviceIdentifier, constructor);
					}

					parameterCallSites = this.CreateArgumentCallSites(
						new(implementationType, serviceIdentifier.TenantId),
						callSiteChain,
						parameters,
						throwIfCallSiteNotFound: true);

					return new ConstructorCallSite(lifetime, serviceIdentifier, constructor, parameterCallSites);
				}

				Array.Sort(constructors,
					(a, b) => b.GetParameters().Length.CompareTo(a.GetParameters().Length));

				ConstructorInfo bestConstructor = null;
				HashSet<Type> bestConstructorParameterTypes = null;
				for (var i = 0; i < constructors.Length; i++) {
					var parameters = constructors[i].GetParameters();

					var currentParameterCallSites = this.CreateArgumentCallSites(
						new(implementationType, serviceIdentifier.TenantId),
						callSiteChain,
						parameters,
						throwIfCallSiteNotFound: false);

					if (currentParameterCallSites != null) {
						if (bestConstructor == null) {
							bestConstructor = constructors[i];
							parameterCallSites = currentParameterCallSites;
						}
						else {
							// Since we're visiting constructors in decreasing order of number of parameters,
							// we'll only see ambiguities or supersets once we've seen a 'bestConstructor'.

							if (bestConstructorParameterTypes == null) {
								bestConstructorParameterTypes = new HashSet<Type>();
								foreach (var p in bestConstructor.GetParameters()) {
									bestConstructorParameterTypes.Add(p.ParameterType);
								}
							}

							foreach (var p in parameters) {
								if (!bestConstructorParameterTypes.Contains(p.ParameterType)) {
									// Ambiguous match exception
									throw new InvalidOperationException(string.Join(
										Environment.NewLine,
										SR.AmbiguousConstructorException(implementationType),
										bestConstructor,
										constructors[i]));
								}
							}
						}
					}
				}

				if (bestConstructor == null) {
					throw new InvalidOperationException(
						SR.UnableToActivateTypeException(implementationType));
				}
				else {
					Debug.Assert(parameterCallSites != null);
					return new ConstructorCallSite(lifetime, in serviceIdentifier, bestConstructor, parameterCallSites);
				}
			}
			finally {
				callSiteChain.Remove(serviceIdentifier);
			}
		}

		private ServiceCallSite[] CreateArgumentCallSites(
			in ServiceIdentifier implementationType,
			CallSiteChain callSiteChain,
			ParameterInfo[] parameters,
			bool throwIfCallSiteNotFound) {
			var parameterCallSites = new ServiceCallSite[parameters.Length];
			for (var index = 0; index < parameters.Length; index++) {
				var parameterType = parameters[index].ParameterType;
				var callSite = this.GetCallSite(new(parameterType, implementationType.TenantId), callSiteChain);

				if (callSite is null && ParameterDefaultValue.TryGetDefaultValue(parameters[index], out var defaultValue)) {
					callSite = new ConstantCallSite(new(parameterType, implementationType.TenantId), defaultValue);
				}

				if (callSite is null) {
					if (throwIfCallSiteNotFound) {
						throw new InvalidOperationException(implementationType.TenantId is null && (this.descriptorLookup.Tenanted.ContainsService(parameterType) || this.callSiteFactories.ContainsKey(parameterType)) ? SR.CannotResolveTenantService(parameterType, implementationType) : SR.CannotResolveService(parameterType, implementationType));
					}

					return null;
				}

				parameterCallSites[index] = callSite;
			}

			return parameterCallSites;
		}


		public void Add(in ServiceIdentifier serviceIdentifier, ServiceCallSite serviceCallSite) => this._callSiteCache[new(serviceIdentifier)] = serviceCallSite;
		public void Add(Type serviceType, Func<ServiceIdentifier, ServiceCallSite?> serviceCallSiteFactory) => this.callSiteFactories[serviceType] = serviceCallSiteFactory;

		bool IServiceProviderIsService.IsService(Type serviceType) => this.IsService(new(serviceType));
		public bool IsService(in ServiceIdentifier serviceIdentifier) {
			if (serviceIdentifier.Type is null)
				throw new ArgumentNullException(nameof(serviceIdentifier.Type));

			// Querying for an open generic should return false (they aren't resolvable)
			if (serviceIdentifier.Type.IsGenericTypeDefinition) {
				return false;
			}

			if (this.descriptorLookup.ContainsService(serviceIdentifier)) {
				return true;
			}

			if (serviceIdentifier.Type.IsConstructedGenericType && serviceIdentifier.Type.GetGenericTypeDefinition() is Type genericDefinition) {
				// We special case IEnumerable since it isn't explicitly registered in the container
				// yet we can manifest instances of it when requested.
				return genericDefinition == typeof(IEnumerable<>) || this.descriptorLookup.ContainsService(serviceIdentifier);
			}

			// These are the built in service types that aren't part of the list of service descriptors
			// If you update these make sure to also update the code in ServiceProvider.ctor
			return serviceIdentifier.Type == typeof(IServiceProvider) ||
				   serviceIdentifier.Type == typeof(IServiceScopeFactory) ||
				   serviceIdentifier.Type == typeof(IServiceProviderIsService);

		}


		private struct ServiceDescriptorCacheItem {
			private ServiceDescriptor _item;

			private List<ServiceDescriptor> _items;

			public ServiceDescriptor Last {
				get {
					if (this._items != null && this._items.Count > 0) {
						return this._items[this._items.Count - 1];
					}

					Debug.Assert(this._item != null);
					return this._item;
				}
			}

			public int Count {
				get {
					if (this._item == null) {
						Debug.Assert(this._items == null);
						return 0;
					}

					return 1 + (this._items?.Count ?? 0);
				}
			}

			public ServiceDescriptor this[int index] {
				get {
					if (index >= this.Count) {
						throw new ArgumentOutOfRangeException(nameof(index));
					}

					if (index == 0) {
						return this._item;
					}

					return this._items[index - 1];
				}
			}

			public int GetSlot(ServiceDescriptor descriptor) {
				if (descriptor == this._item) {
					return this.Count - 1;
				}

				if (this._items != null) {
					var index = this._items.IndexOf(descriptor);
					if (index != -1) {
						return this._items.Count - (index + 1);
					}
				}

				throw new InvalidOperationException(SR.ServiceDescriptorNotExist());
			}

			public ServiceDescriptorCacheItem Add(ServiceDescriptor descriptor) {
				var newCacheItem = default(ServiceDescriptorCacheItem);
				if (this._item == null) {
					Debug.Assert(this._items == null);
					newCacheItem._item = descriptor;
				}
				else {
					newCacheItem._item = this._item;
					newCacheItem._items = this._items ?? new List<ServiceDescriptor>();
					newCacheItem._items.Add(descriptor);
				}
				return newCacheItem;
			}
		}
	}
}
