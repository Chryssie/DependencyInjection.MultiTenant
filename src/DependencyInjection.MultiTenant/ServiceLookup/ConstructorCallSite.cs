// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class ConstructorCallSite : ServiceCallSite {
		internal ConstructorInfo ConstructorInfo { get; }
		internal ServiceCallSite[] ParameterCallSites { get; }

		public ConstructorCallSite(ResultCache cache, in ServiceIdentifier serviceIdentifier, ConstructorInfo constructorInfo)
			: this(cache, serviceIdentifier, constructorInfo, Array.Empty<ServiceCallSite>()) { }

		public ConstructorCallSite(ResultCache cache, in ServiceIdentifier serviceIdentifier, ConstructorInfo constructorInfo, ServiceCallSite[] parameterCallSites) : base(cache) {
			this.ServiceType = serviceIdentifier;

			if (!this.ServiceType.Type.IsAssignableFrom(constructorInfo.DeclaringType)) {
				throw new ArgumentException(SR.ImplementationTypeCantBeConvertedToServiceType(constructorInfo.DeclaringType, this.ServiceType.Type));
			}

			this.ConstructorInfo = constructorInfo;
			this.ParameterCallSites = parameterCallSites;
		}

		public override ServiceIdentifier ServiceType { get; }
		public override ServiceIdentifier ImplementationType => new(this.ConstructorInfo.DeclaringType!, this.ServiceType.TenantId);


		protected internal override TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> visitor, TArgument argument) => visitor.VisitConstructor(this, argument);
	}
}
