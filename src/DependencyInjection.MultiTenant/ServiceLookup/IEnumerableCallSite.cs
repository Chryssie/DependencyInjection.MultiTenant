// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class IEnumerableCallSite : ServiceCallSite {
		internal TenantIdentifier TenantId { get; }
		internal Type ItemType { get; }
		internal ImmutableArray<ServiceCallSite> ServiceCallSites { get; }

		public IEnumerableCallSite(ResultCache cache, in ServiceIdentifier itemServiceIdentifier, ImmutableArray<ServiceCallSite> serviceCallSites) : base(cache) {
			this.TenantId = itemServiceIdentifier.TenantId;
			this.ItemType = itemServiceIdentifier.Type;
			this.ServiceCallSites = serviceCallSites;
		}

		public override ServiceIdentifier ServiceType => new(typeof(IEnumerable<>).MakeGenericType(this.ItemType), this.TenantId);
		public override ServiceIdentifier ImplementationType => new(this.ItemType.MakeArrayType(), this.TenantId);

		protected internal override TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> visitor, TArgument argument) => visitor.VisitIEnumerable(this, argument);
	}
}
