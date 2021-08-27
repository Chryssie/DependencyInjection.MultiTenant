// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal abstract class ServiceProviderCallSite : ServiceCallSite {
		public ServiceProviderCallSite() : base(ResultCache.None) { }
		public sealed override ServiceIdentifier ServiceType => new(typeof(IServiceProvider));

		protected internal sealed override TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> visitor, TArgument argument) => visitor.VisitServiceProvider(this, argument);
	}
	internal sealed class ServiceProviderCallSite<TTenantId> : ServiceProviderCallSite {
		public override ServiceIdentifier ImplementationType => new(typeof(MultiTenantServiceProvider<TTenantId>));


	}
}
