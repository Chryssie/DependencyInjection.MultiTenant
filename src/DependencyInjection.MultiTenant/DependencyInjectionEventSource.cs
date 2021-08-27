// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection.ServiceLookup;
using System;
using System.Diagnostics.Tracing;
using System.Linq.Expressions;

namespace Microsoft.Extensions.DependencyInjection {
	[EventSource(Name = "Microsoft-Extensions-DependencyInjection")]
	internal sealed class DependencyInjectionEventSource : EventSource {
		public static readonly DependencyInjectionEventSource Log = new DependencyInjectionEventSource();

		// Event source doesn't support large payloads so we chunk formatted call site tree
		private const int MaxChunkSize = 10 * 1024;

		private DependencyInjectionEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat) {
		}

		// NOTE
		// - The 'Start' and 'Stop' suffixes on the following event names have special meaning in EventSource. They
		//   enable creating 'activities'.
		//   For more information, take a look at the following blog post:
		//   https://blogs.msdn.microsoft.com/vancem/2015/09/14/exploring-eventsource-activity-correlation-and-causation-features/
		// - A stop event's event id must be next one after its start event.
		// - Avoid renaming methods or parameters marked with EventAttribute. EventSource uses these to form the event object.

		//[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
		//    Justification = "Parameters to this method are primitive and are trimmer safe.")]
		[Event(1, Level = EventLevel.Verbose)]
		private void CallSiteBuilt(string serviceType, string callSite, int chunkIndex, int chunkCount) => this.WriteEvent(1, serviceType, callSite, chunkIndex, chunkCount);

		[Event(2, Level = EventLevel.Verbose)]
		public void ServiceResolved(string serviceType) => this.WriteEvent(2, serviceType);

		[Event(3, Level = EventLevel.Verbose)]
		public void ExpressionTreeGenerated(string serviceType, int nodeCount) => this.WriteEvent(3, serviceType, nodeCount);

		[Event(4, Level = EventLevel.Verbose)]
		public void DynamicMethodBuilt(string serviceType, int methodSize) => this.WriteEvent(4, serviceType, methodSize);

		[Event(5, Level = EventLevel.Verbose)]
		public void ScopeDisposed(int serviceProviderHashCode, int scopedServicesResolved, int disposableServices) => this.WriteEvent(5, serviceProviderHashCode, scopedServicesResolved, disposableServices);

		[Event(6, Level = EventLevel.Error)]
		public void ServiceRealizationFailed(string? exceptionMessage) => this.WriteEvent(6, exceptionMessage);

		[NonEvent]
		public void ServiceResolved(ServiceIdentifier serviceType) {
			if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All)) {
				this.ServiceResolved(serviceType.ToString());
			}
		}

		[NonEvent]
		public void CallSiteBuilt(ServiceIdentifier serviceIdentifier, ServiceCallSite callSite) {
			if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All)) {
				var format = CallSiteJsonFormatter.Instance.Format(callSite);
				var chunkCount = format.Length / MaxChunkSize + (format.Length % MaxChunkSize > 0 ? 1 : 0);

				for (var i = 0; i < chunkCount; i++) {
					this.CallSiteBuilt(
						serviceIdentifier.ToString(),
						format.Substring(i * MaxChunkSize, Math.Min(MaxChunkSize, format.Length - i * MaxChunkSize)), i, chunkCount);
				}
			}
		}

		[NonEvent]
		public void DynamicMethodBuilt(ServiceIdentifier serviceType, int methodSize) {
			if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All)) {
				this.DynamicMethodBuilt(serviceType.ToString(), methodSize);
			}
		}

		[NonEvent]
		public void ServiceRealizationFailed(Exception exception) {
			if (this.IsEnabled(EventLevel.Error, EventKeywords.All)) {
				this.ServiceRealizationFailed(exception.ToString());
			}
		}
	}

	internal static class DependencyInjectionEventSourceExtensions {
		// This is an extension method because this assembly is trimmed at a "type granular" level in Blazor,
		// and the whole DependencyInjectionEventSource type can't be trimmed. So extracting this to a separate
		// type allows for the System.Linq.Expressions usage to be trimmed by the ILLinker.
		public static void ExpressionTreeGenerated(this DependencyInjectionEventSource source, in ServiceIdentifier serviceType, Expression expression) {
			if (source.IsEnabled(EventLevel.Verbose, EventKeywords.All)) {
				var visitor = new NodeCountingVisitor();
				visitor.Visit(expression);
				source.ExpressionTreeGenerated(serviceType.ToString(), visitor.NodeCount);
			}
		}

		private sealed class NodeCountingVisitor : ExpressionVisitor {
			public int NodeCount { get; private set; }

			public override Expression Visit(Expression e) {
				base.Visit(e);
				this.NodeCount++;
				return e;
			}
		}
	}
}
