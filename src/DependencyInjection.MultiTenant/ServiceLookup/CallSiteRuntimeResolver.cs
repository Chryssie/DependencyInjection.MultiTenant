// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class CallSiteRuntimeResolver : CallSiteVisitor<RuntimeResolverContext, object> {
		public static CallSiteRuntimeResolver Instance { get; } = new();

		private CallSiteRuntimeResolver() {
		}

		public object Resolve(ServiceCallSite callSite, MultiTenantProviderEngineScope scope) {
			// Fast path to avoid virtual calls if we already have the cached value in the root scope
			if (scope.IsRootScope && callSite.Value is object cached) {
				return cached;
			}

			return this.VisitCallSite(callSite, new RuntimeResolverContext {
				Scope = scope
			});
		}

		protected internal override object VisitDisposeCache(ServiceCallSite transientCallSite, RuntimeResolverContext context) => context.Scope.CaptureDisposable(this.VisitCallSiteMain(transientCallSite, context));

		protected override internal object VisitConstructor(ConstructorCallSite constructorCallSite, RuntimeResolverContext context) {
			object[] parameterValues;
			if (constructorCallSite.ParameterCallSites.Length == 0) {
				parameterValues = Array.Empty<object>();
			}
			else {
				parameterValues = new object[constructorCallSite.ParameterCallSites.Length];
				for (var index = 0; index < parameterValues.Length; index++) {
					parameterValues[index] = this.VisitCallSite(constructorCallSite.ParameterCallSites[index], context);
				}
			}

#if NETSTANDARD2_1
			return constructorCallSite.ConstructorInfo.Invoke(BindingFlags.DoNotWrapExceptions, binder: null, parameters: parameterValues, culture: null);
#else
            try
            {
                return constructorCallSite.ConstructorInfo.Invoke(parameterValues);
            }
            catch (Exception ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                // The above line will always throw, but the compiler requires we throw explicitly.
                throw;
            }
#endif
		}

		protected internal override object VisitRootCache(ServiceCallSite callSite, RuntimeResolverContext context) {
			if (callSite.Value is object value) {
				// Value already calculated, return it directly
				return value;
			}

			var lockType = RuntimeResolverLock.Root;
			var serviceProviderEngine = context.Scope.RootProvider.Root;

			lock (callSite) {
				// Lock the callsite and check if another thread already cached the value
				if (callSite.Value is object resolved)
					return resolved;

				resolved = this.VisitCallSiteMain(callSite, new RuntimeResolverContext {
					Scope = serviceProviderEngine,
					AcquiredLocks = context.AcquiredLocks | lockType
				});

				if (callSite.RuntimeCaptureDisposable)
					serviceProviderEngine.CaptureDisposable(resolved);

				callSite.Value = resolved;
				return resolved;
			}
		}

		protected internal override object VisitScopeCache(ServiceCallSite callSite, RuntimeResolverContext context) =>
			// Check if we are in the situation where scoped service was promoted to singleton
			// and we need to lock the root
			context.Scope.IsRootScope ?
				this.VisitRootCache(callSite, context) :
				this.VisitCache(callSite, context, context.Scope, RuntimeResolverLock.Scope);

		private object VisitCache(ServiceCallSite callSite, RuntimeResolverContext context, MultiTenantProviderEngineScope serviceProviderEngine, RuntimeResolverLock lockType) {
			var lockTaken = false;
			var sync = serviceProviderEngine.Sync;
			var resolvedServices = serviceProviderEngine.ResolvedServices;
			// Taking locks only once allows us to fork resolution process
			// on another thread without causing the deadlock because we
			// always know that we are going to wait the other thread to finish before
			// releasing the lock
			if ((context.AcquiredLocks & lockType) == 0) {
				Monitor.Enter(sync, ref lockTaken);
			}

			try {
				// Note: This method has already taken lock by the caller for resolution and access synchronization.
				// For scoped: takes a dictionary as both a resolution lock and a dictionary access lock.
				if (resolvedServices.TryGetValue(callSite.Cache.Key, out var resolved)) {
					return resolved;
				}

				resolved = this.VisitCallSiteMain(callSite, new RuntimeResolverContext {
					Scope = serviceProviderEngine,
					AcquiredLocks = context.AcquiredLocks | lockType
				});

				if (callSite.RuntimeCaptureDisposable)
					serviceProviderEngine.CaptureDisposable(resolved);

				resolvedServices.Add(callSite.Cache.Key, resolved);
				return resolved;
			}
			finally {
				if (lockTaken) {
					Monitor.Exit(sync);
				}
			}
		}

		protected override internal object VisitConstant(ConstantCallSite constantCallSite, RuntimeResolverContext context) => constantCallSite.DefaultValue;

		protected override internal object VisitServiceProvider(ServiceProviderCallSite serviceProviderCallSite, RuntimeResolverContext context) => context.Scope;

		protected override internal object VisitIEnumerable(IEnumerableCallSite enumerableCallSite, RuntimeResolverContext context) {
			var array = Array.CreateInstance(
				enumerableCallSite.ItemType,
				enumerableCallSite.ServiceCallSites.Length);

			for (var index = 0; index < enumerableCallSite.ServiceCallSites.Length; index++) {
				var value = this.VisitCallSite(enumerableCallSite.ServiceCallSites[index], context);
				array.SetValue(value, index);
			}
			return array;
		}

		protected override internal object VisitFactory(FactoryCallSite factoryCallSite, RuntimeResolverContext context) => factoryCallSite.Factory(context.Scope);

		protected internal override object VisitTransposedShared(TransposedSharedCallSite transposedSharedCallSite, RuntimeResolverContext argument) 
			=> this.VisitCallSite(transposedSharedCallSite.ServiceCallSite, argument);
	}

	internal struct RuntimeResolverContext {
		public MultiTenantProviderEngineScope Scope { get; set; }

		public RuntimeResolverLock AcquiredLocks { get; set; }
	}

	[Flags]
	internal enum RuntimeResolverLock {
		Scope = 1,
		Root = 2
	}
}
