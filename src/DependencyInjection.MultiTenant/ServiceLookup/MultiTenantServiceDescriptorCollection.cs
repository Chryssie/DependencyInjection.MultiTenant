// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {

	internal struct MultiTenantServiceDescriptorCollection {
		private MultiTenantServiceDescriptorItem[] items;

		public readonly ref readonly MultiTenantServiceDescriptorItem Last => ref this.items[^1];

		public readonly int Count => this.items.Length;
		public readonly bool IsEmpty => this.items.Length == 0;

		public readonly ref readonly MultiTenantServiceDescriptorItem this[int index] => ref this.items[index];
		public ref readonly MultiTenantServiceDescriptorItem this[Slot slot] => ref this.items[this.GetIndex(slot)];

		public readonly Slot GetSlot(int index) => Slot.FromIndexAndCount(index, this.items.Length);
		public Slot GetSlot(ServiceDescriptor descriptor) => this.GetSlot(this.GetIndex(descriptor));
		public readonly int GetIndex(Slot slot) => slot.ToIndex(this.items.Length);

		public int GetIndex(ServiceDescriptor descriptor) {
			for (var i = 0; i < this.items.Length; i++) {
				if (this.items[i].Descriptor == descriptor)
					return i;
			}

			throw new InvalidOperationException(SR.ServiceDescriptorNotExist());
		}

		public readonly ref readonly MultiTenantServiceDescriptorItem GetInfo(ServiceDescriptor descriptor) {
			for (var i = this.items.Length - 1; i >= 0; i--) {
				ref readonly var current = ref this.items[i];
				if (current.Descriptor == descriptor)
					return ref current;
			}

			throw new InvalidOperationException(SR.ServiceDescriptorNotExist());
		}
		public readonly ref readonly MultiTenantServiceDescriptorItem GetInfo(ServiceDescriptor descriptor, out Slot slot) {
			for (var i = this.items.Length - 1; i >= 0; i--) {
				ref readonly var current = ref this.items[i];
				if (current.Descriptor == descriptor) {
					slot = this.GetSlot(i);
					return ref current;
				}
			}

			throw new InvalidOperationException(SR.ServiceDescriptorNotExist());
		}

		public sealed class Builder {
			private readonly List<MultiTenantServiceDescriptorItem> shared = new();
			private readonly List<MultiTenantServiceDescriptorItem> tenanted = new();

			public void Add(ServiceDescriptor descriptor) {
				var transposedSharedIndex = -1;
				if (descriptor.IsShared()) {
					transposedSharedIndex = shared.Count;
					this.shared.Add(new(descriptor, -1));
				}

				this.tenanted.Add(new(descriptor, transposedSharedIndex));
			}

			public (MultiTenantServiceDescriptorCollection Shared, MultiTenantServiceDescriptorCollection Tenanted) Build()
				=> (Shared: new() { items = this.shared.ToArray(), }, Tenanted: new() { items = this.tenanted.ToArray(), });
		}
	}
}
