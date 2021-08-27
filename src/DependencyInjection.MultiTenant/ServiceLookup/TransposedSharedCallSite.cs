// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class TransposedSharedCallSite : ServiceCallSite {
		public TransposedSharedCallSite(ServiceIdentifier serviceIdentifier, ServiceCallSite serviceCallSite) : base(ResultCache.None) {
			this.ServiceType = serviceIdentifier;
			this.ServiceCallSite = serviceCallSite;
		}

		public override ServiceIdentifier ServiceType { get; }
		public override ServiceIdentifier ImplementationType => new(this.ServiceType.Type);

		public ServiceCallSite ServiceCallSite { get; }

		protected internal override TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> visitor, TArgument argument) => visitor.VisitTransposedShared(this, argument);
	}
}
