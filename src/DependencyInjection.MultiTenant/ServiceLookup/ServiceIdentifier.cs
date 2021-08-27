// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Internal;
using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal readonly record struct ServiceIdentifier(Type Type, TenantIdentifier? TenantId = null) : IEquatable<ServiceIdentifier> {
		internal ServiceIdentifier(TenantIdentifier? TenantIdentifier) : this(Type: null!, TenantIdentifier) { }

		public static ServiceIdentifier Empty => default;

		public override string ToString() => FormattableString.Invariant($"{TypeNameHelper.GetTypeDisplayName(this.Type)}{this.TenantId}");
	}
}
