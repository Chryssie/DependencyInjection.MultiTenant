using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Diagnostics.CodeAnalysis {
}
namespace Microsoft.Extensions.DependencyInjection {
	/// <summary>Describes a service with its service type, implementation, and lifetime.</summary>
	[DebuggerDisplay("Lifetime = {Lifetime}, ServiceType = {ServiceType}, ImplementationType = {ImplementationType}")]
	public class MultiTenantServiceDescriptor : ServiceDescriptor {
		/// <summary>Initializes a new instance of <see cref="MultiTenantServiceDescriptor"/> with the specified <paramref name="implementationType"/>.</summary>
		/// <param name="serviceType">The <see cref="Type"/> of the service.</param>
		/// <param name="implementationType">The <see cref="Type"/> implementing the service.</param>
		/// <param name="lifetime">The <see cref="ServiceLifetime"/> of the service.</param>
		public MultiTenantServiceDescriptor(
			Type serviceType,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
			ServiceLifetime lifetime)
			: base(serviceType, implementationType, lifetime) { }

		/// <summary>Initializes a new instance of <see cref="MultiTenantServiceDescriptor"/> with the specified <paramref name="instance"/> as a <see cref="ServiceLifetime.Singleton"/>.</summary>
		/// <param name="serviceType">The <see cref="Type"/> of the service.</param>
		/// <param name="instance">The instance implementing the service.</param>
		public MultiTenantServiceDescriptor(
			Type serviceType,
			object instance)
			: base(serviceType, instance) { }

		public ServiceDescriptor AsSingleTenant() {
			if (this.ImplementationFactory is { } implementationFactory)
				return new ServiceDescriptor(this.ServiceType, implementationFactory, this.Lifetime);
			if (this.ImplementationType is { } implementationType)
				return new ServiceDescriptor(this.ServiceType, implementationType, this.Lifetime);

			Debug.Assert(this.Lifetime == ServiceLifetime.Singleton);

			return new ServiceDescriptor(this.ServiceType, this.ImplementationInstance);
		}

		/// <summary>Initializes a new instance of <see cref="MultiTenantServiceDescriptor"/> with the specified <paramref name="factory"/>.</summary>
		/// <param name="serviceType">The <see cref="Type"/> of the service.</param>
		/// <param name="factory">A factory used for creating service instances.</param>
		/// <param name="lifetime">The <see cref="ServiceLifetime"/> of the service.</param>
		public MultiTenantServiceDescriptor(
			Type serviceType,
			Func<IServiceProvider, object> factory,
			ServiceLifetime lifetime)
			: base(serviceType, factory, lifetime) { }
		/// <inheritdoc />
		public override string ToString() {
			var lifetime = $"{nameof(this.ServiceType)}: {this.ServiceType} {nameof(this.Lifetime)}: {this.Lifetime} ";

			if (this.ImplementationType != null) {
				return lifetime + $"{nameof(this.ImplementationType)}: {this.ImplementationType}";
			}

			if (this.ImplementationFactory != null) {
				return lifetime + $"{nameof(this.ImplementationFactory)}: {this.ImplementationFactory.Method}";
			}

			return lifetime + $"{nameof(this.ImplementationInstance)}: {this.ImplementationInstance}";
		}

		internal Type GetImplementationType() {
			if (this.ImplementationType != null) {
				return this.ImplementationType;
			}
			else if (this.ImplementationInstance != null) {
				return this.ImplementationInstance.GetType();
			}
			else if (this.ImplementationFactory != null) {
				var typeArguments = this.ImplementationFactory.GetType().GenericTypeArguments;

				Debug.Assert(typeArguments.Length == 2);

				return typeArguments[1];
			}

			Debug.Assert(false, "ImplementationType, ImplementationInstance or ImplementationFactory must be non null");
			return null;
		}

		/// <summary>Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified <typeparamref name="TService"/>, <typeparamref name="TImplementation"/>, and the <see cref="ServiceLifetime.Transient"/> lifetime.</summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Transient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
			where TService : class
			where TImplementation : class, TService
			=> Describe<TService, TImplementation>(ServiceLifetime.Transient);

