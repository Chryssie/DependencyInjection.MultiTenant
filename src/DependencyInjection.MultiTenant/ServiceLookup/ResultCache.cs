// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal readonly struct ResultCache
    {
        public static ResultCache None { get; } = new ResultCache(CallSiteResultCacheLocation.None, ServiceCacheKey.Empty);

        internal ResultCache(CallSiteResultCacheLocation lifetime, ServiceCacheKey cacheKey)
        {
			this.Location = lifetime;
			this.Key = cacheKey;
        }

        public ResultCache(ServiceLifetime lifetime, ServiceIdentifier serviceIdentifier, Slot slot)
        {
            Debug.Assert(lifetime == ServiceLifetime.Transient || serviceIdentifier.Type != null);

			this.Location = lifetime switch {
				ServiceLifetime.Singleton => CallSiteResultCacheLocation.Root,
				ServiceLifetime.Scoped => CallSiteResultCacheLocation.Scope,
				ServiceLifetime.Transient => CallSiteResultCacheLocation.Dispose,
				_ => CallSiteResultCacheLocation.None,
			};
			this.Key = new ServiceCacheKey(serviceIdentifier, slot);
        }

        public readonly CallSiteResultCacheLocation Location;

        public readonly ServiceCacheKey Key;

		internal TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> callSiteVisitor, ServiceCallSite callSite, TArgument argument) => this.Location switch {
			CallSiteResultCacheLocation.Root => callSiteVisitor.VisitRootCache(callSite, argument),
			CallSiteResultCacheLocation.Scope => callSiteVisitor.VisitScopeCache(callSite, argument),
			CallSiteResultCacheLocation.Dispose => callSiteVisitor.VisitDisposeCache(callSite, argument),
			CallSiteResultCacheLocation.None => callSiteVisitor.VisitNoCache(callSite, argument),
			_ => throw new ArgumentOutOfRangeException(),
		};
    }
}
