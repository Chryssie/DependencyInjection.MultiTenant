// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup
{
    internal abstract class CompiledServiceProviderEngine : MultiTenantServiceProviderEngine {
#if IL_EMIT
        public ILEmitResolverBuilder ResolverBuilder { get; }
#else
        public ExpressionResolverBuilder ResolverBuilder { get; }
#endif

        public CompiledServiceProviderEngine(MultiTenantServiceProvider provider)
        {
			this.ResolverBuilder = new(provider);
        }

        public override Func<MultiTenantProviderEngineScope, object> RealizeService(ServiceCallSite callSite) => this.ResolverBuilder.Build(callSite);
    }
}
