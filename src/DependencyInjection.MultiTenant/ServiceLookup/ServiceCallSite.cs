// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	/// <summary>Summary description for ServiceCallSite</summary>
	internal abstract class ServiceCallSite {
		protected ServiceCallSite(ResultCache cache)
			=> this.Cache = cache;

		public abstract ServiceIdentifier ServiceType { get; }
		public abstract ServiceIdentifier ImplementationType { get; }
		public ResultCache Cache { get; }
		public object? Value { get; set; }

		public bool RuntimeCaptureDisposable => this.ServiceType.TenantId is not null == this.ImplementationType.TenantId is not null;

		public bool CaptureDisposable => this.RuntimeCaptureDisposable && this.ImplementationType.Type switch {
			null => true,
			var implementationType => typeof(IDisposable).IsAssignableFrom(implementationType) || typeof(IAsyncDisposable).IsAssignableFrom(implementationType),
		};

		protected internal abstract TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> visitor, TArgument argument);
	}
}
