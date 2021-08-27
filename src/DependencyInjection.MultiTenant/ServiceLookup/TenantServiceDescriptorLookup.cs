// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	using static MethodImplOptions;
	internal struct TenantServiceDescriptorLookup {
		private readonly ImmutableDictionary<Type, MultiTenantServiceDescriptorCollection> items;

		internal TenantServiceDescriptorLookup(ImmutableDictionary<Type, MultiTenantServiceDescriptorCollection> items) => this.items = items;

		public bool ContainsService(Type serviceType)
			=> this.items.ContainsKey(serviceType);

		public bool TryGetDescriptors(Type serviceType, [MaybeNullWhen(false)] out MultiTenantServiceDescriptorCollection result)
			=> this.items.TryGetValue(serviceType, out result);

		public readonly ref readonly MultiTenantServiceDescriptorItem GetInfo(ServiceDescriptor descriptor, out Slot slot) {
			if (!this.items.TryGetValue(descriptor.ServiceType, out var result))
				throw new InvalidOperationException(SR.ServiceDescriptorNotExist());


			return ref result.GetInfo(descriptor, out slot);
		}
		public readonly ref readonly MultiTenantServiceDescriptorItem GetInfo(ServiceDescriptor descriptor) {
			if (!this.items.TryGetValue(descriptor.ServiceType, out var result))
				throw new InvalidOperationException(SR.ServiceDescriptorNotExist());

			return ref result.GetInfo(descriptor);
		}

		public readonly ref readonly MultiTenantServiceDescriptorItem GetInfoAtIndex(Type serviceType, int index) {
			if (!this.items.TryGetValue(serviceType, out var result))
				throw new InvalidOperationException(SR.ServiceDescriptorNotExist());

			return ref result[index];
		}

		public readonly ref readonly MultiTenantServiceDescriptorItem GetInfoAtSlot(Type serviceType, int index) {
			if (!this.items.TryGetValue(serviceType, out var result))
				throw new InvalidOperationException(SR.ServiceDescriptorNotExist());

			return ref result[index];
		}
	}
}
