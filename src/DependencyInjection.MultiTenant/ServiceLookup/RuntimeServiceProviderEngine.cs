// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup
{
    internal sealed class RuntimeServiceProviderEngine : MultiTenantServiceProviderEngine {
        public static RuntimeServiceProviderEngine Instance { get; } = new RuntimeServiceProviderEngine();

        private RuntimeServiceProviderEngine() { }

		public override Func<MultiTenantProviderEngineScope, object> RealizeService(ServiceCallSite callSite) => scope => {
			return CallSiteRuntimeResolver.Instance.Resolve(callSite, scope);
		};
	}
}
