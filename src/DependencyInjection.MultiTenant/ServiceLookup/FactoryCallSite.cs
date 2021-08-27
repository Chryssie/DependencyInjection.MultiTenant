// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class FactoryCallSite : ServiceCallSite {
		public Func<IServiceProvider, object> Factory { get; }

		public FactoryCallSite(ResultCache cache, ServiceIdentifier serviceType, Func<IServiceProvider, object> factory) : base(cache) {
			this.Factory = factory;
			this.ServiceType = serviceType;
		}

		public override ServiceIdentifier ServiceType { get; }
		public override ServiceIdentifier ImplementationType => new(null, this.ServiceType.TenantId);

		protected internal override TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> visitor, TArgument argument) => visitor.VisitFactory(this, argument);
	}
}
