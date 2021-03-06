// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal abstract class MultiTenantServiceProviderEngine {
		public abstract Func<MultiTenantProviderEngineScope, object> RealizeService(ServiceCallSite callSite);
	}
}
