// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal record struct MultiTenantServiceDescriptorItem(ServiceDescriptor Descriptor, int SharedIndex) {
		public readonly bool IsTransposedShared => this.SharedIndex != -1;
	}
}
