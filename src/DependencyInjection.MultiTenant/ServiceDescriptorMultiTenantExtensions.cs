using System.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection {
	public static class ServiceDescriptorMultiTenantExtensions {
		public static bool IsShared(this ServiceDescriptor descriptor)
			=> descriptor is not MultiTenantServiceDescriptor;
		public static bool IsTenanted(this ServiceDescriptor descriptor)
			=> descriptor is MultiTenantServiceDescriptor;

		public static MultiTenantServiceDescriptor AsMultiTenant(this ServiceDescriptor descriptor) {
			if (descriptor is MultiTenantServiceDescriptor mtsd)
				return mtsd;

			if (descriptor.ImplementationFactory is { } implementationFactory)
				return new MultiTenantServiceDescriptor(descriptor.ServiceType, implementationFactory, descriptor.Lifetime);
			if (descriptor.ImplementationType is { } implementationType)
				return new MultiTenantServiceDescriptor(descriptor.ServiceType, implementationType, descriptor.Lifetime);

			Debug.Assert(descriptor.Lifetime == ServiceLifetime.Singleton);

			return new MultiTenantServiceDescriptor(descriptor.ServiceType, descriptor.ImplementationInstance);
		}

		public static ServiceDescriptor AsSingleTenant(this ServiceDescriptor descriptor) 
			=> descriptor is MultiTenantServiceDescriptor mtsd ? mtsd.AsSingleTenant() : descriptor;
	}
}
