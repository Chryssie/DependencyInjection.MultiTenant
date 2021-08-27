// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection {
	internal readonly struct DefaultEqualityComparer<T> : IEqualityComparer<T>, IEquatable<DefaultEqualityComparer<T>> {
		private static readonly int hashCode = typeof(DefaultEqualityComparer<T>).GetHashCode();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(T? x, T? y) => EqualityComparer<T>.Default.Equals(x, y);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetHashCode([DisallowNull] T obj) => EqualityComparer<T>.Default.GetHashCode(obj);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object? obj) => obj is DefaultEqualityComparer<T>;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool IEquatable<DefaultEqualityComparer<T>>.Equals(DefaultEqualityComparer<T> other) => true;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => hashCode;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string? ToString() => EqualityComparer<T>.Default.ToString();
	}
}
