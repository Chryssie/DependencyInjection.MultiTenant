// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Internal;
using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	/// <param name="ServiceIdentifier">Type of service being cached</param>
	/// <param name="Slot">
	/// Reverse index of the service when resolved in <c>IEnumerable&lt;Type&gt;</c> where default instance gets slot 0.
	/// For example for service collection
	///  IService Impl1
	///  IService Impl2
	///  IService Impl3
	/// We would get the following cache keys:
	///  Impl1 2
	///  Impl2 1
	///  Impl3 0
	///  </param>
	internal readonly record struct ServiceCacheKey(ServiceIdentifier ServiceIdentifier, Slot Slot = default) {
		public static ServiceCacheKey Empty => default;

		public ServiceCacheKey(Type ServiceType, Slot Slot = default)
			: this(new ServiceIdentifier(ServiceType), Slot) { }
		public ServiceCacheKey(Type ServiceType, TenantIdentifier? TenantIdentifier, Slot Slot = default)
			: this(new ServiceIdentifier(ServiceType, TenantIdentifier), Slot) { }

		public override string ToString() => FormattableString.Invariant($"{this.ServiceIdentifier}[{this.Slot}]");
	}

	internal readonly record struct ServiceFactoryCacheKey(Type ServiceType, Slot Slot = default) {
		public static ServiceFactoryCacheKey Empty => default;

		public override string ToString() => FormattableString.Invariant($"{TypeNameHelper.GetTypeDisplayName(this.ServiceType)}[{this.Slot}]");
	}
}
