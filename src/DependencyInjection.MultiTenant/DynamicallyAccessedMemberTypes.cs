namespace System.Diagnostics.CodeAnalysis {
#if !NET5_0_OR_GREATER
	/// <summary>Indicates that certain members on a specified <see cref="System.Type"/> are accessed dynamically, for example, through <see cref="System.Reflection"/>.</summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
	internal sealed class DynamicallyAccessedMembersAttribute : Attribute {
		/// <summary>Initializes a new instance of the System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute class with the specified member types.</summary>
		/// <param name="memberTypes">The types of the dynamically accessed members.</param>
		public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) => this.MemberTypes = memberTypes;

		/// <summary>Gets the System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes that specifies the type of dynamically accessed members.</summary>
		public DynamicallyAccessedMemberTypes MemberTypes { get; }
	}

	/// <summary>Specifies the types of members that are dynamically accessed. This enumeration has a System.FlagsAttribute attribute that allows a bitwise combination of its member values.</summary>
	[Flags]
	internal enum DynamicallyAccessedMemberTypes {
		/// <summary>Specifies all members.</summary>
		All = -1,
		/// <summary>Specifies no members.</summary>
		None = 0,
		/// <summary>Specifies the default, parameterless public constructor.</summary>
		PublicParameterlessConstructor = 1,
		/// <summary>Specifies all public constructors.</summary>
		PublicConstructors = 3,
		/// <summary>Specifies all non-public constructors.</summary>
		NonPublicConstructors = 4,
		/// <summary>Specifies all public methods.</summary>
		PublicMethods = 8,
		/// <summary>Specifies all non-public methods.</summary>
		NonPublicMethods = 16,
		/// <summary>Specifies all public fields.</summary>
		PublicFields = 32,
		/// <summary>Specifies all non-public fields.</summary>
		NonPublicFields = 64,
		/// <summary>Specifies all public nested types.</summary>
		PublicNestedTypes = 128,
		/// <summary>Specifies all non-public nested types.</summary>
		NonPublicNestedTypes = 256,
		/// <summary>Specifies all public properties.</summary>
		PublicProperties = 512,
		/// <summary>Specifies all non-public properties.</summary>
		NonPublicProperties = 1024,
		/// <summary>Specifies all public events.</summary>
		PublicEvents = 2048,
		/// <summary>Specifies all non-public events.</summary>
		NonPublicEvents = 4096
	}

#endif
}
