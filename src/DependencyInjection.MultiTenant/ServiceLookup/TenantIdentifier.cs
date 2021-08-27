// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal abstract class TenantIdentifier {
		private protected TenantIdentifier() { }
	}
	internal sealed class TestingTenantIdentifier : TenantIdentifier {
		public static readonly TestingTenantIdentifier Instance = new TestingTenantIdentifier();

		private TestingTenantIdentifier() { }

		public override string ToString() => $"<<TEST>>";
	}

	internal sealed class InternalTenantIdentifier<TTenantKey> : TenantIdentifier {
		internal readonly DefaultTenantKeyAccessor<TTenantKey> Accessor;

		public InternalTenantIdentifier(TTenantKey tenantKey) => this.Accessor = new(tenantKey);

		public override string? ToString() => $"({this.Accessor?.TenantKey?.ToString()})";

		internal ConstantCallSite CreateTenantKeyAcessorCallSite() => new (new(typeof(ITenantKeyAcessor<TTenantKey>)), this.Accessor);
	}

	public sealed class DefaultTenantKeyAccessor<TTenantKey> : ITenantKeyAcessor<TTenantKey> {
		public DefaultTenantKeyAccessor(TTenantKey tenantKey) => this.TenantKey = tenantKey;

		public TTenantKey TenantKey { get; }
	}
}
