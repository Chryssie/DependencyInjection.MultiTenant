// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class ILEmitServiceProviderEngine : MultiTenantServiceProviderEngine {
		private readonly ILEmitResolverBuilder _expressionResolverBuilder;
		public ILEmitServiceProviderEngine(MultiTenantServiceProvider serviceProvider) {
			this._expressionResolverBuilder = new ILEmitResolverBuilder(serviceProvider);
		}

		public override Func<MultiTenantProviderEngineScope, object> RealizeService(ServiceCallSite callSite) => this._expressionResolverBuilder.Build(callSite);
	}
}
