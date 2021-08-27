// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection.ServiceLookup;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace System {
	/// <summary>Defines a mechanism for retrieving a single-tenant service object; that is, an object that provides custom support to other objects.</summary>
	public interface IMultiTenantServiceProvider<in TTenantId> {
		object? GetService(TTenantId tenantId, Type serviceType);
	}
}
namespace Microsoft.Extensions.DependencyInjection {
	public class MultiTenantServiceProviderOptions<TTenantId> : ServiceProviderOptions {
		public MultiTenantServiceProviderOptions() { }
		public MultiTenantServiceProviderOptions(ServiceProviderOptions options) {
			switch (options) {
				case null: break;
				case MultiTenantServiceProviderOptions<TTenantId> mto:
					this.TenantIdComparer = mto.TenantIdComparer;
					goto default;
				default:
					this.ValidateScopes = options.ValidateScopes;
					this.ValidateOnBuild = options.ValidateOnBuild;
					break;
			}
		}
		public MultiTenantServiceProviderOptions(MultiTenantServiceProviderOptions<TTenantId> options) {
			if (options is null)
				return;

			this.TenantIdComparer = options.TenantIdComparer;
			this.ValidateScopes = options.ValidateScopes;
			this.ValidateOnBuild = options.ValidateOnBuild;
		}

		public IEqualityComparer<TTenantId>? TenantIdComparer { get; set; }

	}

	public abstract class MultiTenantServiceProvider : IServiceProvider, IDisposable, IAsyncDisposable {
		private protected CallSiteValidator _callSiteValidator { get; init; }

		private protected Func<ServiceIdentifier, Func<MultiTenantProviderEngineScope, object>> _createServiceAccessor { get; init; }

		// Internal for testing
		internal MultiTenantServiceProviderEngine _engine;

		private bool _disposed;

		private protected ConcurrentDictionary<ServiceIdentifier, Func<MultiTenantProviderEngineScope, object>> _realizedServices { get; init; }

		internal CallSiteFactory CallSiteFactory { get; private protected init; }

		internal MultiTenantProviderEngineScope Root { get; private protected init; }

		private protected MultiTenantServiceProvider() { }

		/// <summary>Gets the service object of the specified type.</summary>
		/// <param name="serviceType">The type of the service to get.</param>
		/// <returns>The service that was produced.</returns>
		public object? GetService(Type serviceType) => this.GetService(new ServiceIdentifier(serviceType));

		internal object? GetService(ServiceIdentifier serviceIdentifier) => this.GetService(serviceIdentifier, this.Root);

		/// <inheritdoc />
		public void Dispose() {
			this._disposed = true;
			this.Root.Dispose();
		}

		/// <inheritdoc/>
		public ValueTask DisposeAsync() {
			this._disposed = true;
			return this.Root.DisposeAsync();
		}

		private void OnCreate(ServiceCallSite callSite) => this._callSiteValidator?.ValidateCallSite(callSite);

		private void OnResolve(ServiceIdentifier serviceIdentifier, IServiceScope scope) => this._callSiteValidator?.ValidateResolution(serviceIdentifier, scope, this.Root);

		internal object GetService(ServiceIdentifier serviceIdentifier, MultiTenantProviderEngineScope serviceProviderEngineScope) {
			if (this._disposed)
				ThrowHelper.ThrowObjectDisposedException();

			var realizedService = this._realizedServices.GetOrAdd(serviceIdentifier, this._createServiceAccessor);
			this.OnResolve(serviceIdentifier, serviceProviderEngineScope);
			DependencyInjectionEventSource.Log.ServiceResolved(serviceIdentifier);
			var result = realizedService.Invoke(serviceProviderEngineScope);
			System.Diagnostics.Debug.Assert(result is null || this.CallSiteFactory.IsService(serviceIdentifier));
			return result;
		}

		private protected void ValidateService(ServiceDescriptor descriptor) {
			if (descriptor.ServiceType.IsGenericType && !descriptor.ServiceType.IsConstructedGenericType) {
				return;
			}

			try {
				var callSite = this.CallSiteFactory.GetCallSite(descriptor, tenantId: descriptor.IsShared() ? null : TestingTenantIdentifier.Instance, new CallSiteChain());
				if (callSite != null) {
					this.OnCreate(callSite);
				}
			}
			catch (Exception e) {
				throw new InvalidOperationException($"Error while validating the service descriptor '{descriptor}': {e.Message}", e);
			}
		}

