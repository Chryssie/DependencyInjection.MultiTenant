// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Internal;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class CallSiteChain {
		private readonly Dictionary<ServiceIdentifier, ChainItemInfo> callSiteChain = new();

		public CallSiteChain()
			=> this.callSiteChain = new Dictionary<ServiceIdentifier, ChainItemInfo>();

		public void CheckCircularDependency(ServiceIdentifier serviceIdentifier) {
			if (this.callSiteChain.ContainsKey(serviceIdentifier))
				throw new InvalidOperationException(this.CreateCircularDependencyExceptionMessage(serviceIdentifier));
		}

		public void Remove(ServiceIdentifier serviceIdentifier) => this.callSiteChain.Remove(serviceIdentifier);

		public void Add(ServiceIdentifier serviceIdentifier) => this.Add(serviceIdentifier, new ServiceIdentifier(serviceIdentifier.TenantId));
		public void Add(ServiceIdentifier serviceIdentifier, Type implementationType) => this.Add(serviceIdentifier, new ServiceIdentifier(implementationType, serviceIdentifier.TenantId));
		public void Add(ServiceIdentifier serviceIdentifier, ServiceIdentifier implementation) => this.callSiteChain[serviceIdentifier] = new ChainItemInfo(this.callSiteChain.Count, implementation);

		private string CreateCircularDependencyExceptionMessage(ServiceIdentifier serviceIdentifier) {
			var messageBuilder = new StringBuilder();
			_ = messageBuilder.Append(SR.CircularDependencyException(serviceIdentifier.ToString()));
			_ = messageBuilder.AppendLine();

			this.AppendResolutionPath(messageBuilder, serviceIdentifier);

			return messageBuilder.ToString();
		}

		private SortedChainInfoCollection GetSortedChain() => new(this.callSiteChain);

		private void AppendResolutionPath(StringBuilder builder, ServiceIdentifier currentlyResolving) {
			foreach (var (service, implementation) in this.GetSortedChain()) {
				_ = (service.Type == implementation.Type || implementation.Type is null) && service.TenantId == implementation.TenantId
					? builder.Append(service) : builder.AppendFormat("{0}({1})", service, implementation);

				_ = builder.Append(" -> ");
			}

			_ = builder.Append(TypeNameHelper.GetTypeDisplayName(currentlyResolving));
		}

		private readonly record struct ChainItemInfo(int Order, ServiceIdentifier ImplementationType);

		private readonly record struct SortedChainInfo(ServiceIdentifier Service, ServiceIdentifier Implementation);

		private struct SortedChainInfoCollection : IReadOnlyList<SortedChainInfo> {
			private readonly KeyValuePair<ServiceIdentifier, ChainItemInfo>[] sorted;

			public SortedChainInfoCollection(Dictionary<ServiceIdentifier, ChainItemInfo> callSiteChain) {
				this.sorted = callSiteChain.ToArray();
				Array.Sort(this.sorted, static (a, b) => a.Value.Order.CompareTo(b.Value.Order));
			}

			public readonly SortedChainInfo this[int index] => Get(in this.sorted[index]);

			private static SortedChainInfo Get(in KeyValuePair<ServiceIdentifier, ChainItemInfo> pair) => new(pair.Key, pair.Value.ImplementationType);

			public readonly int Count => this.sorted.Length;

			public IEnumerator<SortedChainInfo> GetEnumerator() {
				for (var i = 0; i < this.sorted.Length; i++) yield return Get(in this.sorted[i]);
			}

			IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
		}
	}

}
