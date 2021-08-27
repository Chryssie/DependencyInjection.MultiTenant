// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	using static MethodImplOptions;

	internal sealed class TenantKeyFactory<T> where T : notnull {
		private readonly ConcurrentDictionary<CacheKey, InternalTenantIdentifier<T>> internalKeyLookup;

		public TenantKeyFactory(IEqualityComparer<T>? comparer) => this.internalKeyLookup = new(CacheKey.CreateEqualityComparer(comparer));

		public TenantIdentifier this[T value] { [MethodImpl(AggressiveInlining)] get => this.internalKeyLookup.GetOrAdd(new(value), static v => new InternalTenantIdentifier<T>(v.value)); }

		private struct CacheKey : IEquatable<CacheKey> {
			public static IEqualityComparer<CacheKey> CreateEqualityComparer(IEqualityComparer<T>? comparer)
				=> comparer is null || ReferenceEquals(comparer, EqualityComparer<T>.Default) || ReferenceEquals(comparer, StringComparer.Ordinal) ? EqualityComparer<CacheKey>.Default : new EqualityComparer(comparer);

			internal readonly T value;

			[MethodImpl(AggressiveInlining)]
			public CacheKey(T value) => this.value = value;

			[MethodImpl(AggressiveInlining)]
			public override readonly bool Equals(object? obj) => obj is CacheKey other && this.Equals(other);
			[MethodImpl(AggressiveInlining)]
			public readonly bool Equals(CacheKey other) => EqualityComparer<T>.Default.Equals(this.value, other.value);
			[MethodImpl(AggressiveInlining)]
			public override readonly int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(value);

			private sealed class EqualityComparer : IEqualityComparer<CacheKey> {
				private readonly IEqualityComparer<T> comparer;

				public EqualityComparer(IEqualityComparer<T> comparer) => this.comparer = comparer;

				[MethodImpl(AggressiveInlining)]
				public bool Equals(CacheKey x, CacheKey y) => this.comparer.Equals(x.value, y.value);
				[MethodImpl(AggressiveInlining)]
				public int GetHashCode([DisallowNull] CacheKey obj) => this.comparer.GetHashCode(obj.value);
			}
		}
	}
}
