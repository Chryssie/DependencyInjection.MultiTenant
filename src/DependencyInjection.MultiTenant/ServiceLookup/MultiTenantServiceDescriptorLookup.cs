// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal struct MultiTenantServiceDescriptorLookup {
		public readonly TenantServiceDescriptorLookup Shared;
		public readonly TenantServiceDescriptorLookup Tenanted;

		private MultiTenantServiceDescriptorLookup(TenantServiceDescriptorLookup shared, TenantServiceDescriptorLookup tenanted) {
			this.Shared = shared;
			this.Tenanted = tenanted;
		}

		public readonly bool ContainsService(ServiceIdentifier serviceIdentifier)
			=> (serviceIdentifier.TenantId is not null ? this.Tenanted : this.Shared).ContainsService(serviceIdentifier.Type);

		public readonly bool TryGetDescriptors(ServiceIdentifier serviceIdentifier, [MaybeNullWhen(false)] out MultiTenantServiceDescriptorCollection result)
			=> (serviceIdentifier.TenantId is not null ? this.Tenanted : this.Shared).TryGetDescriptors(serviceIdentifier.Type, out result);

		public readonly ref readonly MultiTenantServiceDescriptorItem GetInfo(ServiceIdentifier serviceIdentifier, ServiceDescriptor descriptor)
			=> ref serviceIdentifier.TenantId is not null ? ref this.Tenanted.GetInfo(descriptor) : ref this.Shared.GetInfo(descriptor);

		public readonly ref readonly MultiTenantServiceDescriptorItem GetSharedInfo(in MultiTenantServiceDescriptorItem descriptor) {
			if (descriptor.IsTransposedShared)
				return ref descriptor;

			if (!this.Shared.TryGetDescriptors(descriptor.Descriptor.ServiceType, out var descriptors))
				throw new InvalidOperationException(SR.InvalidServiceDescriptor());

			return ref descriptors[descriptor.SharedIndex];
		}

		public sealed class Builder {
			public readonly Dictionary<Type, MultiTenantServiceDescriptorCollection.Builder> builder = new();

			public void Add(ServiceDescriptor descriptor) {
				if (descriptor is null)
					throw new ArgumentNullException(nameof(descriptor));

				var serviceType = descriptor.ServiceType;

				if (!this.builder.TryGetValue(serviceType, out var builder))
					this.builder.Add(serviceType, builder = new());

				builder.Add(descriptor);
			}

			public MultiTenantServiceDescriptorLookup Build() {
				var sharedBuilder = ImmutableDictionary.CreateBuilder<Type, MultiTenantServiceDescriptorCollection>();
				var tenantedBuilder = ImmutableDictionary.CreateBuilder<Type, MultiTenantServiceDescriptorCollection>();

				foreach (var (type, builder) in this.builder) {
					var (shared, tenanted) = builder.Build();
					if (!shared.IsEmpty)
						sharedBuilder.Add(type, shared);
					if (!tenanted.IsEmpty)
						tenantedBuilder.Add(type, tenanted);
				}

				return new(new(sharedBuilder.ToImmutable()), new(tenantedBuilder.ToImmutable()));
			}
		}
	}
}
