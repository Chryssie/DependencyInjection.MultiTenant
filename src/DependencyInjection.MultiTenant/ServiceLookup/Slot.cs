// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal struct Slot : IEquatable<Slot>, IComparable<Slot> {
		public static Slot Default => new(0);

		public int Value { get; }

		public Slot(int value) => this.Value = value;

		public static Slot FromIndexAndCount(int index, int count)
			=> new(index + 1 - count);

		public readonly int ToIndex(int count)
			=> this.Value - 1 + count;
		public readonly Index ToIndex()
			=> Index.FromEnd(this.Value + 1);

		public override readonly bool Equals(object? obj) => obj is Slot other && this.Equals(other);
		public readonly bool Equals(Slot other) => this.Value == other.Value;
		public readonly int CompareTo(Slot other) => this.Value.CompareTo(other.Value);
		public override readonly int GetHashCode() => ~this.Value;

		public override readonly string ToString() => FormattableString.Invariant($"^{this.Value + 1}");

		public static Slot operator +(Slot slot, int value) => new(slot.Value + value);
		public static Slot operator -(Slot slot, int value) => new(slot.Value - value);

		public static Slot operator ++(Slot slot) => slot + 1;
		public static Slot operator --(Slot slot) => slot - 1;
		public static bool operator ==(Slot left, Slot right) => left.Equals(right);
		public static bool operator !=(Slot left, Slot right) => !(left == right);
	}
}
