// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class ConstantCallSite : ServiceCallSite {
		internal object DefaultValue => this.Value;

		public ConstantCallSite(in ServiceIdentifier serviceIdentifier, object defaultValue) : base(ResultCache.None) {
			this.ServiceType = serviceIdentifier;

			if (this.ServiceType.Type is null) throw new ArgumentNullException(nameof(this.ServiceType));

			if (defaultValue != null && !this.ServiceType.Type!.IsInstanceOfType(defaultValue)) {
				throw new ArgumentException(SR.ConstantCantBeConvertedToServiceType(defaultValue.GetType(), this.ServiceType.Type));
			}

			this.Value = defaultValue;
		}

		public override ServiceIdentifier ServiceType { get; }
		public override ServiceIdentifier ImplementationType => new(this.DefaultValue?.GetType() ?? this.ServiceType.Type, this.ServiceType.TenantId);

		protected internal override TResult Accept<TArgument, TResult>(CallSiteVisitor<TArgument, TResult> visitor, TArgument argument) => visitor.VisitConstant(this, argument);
	}
}
