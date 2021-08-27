// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection.ServiceLookup;

namespace Microsoft.Extensions.DependencyInjection {
	internal sealed class CallSiteJsonFormatter : CallSiteVisitor<CallSiteJsonFormatter.CallSiteFormatterContext, object> {
		internal static CallSiteJsonFormatter Instance = new CallSiteJsonFormatter();

		private CallSiteJsonFormatter() {
		}

		public string Format(ServiceCallSite callSite) {
			var stringBuilder = new StringBuilder();
			var context = new CallSiteFormatterContext(stringBuilder, 0, new HashSet<ServiceCallSite>());

			this.VisitCallSite(callSite, context);

			return stringBuilder.ToString();
		}

		protected internal override object VisitConstructor(ConstructorCallSite constructorCallSite, CallSiteFormatterContext argument) {
			argument.WriteProperty("implementationType", constructorCallSite.ImplementationType);

			if (constructorCallSite.ParameterCallSites.Length > 0) {
				argument.StartProperty("arguments");

				var childContext = argument.StartArray();
				foreach (var parameter in constructorCallSite.ParameterCallSites) {
					childContext.StartArrayItem();
					this.VisitCallSite(parameter, childContext);
				}
				argument.EndArray();
			}

			return null;
		}

		protected internal override object VisitCallSiteMain(ServiceCallSite callSite, CallSiteFormatterContext argument) {
			if (argument.ShouldFormat(callSite)) {
				var childContext = argument.StartObject();

				childContext.WriteProperty("serviceType", callSite.ServiceType);
				childContext.WriteProperty("kind", callSite.GetType().Name);
				childContext.WriteProperty("cache", callSite.Cache.Location);

				base.VisitCallSiteMain(callSite, childContext);

				argument.EndObject();
			}
			else {
				var childContext = argument.StartObject();
				childContext.WriteProperty("ref", callSite.ServiceType);
				argument.EndObject();
			}

			return null;
		}

		protected internal override object VisitConstant(ConstantCallSite constantCallSite, CallSiteFormatterContext argument) {
			argument.WriteProperty("value", constantCallSite.DefaultValue ?? "");

			return null;
		}

		protected internal override object VisitServiceProvider(ServiceProviderCallSite serviceProviderCallSite, CallSiteFormatterContext argument) => null;

		protected internal override object VisitIEnumerable(IEnumerableCallSite enumerableCallSite, CallSiteFormatterContext argument) {
			argument.WriteProperty("itemType", enumerableCallSite.ItemType);
			argument.WriteProperty("size", enumerableCallSite.ServiceCallSites.Length);

			if (enumerableCallSite.ServiceCallSites.Length > 0) {
				argument.StartProperty("items");

				var childContext = argument.StartArray();
				foreach (var item in enumerableCallSite.ServiceCallSites) {
					childContext.StartArrayItem();
					this.VisitCallSite(item, childContext);
				}
				argument.EndArray();
			}
			return null;
		}

		protected internal override object VisitFactory(FactoryCallSite factoryCallSite, CallSiteFormatterContext argument) {
			argument.WriteProperty("method", factoryCallSite.Factory.Method);

			return null;
		}

		protected internal override object VisitTransposedShared(TransposedSharedCallSite transposedSharedCallSite, CallSiteFormatterContext argument) {
			argument.WriteProperty("transposed", transposedSharedCallSite.ServiceType.TenantId?.ToString());
			this.VisitCallSite(transposedSharedCallSite.ServiceCallSite, argument);

			return null;
		}

		internal struct CallSiteFormatterContext {
			private readonly HashSet<ServiceCallSite> _processedCallSites;

			public CallSiteFormatterContext(StringBuilder builder, int offset, HashSet<ServiceCallSite> processedCallSites) {
				this.Builder = builder;
				this.Offset = offset;
				this._processedCallSites = processedCallSites;
				this._firstItem = true;
			}

			private bool _firstItem;

			public int Offset { get; }
			public StringBuilder Builder { get; }

			public bool ShouldFormat(ServiceCallSite serviceCallSite) => this._processedCallSites.Add(serviceCallSite);

			public CallSiteFormatterContext IncrementOffset() => new CallSiteFormatterContext(this.Builder, this.Offset + 4, this._processedCallSites) {
				_firstItem = true
			};

			public CallSiteFormatterContext StartObject() {
				this.Builder.Append('{');
				return this.IncrementOffset();
			}

			public void EndObject() => this.Builder.Append('}');

			public void StartProperty(string name) {
				if (!this._firstItem) {
					this.Builder.Append(',');
				}
				else {
					this._firstItem = false;
				}
				this.Builder.AppendFormat("\"{0}\":", name);
			}

			public void StartArrayItem() {
				if (!this._firstItem) {
					this.Builder.Append(',');
				}
				else {
					this._firstItem = false;
				}
			}

			public void WriteProperty(string name, object value) {
				this.StartProperty(name);
				if (value != null) {
					this.Builder.AppendFormat(" \"{0}\"", value);
				}
				else {
					this.Builder.Append("null");
				}
			}

			public CallSiteFormatterContext StartArray() {
				this.Builder.Append('[');
				return this.IncrementOffset();
			}

			public void EndArray() => this.Builder.Append(']');
		}
	}
}