		private protected Func<MultiTenantProviderEngineScope, object> CreateServiceAccessor(ServiceIdentifier serviceIdentifier) {
			var callSite = this.CallSiteFactory.GetCallSite(serviceIdentifier, new CallSiteChain());
			if (callSite != null) {
				DependencyInjectionEventSource.Log.CallSiteBuilt(serviceIdentifier, callSite);
				this.OnCreate(callSite);

				// Optimize singleton case
				if (callSite.Cache.Location == CallSiteResultCacheLocation.Root) {
					var value = CallSiteRuntimeResolver.Instance.Resolve(callSite, this.Root);
					return scope => value;
				}

				return this._engine.RealizeService(callSite);
			}

			return _ => null;
		}

		internal void ReplaceServiceAccessor(ServiceCallSite callSite, Func<MultiTenantProviderEngineScope, object> accessor) => this._realizedServices[callSite.ImplementationType] = accessor;

		internal IServiceScope CreateScope() {
			if (this._disposed) {
				ThrowHelper.ThrowObjectDisposedException();
			}

			return new MultiTenantProviderEngineScope(this, isRootScope: false);
		}

		private protected MultiTenantServiceProviderEngine GetEngine() {
			MultiTenantServiceProviderEngine engine;

#if !NETSTANDARD2_1
			engine = new DynamicServiceProviderEngine(this);
#else
			if (RuntimeFeature.IsDynamicCodeCompiled) {
				engine = new DynamicServiceProviderEngine(this);
			}
			else {
				// Don't try to compile Expressions/IL if they are going to get interpreted
				engine = RuntimeServiceProviderEngine.Instance;
			}
#endif
			return engine;
		}
	}

	/// <summary>The default IServiceProvider.</summary>
	public sealed class MultiTenantServiceProvider<TTenantKey> : MultiTenantServiceProvider, IMultiTenantServiceProvider<TTenantKey>, IServiceProvider, IDisposable, IAsyncDisposable {
		private readonly TenantKeyFactory<TTenantKey> tenantKeyFactory;

		internal MultiTenantServiceProvider(IEnumerable<ServiceDescriptor> serviceDescriptors, MultiTenantServiceProviderOptions<TTenantKey> options) {
			this.tenantKeyFactory = new(options.TenantIdComparer);

			// note that Root needs to be set before calling GetEngine(), because the engine may need to access Root
			this.Root = new MultiTenantProviderEngineScope(this, isRootScope: true);
			this._engine = this.GetEngine();
			this._createServiceAccessor = this.CreateServiceAccessor;
			this._realizedServices = new ConcurrentDictionary<ServiceIdentifier, Func<MultiTenantProviderEngineScope, object>>();

			this.CallSiteFactory = new CallSiteFactory(serviceDescriptors);
			// The list of built in services that aren't part of the list of service descriptors
			// keep this in sync with CallSiteFactory.IsService
			this.CallSiteFactory.Add(new (typeof(IServiceProvider)), new ServiceProviderCallSite<TTenantKey>());
			this.CallSiteFactory.Add(new (typeof(IServiceScopeFactory)), new ConstantCallSite(new(typeof(IServiceScopeFactory)), this.Root));
			this.CallSiteFactory.Add(typeof(ITenantKeyAcessor<TTenantKey>), serviceIdentifier => ((InternalTenantIdentifier<TTenantKey>?)serviceIdentifier.TenantId)?.CreateTenantKeyAcessorCallSite());
			//CallSiteFactory.Add(typeof(IServiceProviderIsService), new ConstantCallSite(typeof(IServiceProviderIsService), CallSiteFactory));

			if (options.ValidateScopes) {
				this._callSiteValidator = new CallSiteValidator();
			}

			if (options.ValidateOnBuild) {
				List<Exception>? exceptions = null;
				foreach (var serviceDescriptor in serviceDescriptors) {
					try {
						this.ValidateService(serviceDescriptor);
					}
					catch (Exception e) {
						exceptions ??= new List<Exception>();
						exceptions.Add(e);
					}
				}

				if (exceptions != null) {
					throw new AggregateException("Some services are not able to be constructed", exceptions.ToArray());
				}
			}
		}

		public object? GetService(TTenantKey tenantId, Type serviceType) => this.GetService(new ServiceIdentifier(serviceType, this.tenantKeyFactory[tenantId]));
	}
}
