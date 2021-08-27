// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class ILEmitResolverBuilder : CallSiteVisitor<ILEmitResolverBuilderContext, object> {
		private static readonly MethodInfo ResolvedServicesGetter = typeof(MultiTenantProviderEngineScope).GetProperty(
			nameof(MultiTenantProviderEngineScope.ResolvedServices), BindingFlags.Instance | BindingFlags.NonPublic)!.GetMethod!;

		private static readonly MethodInfo ScopeLockGetter = typeof(MultiTenantProviderEngineScope).GetProperty(
			nameof(MultiTenantProviderEngineScope.Sync), BindingFlags.Instance | BindingFlags.NonPublic)!.GetMethod!;

		private static readonly MethodInfo ScopeIsRootScope = typeof(MultiTenantProviderEngineScope).GetProperty(
			nameof(MultiTenantProviderEngineScope.IsRootScope), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

		private static readonly MethodInfo CallSiteRuntimeResolverResolveMethod = typeof(CallSiteRuntimeResolver).GetMethod(
			nameof(CallSiteRuntimeResolver.Resolve), BindingFlags.Public | BindingFlags.Instance)!;

		private static readonly MethodInfo CallSiteRuntimeResolverInstanceField = typeof(CallSiteRuntimeResolver).GetProperty(
			nameof(CallSiteRuntimeResolver.Instance), BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance)!.GetMethod!;

		private static readonly FieldInfo FactoriesField = typeof(ILEmitResolverBuilderRuntimeContext).GetField(nameof(ILEmitResolverBuilderRuntimeContext.Factories))!;
		private static readonly FieldInfo ConstantsField = typeof(ILEmitResolverBuilderRuntimeContext).GetField(nameof(ILEmitResolverBuilderRuntimeContext.Constants))!;
		private static readonly MethodInfo GetTypeFromHandleMethod = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;

		private static readonly ConstructorInfo CacheKeyCtor = typeof(ServiceCacheKey).GetConstructor(new Type[2] { typeof(ServiceIdentifier), typeof(Slot), })!;
		private static readonly ConstructorInfo SlotCtor = typeof(Slot).GetConstructor(new Type[1] { typeof(Type), })!;
		private static readonly ConstructorInfo ServiceIdentifierCtor = typeof(ServiceCacheKey).GetConstructor(new Type[2] { typeof(Type), typeof(TenantIdentifier), })!;

		private sealed class ILEmitResolverBuilderRuntimeContext {
			public object[] Constants;
			public Func<IServiceProvider, object>[] Factories;
		}

		private struct GeneratedMethod {
			public Func<MultiTenantProviderEngineScope, object> Lambda;

			public ILEmitResolverBuilderRuntimeContext Context;
			public DynamicMethod DynamicMethod;
		}

		private readonly MultiTenantProviderEngineScope _rootScope;

		private readonly ConcurrentDictionary<ServiceCacheKey, GeneratedMethod> _scopeResolverCache;

		private readonly Func<ServiceCacheKey, ServiceCallSite, GeneratedMethod> _buildTypeDelegate;

		public ILEmitResolverBuilder(MultiTenantServiceProvider serviceProvider) {
			this._rootScope = serviceProvider.Root;
			this._scopeResolverCache = new ConcurrentDictionary<ServiceCacheKey, GeneratedMethod>();
			this._buildTypeDelegate = (key, cs) => this.BuildTypeNoCache(cs);
		}

		public Func<MultiTenantProviderEngineScope, object> Build(ServiceCallSite callSite) => this.BuildType(callSite).Lambda;

		private GeneratedMethod BuildType(ServiceCallSite callSite) {
			// Only scope methods are cached
			if (callSite.Cache.Location == CallSiteResultCacheLocation.Scope) {
#if NETSTANDARD2_1
                return this._scopeResolverCache.GetOrAdd(callSite.Cache.Key, this._buildTypeDelegate, callSite);
#else
				return this._scopeResolverCache.GetOrAdd(callSite.Cache.Key, key => this._buildTypeDelegate(key, callSite));
#endif
			}

			return this.BuildTypeNoCache(callSite);
		}

		private GeneratedMethod BuildTypeNoCache(ServiceCallSite callSite) {
			// We need to skip visibility checks because services/constructors might be private
			var dynamicMethod = new DynamicMethod("ResolveService",
				attributes: MethodAttributes.Public | MethodAttributes.Static,
				callingConvention: CallingConventions.Standard,
				returnType: typeof(object),
				parameterTypes: new[] { typeof(ILEmitResolverBuilderRuntimeContext), typeof(MultiTenantProviderEngineScope) },
				owner: this.GetType(),
				skipVisibility: true);

			// In traces we've seen methods range from 100B - 4K sized methods since we've
			// stop trying to inline everything into scoped methods. We'll pay for a couple of resizes
			// so there'll be allocations but we could potentially change ILGenerator to use the array pool
			var ilGenerator = dynamicMethod.GetILGenerator(512);
			var runtimeContext = this.GenerateMethodBody(callSite, ilGenerator);

#if SAVE_ASSEMBLIES
            var assemblyName = "Test" + DateTime.Now.Ticks;

            var fileName = "Test" + DateTime.Now.Ticks;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndSave);
            var module = assembly.DefineDynamicModule(assemblyName, assemblyName+".dll");
            var type = module.DefineType("Resolver");

            var method = type.DefineMethod(
                "ResolveService", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(object),
                new[] { typeof(ILEmitResolverBuilderRuntimeContext), typeof(ServiceProviderEngineScope) });

            GenerateMethodBody(callSite, method.GetILGenerator());
            type.CreateTypeInfo();
            assembly.Save(assemblyName + ".dll");
#endif
			DependencyInjectionEventSource.Log.DynamicMethodBuilt(callSite.ServiceType, ilGenerator.ILOffset);

			return new GeneratedMethod() {
				Lambda = (Func<MultiTenantProviderEngineScope, object>)dynamicMethod.CreateDelegate(typeof(Func<MultiTenantProviderEngineScope, object>), runtimeContext),
				Context = runtimeContext,
				DynamicMethod = dynamicMethod
			};
		}


		protected internal override object? VisitDisposeCache(ServiceCallSite transientCallSite, ILEmitResolverBuilderContext argument) {
			if (transientCallSite.CaptureDisposable) {
				BeginCaptureDisposable(argument);
				_ = this.VisitCallSiteMain(transientCallSite, argument);
				EndCaptureDisposable(argument);
			}
			else {
				_ = this.VisitCallSiteMain(transientCallSite, argument);
			}
			return null;
		}

		protected override internal object? VisitConstructor(ConstructorCallSite constructorCallSite, ILEmitResolverBuilderContext argument) {
			// new T([create arguments])
			foreach (var parameterCallSite in constructorCallSite.ParameterCallSites) {
				_ = this.VisitCallSite(parameterCallSite, argument);
				if (parameterCallSite.ServiceType.Type.IsValueType) {
					argument.Generator.Emit(OpCodes.Unbox_Any, parameterCallSite.ServiceType.Type);
				}
			}

			argument.Generator.Emit(OpCodes.Newobj, constructorCallSite.ConstructorInfo);
			return null;
		}

		protected internal override object? VisitRootCache(ServiceCallSite callSite, ILEmitResolverBuilderContext argument) {
			this.AddConstant(argument, CallSiteRuntimeResolver.Instance.Resolve(callSite, this._rootScope));
			return null;
		}

		protected internal override object? VisitScopeCache(ServiceCallSite scopedCallSite, ILEmitResolverBuilderContext argument) {
			var generatedMethod = this.BuildType(scopedCallSite);

			// Type builder doesn't support invoking dynamic methods, replace them with delegate.Invoke calls
#if SAVE_ASSEMBLIES
            AddConstant(argument, generatedMethod.Lambda);
            // ProviderScope
            argument.Generator.Emit(OpCodes.Ldarg_1);
            argument.Generator.Emit(OpCodes.Call, generatedMethod.Lambda.GetType().GetMethod("Invoke"));
#else
			this.AddConstant(argument, generatedMethod.Context);
			// ProviderScope
			argument.Generator.Emit(OpCodes.Ldarg_1);
			argument.Generator.Emit(OpCodes.Call, generatedMethod.DynamicMethod);
#endif

			return null;
		}

		protected override internal object? VisitConstant(ConstantCallSite constantCallSite, ILEmitResolverBuilderContext argument) {
			this.AddConstant(argument, constantCallSite.DefaultValue);
			return null;
		}

		protected override internal object? VisitServiceProvider(ServiceProviderCallSite serviceProviderCallSite, ILEmitResolverBuilderContext argument) {
			// [return] ProviderScope
			argument.Generator.Emit(OpCodes.Ldarg_1);
			return null;
		}

		protected internal override object? VisitIEnumerable(IEnumerableCallSite enumerableCallSite, ILEmitResolverBuilderContext argument) {
			if (enumerableCallSite.ServiceCallSites.Length == 0) {
				argument.Generator.Emit(OpCodes.Call, ServiceLookupHelpers.GetArrayEmptyMethodInfo(enumerableCallSite.ItemType));
			}
			else {
				// var array = new ItemType[];
				// array[0] = [Create argument0];
				// array[1] = [Create argument1];
				// ...
				argument.Generator.Emit(OpCodes.Ldc_I4, enumerableCallSite.ServiceCallSites.Length);
				argument.Generator.Emit(OpCodes.Newarr, enumerableCallSite.ItemType);
				for (var i = 0; i < enumerableCallSite.ServiceCallSites.Length; i++) {
					// duplicate array
					argument.Generator.Emit(OpCodes.Dup);
					// push index
					argument.Generator.Emit(OpCodes.Ldc_I4, i);
					// create parameter
					var parameterCallSite = enumerableCallSite.ServiceCallSites[i];
					_ = this.VisitCallSite(parameterCallSite, argument);
					if (parameterCallSite.ServiceType.Type.IsValueType) {
						argument.Generator.Emit(OpCodes.Unbox_Any, parameterCallSite.ServiceType.Type);
					}

					// store
					argument.Generator.Emit(OpCodes.Stelem, enumerableCallSite.ItemType);
				}
			}

			return null;
		}

		protected internal override object? VisitFactory(FactoryCallSite factoryCallSite, ILEmitResolverBuilderContext argument) {
			if (argument.Factories == null) {
				argument.Factories = new List<Func<IServiceProvider, object>>();
			}

			// this.Factories[i](ProviderScope)
			argument.Generator.Emit(OpCodes.Ldarg_0);
			argument.Generator.Emit(OpCodes.Ldfld, FactoriesField);

			argument.Generator.Emit(OpCodes.Ldc_I4, argument.Factories.Count);
			argument.Generator.Emit(OpCodes.Ldelem, typeof(Func<IServiceProvider, object>));

			argument.Generator.Emit(OpCodes.Ldarg_1);
			argument.Generator.Emit(OpCodes.Call, ServiceLookupHelpers.InvokeFactoryMethodInfo);

			argument.Factories.Add(factoryCallSite.Factory);
			return null;
		}

		protected internal override object VisitTransposedShared(TransposedSharedCallSite transposedSharedCallSite, ILEmitResolverBuilderContext argument)
			=> this.VisitCallSite(transposedSharedCallSite, argument);

		private void AddConstant<T>(ILEmitResolverBuilderContext argument, T? value) where T : class {
			switch (value) {
				case null: // null
					argument.Generator.Emit(OpCodes.Ldnull);
					break;

				case Type t: // typeof(key.Type)
					argument.Generator.Emit(OpCodes.Ldtoken, t);
					argument.Generator.Emit(OpCodes.Call, GetTypeFromHandleMethod);
					break;

				default: // this.Constants[i]
					argument.Constants ??= new List<object>();

					
					argument.Generator.Emit(OpCodes.Ldarg_0);
					argument.Generator.Emit(OpCodes.Ldfld, ConstantsField);

					argument.Generator.Emit(OpCodes.Ldc_I4, argument.Constants.Count);
					argument.Generator.Emit(OpCodes.Ldelem, typeof(T));
					argument.Constants.Add(value);
					break;
			}
		}

		private void AddCacheKey(ILEmitResolverBuilderContext argument, ServiceCacheKey key) {
			// new ServiceCacheKey(new ServiceIdentifier(typeof(key.ServiceIdentifier.Type), key.ServiceIdentifier.TenantId), new Slot(key.Slot.Value))

			argument.Generator.Emit(OpCodes.Ldtoken, key.ServiceIdentifier.Type);
			argument.Generator.Emit(OpCodes.Call, GetTypeFromHandleMethod);
			argument.Generator.Emit(OpCodes.Ldc_I4, key.Slot.Value);
			argument.Generator.Emit(OpCodes.Newobj, SlotCtor);
			argument.Generator.Emit(OpCodes.Newobj, CacheKeyCtor);
		}

		private void AddSlot(ILEmitResolverBuilderContext argument, Slot slot) {
			// new Slot(slot.Value)
			argument.Generator.Emit(OpCodes.Ldc_I4, slot.Value);
			argument.Generator.Emit(OpCodes.Newobj, SlotCtor);
		}

		private void AddServiceIdentifier(ILEmitResolverBuilderContext argument, ServiceIdentifier identifier) {
			// new ServiceIdentifier(typeof(identifier.Type), identifier.TenantId)

			this.AddConstant(argument, identifier.Type);
			this.AddConstant(argument, identifier.TenantId);
			argument.Generator.Emit(OpCodes.Newobj, ServiceIdentifierCtor);
		}

		private ILEmitResolverBuilderRuntimeContext GenerateMethodBody(ServiceCallSite callSite, ILGenerator generator) {
			var context = new ILEmitResolverBuilderContext() {
				Generator = generator,
				Constants = null,
				Factories = null
			};

			// if (scope.IsRootScope)
			// {
			//     return CallSiteRuntimeResolver.Instance.Resolve(callSite, scope);
			// }
			//  var cacheKey = scopedCallSite.CacheKey;
			//  try
			//  {
			//    var resolvedServices = scope.ResolvedServices;
			//    Monitor.Enter(resolvedServices, out var lockTaken);
			//    if (!resolvedServices.TryGetValue(cacheKey, out value)
			//    {
			//       value = [createvalue];
			//       CaptureDisposable(value);
			//       resolvedServices.Add(cacheKey, value);
			//    }
			// }
			// finally
			// {
			//   if (lockTaken) Monitor.Exit(scope.ResolvedServices);
			// }
			// return value;

			if (callSite.Cache.Location == CallSiteResultCacheLocation.Scope) {
				var cacheKeyLocal = context.Generator.DeclareLocal(typeof(ServiceCacheKey));
				var resolvedServicesLocal = context.Generator.DeclareLocal(typeof(IDictionary<ServiceCacheKey, object>));
				var syncLocal = context.Generator.DeclareLocal(typeof(object));
				var lockTakenLocal = context.Generator.DeclareLocal(typeof(bool));
				var resultLocal = context.Generator.DeclareLocal(typeof(object));

				var skipCreationLabel = context.Generator.DefineLabel();
				var returnLabel = context.Generator.DefineLabel();
				var defaultLabel = context.Generator.DefineLabel();

				// Check if scope IsRootScope
				context.Generator.Emit(OpCodes.Ldarg_1);
				context.Generator.Emit(OpCodes.Callvirt, ScopeIsRootScope);
				context.Generator.Emit(OpCodes.Brfalse_S, defaultLabel);

				context.Generator.Emit(OpCodes.Call, CallSiteRuntimeResolverInstanceField);
				this.AddConstant(context, callSite);
				context.Generator.Emit(OpCodes.Ldarg_1);
				context.Generator.Emit(OpCodes.Callvirt, CallSiteRuntimeResolverResolveMethod);
				context.Generator.Emit(OpCodes.Ret);

				// Generate cache key
				context.Generator.MarkLabel(defaultLabel);
				this.AddCacheKey(context, callSite.Cache.Key);
				// and store to local
				this.Stloc(context.Generator, cacheKeyLocal.LocalIndex);

				context.Generator.BeginExceptionBlock();

				// scope
				context.Generator.Emit(OpCodes.Ldarg_1);
				// .ResolvedServices
				context.Generator.Emit(OpCodes.Callvirt, ResolvedServicesGetter);
				// Store resolved services
				this.Stloc(context.Generator, resolvedServicesLocal.LocalIndex);

				// scope
				context.Generator.Emit(OpCodes.Ldarg_1);
				// .Sync
				context.Generator.Emit(OpCodes.Callvirt, ScopeLockGetter);
				// Store syncLocal
				this.Stloc(context.Generator, syncLocal.LocalIndex);

				// Load syncLocal
				this.Ldloc(context.Generator, syncLocal.LocalIndex);
				// Load address of lockTaken
				context.Generator.Emit(OpCodes.Ldloca_S, lockTakenLocal.LocalIndex);
				// Monitor.Enter
				context.Generator.Emit(OpCodes.Call, ServiceLookupHelpers.MonitorEnterMethodInfo);

				// Load resolved services
				this.Ldloc(context.Generator, resolvedServicesLocal.LocalIndex);
				// Load cache key
				this.Ldloc(context.Generator, cacheKeyLocal.LocalIndex);
				// Load address of result local
				context.Generator.Emit(OpCodes.Ldloca_S, resultLocal.LocalIndex);
				// .TryGetValue
				context.Generator.Emit(OpCodes.Callvirt, ServiceLookupHelpers.TryGetValueMethodInfo);

				// Jump to the end if already in cache
				context.Generator.Emit(OpCodes.Brtrue, skipCreationLabel);

				// Create value
				this.VisitCallSiteMain(callSite, context);
				this.Stloc(context.Generator, resultLocal.LocalIndex);

				if (callSite.CaptureDisposable) {
					BeginCaptureDisposable(context);
					this.Ldloc(context.Generator, resultLocal.LocalIndex);
					EndCaptureDisposable(context);
					// Pop value returned by CaptureDisposable off the stack
					generator.Emit(OpCodes.Pop);
				}

				// load resolvedServices
				this.Ldloc(context.Generator, resolvedServicesLocal.LocalIndex);
				// load cache key
				this.Ldloc(context.Generator, cacheKeyLocal.LocalIndex);
				// load value
				this.Ldloc(context.Generator, resultLocal.LocalIndex);
				// .Add
				context.Generator.Emit(OpCodes.Callvirt, ServiceLookupHelpers.AddMethodInfo);

				context.Generator.MarkLabel(skipCreationLabel);

				context.Generator.BeginFinallyBlock();

				// load lockTaken
				this.Ldloc(context.Generator, lockTakenLocal.LocalIndex);
				// return if not
				context.Generator.Emit(OpCodes.Brfalse, returnLabel);
				// Load syncLocal
				this.Ldloc(context.Generator, syncLocal.LocalIndex);
				// Monitor.Exit
				context.Generator.Emit(OpCodes.Call, ServiceLookupHelpers.MonitorExitMethodInfo);

				context.Generator.MarkLabel(returnLabel);

				context.Generator.EndExceptionBlock();


				// load value
				this.Ldloc(context.Generator, resultLocal.LocalIndex);
				// return
				context.Generator.Emit(OpCodes.Ret);
			}
			else {
				this.VisitCallSite(callSite, context);
				// return
				context.Generator.Emit(OpCodes.Ret);
			}

			return new ILEmitResolverBuilderRuntimeContext {
				Constants = context.Constants?.ToArray(),
				Factories = context.Factories?.ToArray()
			};
		}

		private static void BeginCaptureDisposable(ILEmitResolverBuilderContext argument) => argument.Generator.Emit(OpCodes.Ldarg_1);

		private static void EndCaptureDisposable(ILEmitResolverBuilderContext argument) =>
			// Call CaptureDisposabl we expect calee and arguments to be on the stackcontext.Generator.BeginExceptionBlock
			argument.Generator.Emit(OpCodes.Callvirt, ServiceLookupHelpers.CaptureDisposableMethodInfo);

		private void Ldloc(ILGenerator generator, int index) {
			switch (index) {
				case 0:
					generator.Emit(OpCodes.Ldloc_0);
					return;
				case 1:
					generator.Emit(OpCodes.Ldloc_1);
					return;
				case 2:
					generator.Emit(OpCodes.Ldloc_2);
					return;
				case 3:
					generator.Emit(OpCodes.Ldloc_3);
					return;
			}

			if (index < byte.MaxValue) {
				generator.Emit(OpCodes.Ldloc_S, (byte)index);
				return;
			}

			generator.Emit(OpCodes.Ldloc, index);
		}

		private void Stloc(ILGenerator generator, int index) {
			switch (index) {
				case 0:
					generator.Emit(OpCodes.Stloc_0);
					return;
				case 1:
					generator.Emit(OpCodes.Stloc_1);
					return;
				case 2:
					generator.Emit(OpCodes.Stloc_2);
					return;
				case 3:
					generator.Emit(OpCodes.Stloc_3);
					return;
			}

			if (index < byte.MaxValue) {
				generator.Emit(OpCodes.Stloc_S, (byte)index);
				return;
			}

			generator.Emit(OpCodes.Stloc, index);
		}
	}
}
