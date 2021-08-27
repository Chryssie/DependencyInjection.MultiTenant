// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETSTANDARD2_0
using System.Collections.Generic;

namespace System.Collections.Generic {
	internal static class KeyValuePairExtensions{
		public static void Deconstruct<TKey,TValue>(this in KeyValuePair<TKey,TValue> pair, out TKey key, out TValue value){
			key = pair.Key;
			value = pair.Value;
		}
	}
}
#endif