		/// <summary>Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified <paramref name="service"/> and <paramref name="implementationType"/> and the <see cref="ServiceLifetime.Transient"/> lifetime. </summary>
		/// <param name="service">The type of the service.</param>
		/// <param name="implementationType">The type of the implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Transient(
			Type service,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType) {
			if (service is null)
				throw new ArgumentNullException(nameof(service));
			if (implementationType is null)
				throw new ArgumentNullException(nameof(implementationType));

			return Describe(service, implementationType, ServiceLifetime.Transient);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <typeparamref name="TImplementation"/>,
		/// <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Transient"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Transient<TService, TImplementation>(
			Func<IServiceProvider, TImplementation> implementationFactory)
			where TService : class
			where TImplementation : class, TService {
			if (implementationFactory is null)
				throw new ArgumentNullException(nameof(implementationFactory));

			return Describe(typeof(TService), implementationFactory, ServiceLifetime.Transient);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Transient"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Transient<TService>(Func<IServiceProvider, TService> implementationFactory)
			where TService : class {
			if (implementationFactory is null) {
				throw new ArgumentNullException(nameof(implementationFactory));
			}

			return Describe(typeof(TService), implementationFactory, ServiceLifetime.Transient);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="service"/>, <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Transient"/> lifetime.
		/// </summary>
		/// <param name="service">The type of the service.</param>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Transient(Type service, Func<IServiceProvider, object> implementationFactory) {
			if (service is null) {
				throw new ArgumentNullException(nameof(service));
			}

			if (implementationFactory is null) {
				throw new ArgumentNullException(nameof(implementationFactory));
			}

			return Describe(service, implementationFactory, ServiceLifetime.Transient);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <typeparamref name="TImplementation"/>,
		/// and the <see cref="ServiceLifetime.Scoped"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Scoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
			where TService : class
			where TImplementation : class, TService
			=> Describe<TService, TImplementation>(ServiceLifetime.Scoped);

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="service"/> and <paramref name="implementationType"/>
		/// and the <see cref="ServiceLifetime.Scoped"/> lifetime.
		/// </summary>
		/// <param name="service">The type of the service.</param>
		/// <param name="implementationType">The type of the implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Scoped(
			Type service,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
			=> Describe(service, implementationType, ServiceLifetime.Scoped);

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <typeparamref name="TImplementation"/>,
		/// <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Scoped"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Scoped<TService, TImplementation>(
			Func<IServiceProvider, TImplementation> implementationFactory)
			where TService : class
			where TImplementation : class, TService {
			if (implementationFactory is null)
				throw new ArgumentNullException(nameof(implementationFactory));

			return Describe(typeof(TService), implementationFactory, ServiceLifetime.Scoped);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Scoped"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Scoped<TService>(Func<IServiceProvider, TService> implementationFactory)
			where TService : class {
			if (implementationFactory is null)
				throw new ArgumentNullException(nameof(implementationFactory));

			return Describe(typeof(TService), implementationFactory, ServiceLifetime.Scoped);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="service"/>, <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Scoped"/> lifetime.
		/// </summary>
		/// <param name="service">The type of the service.</param>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Scoped(Type service, Func<IServiceProvider, object> implementationFactory) {
			if (service is null)
				throw new ArgumentNullException(nameof(service));

			if (implementationFactory is null)
				throw new ArgumentNullException(nameof(implementationFactory));

			return Describe(service, implementationFactory, ServiceLifetime.Scoped);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <typeparamref name="TImplementation"/>,
		/// and the <see cref="ServiceLifetime.Singleton"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Singleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
			where TService : class
			where TImplementation : class, TService
			=> Describe<TService, TImplementation>(ServiceLifetime.Singleton);

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="service"/> and <paramref name="implementationType"/>
		/// and the <see cref="ServiceLifetime.Singleton"/> lifetime.
		/// </summary>
		/// <param name="service">The type of the service.</param>
		/// <param name="implementationType">The type of the implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Singleton(
			Type service,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType) {
			if (service is null)
				throw new ArgumentNullException(nameof(service));

			if (implementationType is null)
				throw new ArgumentNullException(nameof(implementationType));

			return Describe(service, implementationType, ServiceLifetime.Singleton);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <typeparamref name="TImplementation"/>,
		/// <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Singleton"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Singleton<TService, TImplementation>(
			Func<IServiceProvider, TImplementation> implementationFactory)
			where TService : class
			where TImplementation : class, TService {
			if (implementationFactory is null)
				throw new ArgumentNullException(nameof(implementationFactory));

			return Describe(typeof(TService), implementationFactory, ServiceLifetime.Singleton);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Singleton"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Singleton<TService>(Func<IServiceProvider, TService> implementationFactory)
			where TService : class {
			if (implementationFactory is null)
				throw new ArgumentNullException(nameof(implementationFactory));

			return Describe(typeof(TService), implementationFactory, ServiceLifetime.Singleton);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="serviceType"/>, <paramref name="implementationFactory"/>,
		/// and the <see cref="ServiceLifetime.Singleton"/> lifetime.
		/// </summary>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Singleton(
			Type serviceType,
			Func<IServiceProvider, object> implementationFactory) {
			if (serviceType is null)
				throw new ArgumentNullException(nameof(serviceType));

			if (implementationFactory is null)
				throw new ArgumentNullException(nameof(implementationFactory));

			return Describe(serviceType, implementationFactory, ServiceLifetime.Singleton);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <typeparamref name="TService"/>, <paramref name="implementationInstance"/>,
		/// and the <see cref="ServiceLifetime.Singleton"/> lifetime.
		/// </summary>
		/// <typeparam name="TService">The type of the service.</typeparam>
		/// <param name="implementationInstance">The instance of the implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Singleton<TService>(TService implementationInstance)
			where TService : class {
			if (implementationInstance is null)
				throw new ArgumentNullException(nameof(implementationInstance));

			return Singleton(typeof(TService), implementationInstance);
		}

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="serviceType"/>, <paramref name="implementationInstance"/>,
		/// and the <see cref="ServiceLifetime.Singleton"/> lifetime.
		/// </summary>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="implementationInstance">The instance of the implementation.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Singleton(
			Type serviceType,
			object implementationInstance) {
			if (serviceType is null)
				throw new ArgumentNullException(nameof(serviceType));

			if (implementationInstance is null)
				throw new ArgumentNullException(nameof(implementationInstance));

			return new MultiTenantServiceDescriptor(serviceType, implementationInstance);
		}

		private static MultiTenantServiceDescriptor Describe<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(ServiceLifetime lifetime)
			where TService : class
			where TImplementation : class, TService
			=> Describe(typeof(TService), typeof(TImplementation), lifetime: lifetime);

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="serviceType"/>, <paramref name="implementationType"/>,
		/// and <paramref name="lifetime"/>.
		/// </summary>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="implementationType">The type of the implementation.</param>
		/// <param name="lifetime">The lifetime of the service.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Describe(
			Type serviceType,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
			ServiceLifetime lifetime)
			=> new(serviceType, implementationType, lifetime);

		/// <summary>
		/// Creates an instance of <see cref="MultiTenantServiceDescriptor"/> with the specified
		/// <paramref name="serviceType"/>, <paramref name="implementationFactory"/>,
		/// and <paramref name="lifetime"/>.
		/// </summary>
		/// <param name="serviceType">The type of the service.</param>
		/// <param name="implementationFactory">A factory to create new instances of the service implementation.</param>
		/// <param name="lifetime">The lifetime of the service.</param>
		/// <returns>A new instance of <see cref="MultiTenantServiceDescriptor"/>.</returns>
		public static new MultiTenantServiceDescriptor Describe(Type serviceType, Func<IServiceProvider, object> implementationFactory, ServiceLifetime lifetime)
			=> new(serviceType, implementationFactory, lifetime);
	}
}
