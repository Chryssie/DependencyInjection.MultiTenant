// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection {
	/// <summary>Defines methods to support the comparison of objects for equality by reference.</summary>
	/// <typeparam name="T">The type of objects to compare.</typeparam>
	internal interface IRefEqualityComparer<T> : IEqualityComparer<T> {
		/// <summary>Determines whether the specified objects are equal.</summary>
		/// <param name="x">The first object of type T to compare.</param>
		/// <param name="y">The second object of type T to compare.</param>
		/// <returns><see langword="true"/> if the specified objects are equal; otherwise, <see langword="false"/>.</returns>
		bool Equals([AllowNull] in T x, [AllowNull] in T y);

		/// <summary>Returns a hash code for the specified object.</summary>
		/// <param name="obj">The <see cref="object"/> for which a hash code is to be returned.</param>
		/// <returns>A hash code for the specified object.</returns>
		/// <exception cref="ArgumentNullException">The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is <see langword="null"/>.</exception>
		int GetHashCode([DisallowNull] in T obj);
	}
}
