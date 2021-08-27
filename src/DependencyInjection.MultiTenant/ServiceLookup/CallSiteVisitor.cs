// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal abstract class CallSiteVisitor<TArgument, TResult> {
		private readonly StackGuard _stackGuard;

		protected CallSiteVisitor() {
			this._stackGuard = new StackGuard();
		}

		protected virtual TResult VisitCallSite(ServiceCallSite callSite, TArgument argument) {
			if (!this._stackGuard.TryEnterOnCurrentStack()) 
				return this._stackGuard.RunOnEmptyStack((c, a) => this.VisitCallSite(c, a), callSite, argument);

			return callSite.Cache.Accept(this, callSite, argument);
		}

		protected internal virtual TResult VisitCallSiteMain(ServiceCallSite callSite, TArgument argument) => callSite.Accept(this, argument);

		protected internal virtual TResult VisitNoCache(ServiceCallSite callSite, TArgument argument) => this.VisitCallSiteMain(callSite, argument);
		protected internal virtual TResult VisitDisposeCache(ServiceCallSite callSite, TArgument argument) => this.VisitCallSiteMain(callSite, argument);
		protected internal virtual TResult VisitRootCache(ServiceCallSite callSite, TArgument argument) => this.VisitCallSiteMain(callSite, argument);
		protected internal virtual TResult VisitScopeCache(ServiceCallSite callSite, TArgument argument) => this.VisitCallSiteMain(callSite, argument);

		protected internal abstract TResult VisitConstructor(ConstructorCallSite constructorCallSite, TArgument argument);

		protected internal abstract TResult VisitConstant(ConstantCallSite constantCallSite, TArgument argument);

		protected internal abstract TResult VisitServiceProvider(ServiceProviderCallSite serviceProviderCallSite, TArgument argument);

		protected internal abstract TResult VisitIEnumerable(IEnumerableCallSite enumerableCallSite, TArgument argument);

		protected internal abstract TResult VisitFactory(FactoryCallSite factoryCallSite, TArgument argument);

		protected internal abstract TResult VisitTransposedShared(TransposedSharedCallSite transposedSharedCallSite, TArgument argument);
	}
}
