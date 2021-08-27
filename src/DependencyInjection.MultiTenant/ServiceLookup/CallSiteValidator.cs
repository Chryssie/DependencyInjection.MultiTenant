// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class CallSiteValidator : CallSiteVisitor<CallSiteValidator.CallSiteValidatorState, ServiceIdentifier?> {
		internal struct CallSiteValidatorState {
			public ServiceCallSite Singleton { get; set; }
		}
		// Keys are services being resolved via GetService, values - first scoped service in their call site tree
		private readonly ConcurrentDictionary<ServiceIdentifier, ServiceIdentifier> _scopedServices = new ConcurrentDictionary<ServiceIdentifier, ServiceIdentifier>();

		public void ValidateCallSite(ServiceCallSite callSite) {
			if (this.VisitCallSite(callSite, default) is { } scoped)
				this._scopedServices[callSite.ServiceType] = scoped;
		}

		public void ValidateResolution(in ServiceIdentifier serviceIdentifier, IServiceScope scope, IServiceScope rootScope) {
			if (ReferenceEquals(scope, rootScope) && this._scopedServices.TryGetValue(serviceIdentifier, out var scopedService)) {
				if (serviceIdentifier == scopedService) {
					throw new InvalidOperationException(
						SR.DirectScopedResolvedFromRootException(serviceIdentifier,
							nameof(ServiceLifetime.Scoped).ToLowerInvariant()));
				}

				throw new InvalidOperationException(
					SR.ScopedResolvedFromRootException(
						serviceIdentifier,
						scopedService,
						nameof(ServiceLifetime.Scoped).ToLowerInvariant()));
			}
		}

		protected internal override ServiceIdentifier? VisitConstructor(ConstructorCallSite constructorCallSite, CallSiteValidatorState state) {
			ServiceIdentifier? result = null;
			foreach (var parameterCallSite in constructorCallSite.ParameterCallSites) {
				var scoped = this.VisitCallSite(parameterCallSite, state);
				result ??= scoped;
			}
			return result;
		}

		protected internal override ServiceIdentifier? VisitIEnumerable(IEnumerableCallSite enumerableCallSite, CallSiteValidatorState state) {
			ServiceIdentifier? result = null;
			foreach (var serviceCallSite in enumerableCallSite.ServiceCallSites) {
				var scoped = this.VisitCallSite(serviceCallSite, state);
				result ??= scoped;
			}
			return result;
		}

		protected internal override ServiceIdentifier? VisitRootCache(ServiceCallSite singletonCallSite, CallSiteValidatorState state) {
			state.Singleton = singletonCallSite;
			return this.VisitCallSiteMain(singletonCallSite, state);
		}

		protected internal override ServiceIdentifier? VisitScopeCache(ServiceCallSite scopedCallSite, CallSiteValidatorState state) {
			// We are fine with having ServiceScopeService requested by singletons
			if (scopedCallSite.ServiceType.Type == typeof(IServiceScopeFactory)) {
				return null;
			}
			if (state.Singleton != null) {
				throw new InvalidOperationException(SR.ScopedInSingletonException(
					scopedCallSite.ServiceType,
					state.Singleton.ServiceType,
					nameof(ServiceLifetime.Scoped).ToLowerInvariant(),
					nameof(ServiceLifetime.Singleton).ToLowerInvariant()
					));
			}

			this.VisitCallSiteMain(scopedCallSite, state);
			return scopedCallSite.ServiceType;
		}

		protected internal override ServiceIdentifier? VisitConstant(ConstantCallSite constantCallSite, CallSiteValidatorState state) => null;

		protected internal override ServiceIdentifier? VisitServiceProvider(ServiceProviderCallSite serviceProviderCallSite, CallSiteValidatorState state) => null;

		protected internal override ServiceIdentifier? VisitFactory(FactoryCallSite factoryCallSite, CallSiteValidatorState state) => null;

		protected internal override ServiceIdentifier? VisitTransposedShared(TransposedSharedCallSite transposedSharedCallSite, CallSiteValidatorState argument) {
			return this.VisitCallSite(transposedSharedCallSite.ServiceCallSite, argument);
		}
	}
}
