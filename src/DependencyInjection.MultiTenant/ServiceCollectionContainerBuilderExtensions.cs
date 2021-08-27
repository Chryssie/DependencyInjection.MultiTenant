// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection.ServiceLookup;

namespace Microsoft.Extensions.DependencyInjection {
	/// <summary>
	/// Extension methods for building a <see cref="ServiceProvider"/> from an <see cref="IServiceCollection"/>.
	/// </summary>
	public static class ServiceCollectionContainerBuilderExtensions {
		/// <summary>
		/// Creates a <see cref="ServiceProvider"/> containing services from the provided <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection"/> containing service descriptors.</param>
		/// <returns>The <see cref="ServiceProvider"/>.</returns>

		public static MultiTenantServiceProvider<TTenantKey> BuildMultiTenantServiceProvider<TTenantKey>(this IServiceCollection services, IEqualityComparer<TTenantKey>? tenantKeyComparer = null) {
			return BuildMultiTenantServiceProvider(services, validateScopes: false, tenantKeyComparer: tenantKeyComparer);
		}

		/// <summary>
		/// Creates a <see cref="ServiceProvider"/> containing services from the provided <see cref="IServiceCollection"/>
		/// optionally enabling scope validation.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection"/> containing service descriptors.</param>
		/// <param name="validateScopes">
		/// <c>true</c> to perform check verifying that scoped services never gets resolved from root provider; otherwise <c>false</c>.
		/// </param>
		/// <returns>The <see cref="ServiceProvider"/>.</returns>
		public static MultiTenantServiceProvider<TTenantKey> BuildMultiTenantServiceProvider<TTenantKey>(this IServiceCollection services, bool validateScopes, IEqualityComparer<TTenantKey>? tenantKeyComparer = null) {
			return services.BuildMultiTenantServiceProvider(new MultiTenantServiceProviderOptions<TTenantKey> { ValidateScopes = validateScopes , TenantIdComparer = tenantKeyComparer, });
		}

		/// <summary>
		/// Creates a <see cref="ServiceProvider"/> containing services from the provided <see cref="IServiceCollection"/>
		/// optionally enabling scope validation.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection"/> containing service descriptors.</param>
		/// <param name="options">
		/// Configures various service provider behaviors.
		/// </param>
		/// <returns>The <see cref="ServiceProvider"/>.</returns>
		public static MultiTenantServiceProvider<TTenantKey> BuildMultiTenantServiceProvider<TTenantKey>(this IServiceCollection services, MultiTenantServiceProviderOptions<TTenantKey> options) {
			if (services == null) {
				throw new ArgumentNullException(nameof(services));
			}

			if (options == null) {
				throw new ArgumentNullException(nameof(options));
			}

			return new MultiTenantServiceProvider<TTenantKey>(services, options);
		}
	}
}
