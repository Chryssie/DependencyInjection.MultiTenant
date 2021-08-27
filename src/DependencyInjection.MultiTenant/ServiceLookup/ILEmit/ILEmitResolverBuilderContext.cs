// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal interface IConstantILFactory {
		Action<ILGenerator> GetConstantGenerator<T>(T value) where T : class;
	}

	internal interface ILocalDefaultManager {
		LocalBuilder GetOrDeclareLocal(Type type);

	}

	internal abstract class LocalDefaultManager : ILocalDefaultManager {
		public abstract LocalBuilder GetOrDeclareLocal(Type type);

		[return: NotNullIfNotNull("generator")]
		public static ILocalDefaultManager? Get(ILGenerator? generator) => generator switch {
			null => null,
			ILocalDefaultManager ldm => ldm,
			_ => DefaultLocalDefaultManager.GetOrCreateDefaultLocalDefaultManager(generator),
		};

		private sealed class DefaultLocalDefaultManager : LocalDefaultManager {
			private static readonly ConditionalWeakTable<ILGenerator, DefaultLocalDefaultManager> generators = new();

			public static DefaultLocalDefaultManager GetOrCreateDefaultLocalDefaultManager(ILGenerator generator) => generators.GetValue(generator, static g => new(g));

			private readonly ILGenerator generator;
			private readonly Dictionary<Type, LocalBuilder> builders = new();

			public DefaultLocalDefaultManager(ILGenerator generator) => this.generator = generator;

			public override LocalBuilder GetOrDeclareLocal(Type type) {
				lock (this.builders) {
					if (builders.TryGetValue(type, out var builder))
						return builder;

					builder = this.generator.DeclareLocal(type);

					builders.Add(type, builder);

					return builder;
				}
			}
		}
	}

	internal static class ILGeneratorExtensions {
		private static RuntimeMethodHandle? nullableCtor;

		public static LocalBuilder GetOrDefaultLocalDefault(this ILGenerator generator, Type type) {
			return LocalDefaultManager.Get(generator).GetOrDeclareLocal(type);
		}

		private static ConstructorInfo NullableCtor<T>() where T : struct
			=> (ConstructorInfo)MethodBase.GetMethodFromHandle(nullableCtor ??= typeof(Nullable<>).GetConstructor(typeof(Nullable<>).GetGenericArguments())!.MethodHandle, typeof(T?).TypeHandle)!;

		private static void EmitNewNullable<T>(this ILGenerator generator) where T : struct
			=> generator.Emit(OpCodes.Newobj, NullableCtor<T>());
		public static void EmitLdc(this ILGenerator generator, bool value) => generator.Emit(value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
		public static void EmitLdc(this ILGenerator generator, bool? value) {

		}
		public static void EmitLdc(this ILGenerator generator, int value) {
			switch (value) {
				case -1: generator.Emit(OpCodes.Ldc_I4_M1); break;
				case 0: generator.Emit(OpCodes.Ldc_I4_0); break;
				case 1: generator.Emit(OpCodes.Ldc_I4_1); break;
				case 2: generator.Emit(OpCodes.Ldc_I4_2); break;
				case 3: generator.Emit(OpCodes.Ldc_I4_3); break;
				case 4: generator.Emit(OpCodes.Ldc_I4_4); break;
				case 5: generator.Emit(OpCodes.Ldc_I4_5); break;
				case 6: generator.Emit(OpCodes.Ldc_I4_6); break;
				case 7: generator.Emit(OpCodes.Ldc_I4_7); break;
				case 8: generator.Emit(OpCodes.Ldc_I4_8); break;

				case >= sbyte.MinValue and <= sbyte.MaxValue: generator.Emit(OpCodes.Ldc_I4_S, (sbyte)value); break;
				default: generator.Emit(OpCodes.Ldc_I4, value); break;
			}
		}

		public static void EmitLdc(this ILGenerator generator, long value) => generator.Emit(OpCodes.Ldc_I8, value);
		public static void EmitLdc(this ILGenerator generator, float value) => generator.Emit(OpCodes.Ldc_R4, value);
		public static void EmitLdc(this ILGenerator generator, double value) => generator.Emit(OpCodes.Ldc_R8, value);


		public static void EmitLdc(this ILGenerator generator, uint value) {
			generator.EmitLdc(unchecked((int)value));
			generator.Emit(OpCodes.Conv_U4);
		}

		public static void EmitLdc(this ILGenerator generator, ulong value) {
			generator.EmitLdc(unchecked((int)value));
			generator.Emit(OpCodes.Conv_U8);
		}
	}

	internal static class ConstantILFactory {
		private static readonly Action<ILGenerator> EmitNullAction = g => g.Emit(OpCodes.Ldnull);
		private static RuntimeMethodHandle? nullableCtor;

		private static ConstructorInfo NullableCtor<T>() where T : struct
			=> (ConstructorInfo)MethodBase.GetMethodFromHandle(nullableCtor ??= typeof(Nullable<>).GetConstructor(typeof(Nullable<>).GetGenericArguments())!.MethodHandle, typeof(T?).TypeHandle)!;

		private static class StructHelpers<T> where T : struct {
			private static readonly ConstructorInfo nullableCtor = typeof(T?).GetConstructor(new Type[1] { typeof(T), })!;

			public static readonly Action<ILGenerator> NewNullable = g => g.Emit(OpCodes.Newobj, StructHelpers<T>.nullableCtor);
			public static readonly Action<ILGenerator> Box = g => g.Emit(OpCodes.Box, typeof(T));
			public static readonly Action<ILGenerator> Unbox = g => g.Emit(OpCodes.Unbox, typeof(T));

		}

		public static Action<ILGenerator>? TryCreateSimpleConstantEmitILAction<T>(T value) {
			return value switch {
				null when typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>) => g => g.Emit(OpCodes.Newobj),

				int n => FixValueType<int>(g => g.EmitLdc(n)),
				long n => FixValueType<long>(g => g.EmitLdc(n)),

				uint n => FixValueType<uint>(g => g.EmitLdc(n)),
				ulong n => FixValueType<ulong>(g => g.EmitLdc(n)),

				float n => FixValueType<float>(g => g.EmitLdc(n)),
				double n => FixValueType<double>(g => g.EmitLdc(n)),

				bool n => FixValueType<bool>(g => g.EmitLdc(n)),

				_ => (Action<ILGenerator>?)null,
			};

			static Action<ILGenerator> FixValueType<TValue>(Action<ILGenerator> action) where TValue : struct {
				if (typeof(T) == typeof(TValue?))
					return action + StructHelpers<TValue>.NewNullable;

				if (!typeof(T).IsValueType)
					return action + StructHelpers<TValue>.Box;

				return action;
			}
		}
	}
	internal interface IILEmitResolverBuilderContext : IConstantILFactory {
		ILGenerator Generator { get; }
	}
	internal sealed class ILEmitResolverBuilderContext {
		public ILGenerator Generator { get; set; }
		public List<object> Constants { get; set; }
		public List<Func<IServiceProvider, object>> Factories { get; set; }
	}
	internal static class ServiceCacheKeyILGeneratorExtensions {
		private static readonly ConstructorInfo CacheKeyCtor = typeof(ServiceCacheKey).GetConstructor(new Type[2] { typeof(ServiceIdentifier), typeof(Slot), })!;

		public static void EmitNewServiceCacheKey(this ILGenerator generator) {

		}
	}
	internal static class SlotILGeneratorExtensions {
		private static readonly ConstructorInfo SlotCtor = typeof(Slot).GetConstructor(new Type[1] { typeof(int), })!;

		private static void EmitNewSlot<TContext>(this TContext context, int value) where TContext : class, IILEmitResolverBuilderContext => EmitNewSlotCore(ref context, value);
		private static void EmitNewSlot<TContext>(this ref TContext context, int value) where TContext : struct, IILEmitResolverBuilderContext => EmitNewSlotCore(ref context, value);
		private static void EmitNewSlotCore<TContext>(ref TContext context, int value) where TContext : IILEmitResolverBuilderContext {
			// new Slot(value)

			context.Generator.EmitLdc(value);
			context.Generator.Emit(OpCodes.Newobj, SlotCtor);
		}
	}
	internal static class ServiceIdentifierILGeneratorExtensions {
		private static readonly ConstructorInfo Ctor = typeof(ServiceCacheKey).GetConstructor(new Type[2] { typeof(Type), typeof(TenantIdentifier), })!;

		private static void EmitNewServiceIdentifier<TContext>(this TContext context, Type type, TenantIdentifier tenantIdentifier) where TContext : class, IILEmitResolverBuilderContext => EmitNewServiceIdentifierCore(ref context, type, tenantIdentifier);
		private static void EmitNewServiceIdentifier<TContext>(this ref TContext context, Type type, TenantIdentifier tenantIdentifier) where TContext : struct, IILEmitResolverBuilderContext => EmitNewServiceIdentifierCore(ref context, type, tenantIdentifier);
		private static void EmitNewServiceIdentifierCore<TContext>(ref TContext context, Type type, TenantIdentifier tenantIdentifier) where TContext : IILEmitResolverBuilderContext {
			// new ServiceIdentifier(typeof(identifier.Type), identifier.TenantId)

			context.GetConstantGenerator(type).Invoke(context.Generator);
			context.GetConstantGenerator(tenantIdentifier).Invoke(context.Generator);
			context.Generator.Emit(OpCodes.Newobj, Ctor);
		}
	}
}
