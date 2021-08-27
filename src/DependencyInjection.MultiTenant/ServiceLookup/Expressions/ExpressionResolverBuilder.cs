// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup
{
    internal sealed class ExpressionResolverBuilder : CallSiteVisitor<object, Expression>
    {
        private static readonly ParameterExpression ScopeParameter = Expression.Parameter(typeof(MultiTenantProviderEngineScope));

        private static readonly ParameterExpression ResolvedServices = Expression.Variable(typeof(IDictionary<ServiceCacheKey, object>), ScopeParameter.Name + "resolvedServices");
        private static readonly ParameterExpression Sync = Expression.Variable(typeof(object), ScopeParameter.Name + "sync");
        private static readonly BinaryExpression ResolvedServicesVariableAssignment =
            Expression.Assign(ResolvedServices,
                Expression.Property(
                    ScopeParameter,
                    typeof(MultiTenantProviderEngineScope).GetProperty(nameof(MultiTenantProviderEngineScope.ResolvedServices), BindingFlags.Instance | BindingFlags.NonPublic)!));

        private static readonly BinaryExpression SyncVariableAssignment =
            Expression.Assign(Sync,
                Expression.Property(
                    ScopeParameter,
                    typeof(MultiTenantProviderEngineScope).GetProperty(nameof(MultiTenantProviderEngineScope.Sync), BindingFlags.Instance | BindingFlags.NonPublic)!));

        private static readonly ParameterExpression CaptureDisposableParameter = Expression.Parameter(typeof(object));
        private static readonly LambdaExpression CaptureDisposable = Expression.Lambda(
                    Expression.Call(ScopeParameter, ServiceLookupHelpers.CaptureDisposableMethodInfo, CaptureDisposableParameter),
                    CaptureDisposableParameter);

        private static readonly ConstantExpression CallSiteRuntimeResolverInstanceExpression = Expression.Constant(
            CallSiteRuntimeResolver.Instance,
            typeof(CallSiteRuntimeResolver));

        private readonly MultiTenantProviderEngineScope _rootScope;

        private readonly ConcurrentDictionary<ServiceCacheKey, Func<MultiTenantProviderEngineScope, object>> _scopeResolverCache;

        private readonly Func<ServiceCacheKey, ServiceCallSite, Func<MultiTenantProviderEngineScope, object>> _buildTypeDelegate;

        public ExpressionResolverBuilder(MultiTenantServiceProvider serviceProvider)
        {
			this._rootScope = serviceProvider.Root;
			this._scopeResolverCache = new ConcurrentDictionary<ServiceCacheKey, Func<MultiTenantProviderEngineScope, object>>();
			this._buildTypeDelegate = (key, cs) => this.BuildNoCache(cs);
        }

        public Func<MultiTenantProviderEngineScope, object> Build(ServiceCallSite callSite)
        {
            // Only scope methods are cached
            if (callSite.Cache.Location == CallSiteResultCacheLocation.Scope)
            {
#if NETSTANDARD2_1
                return this._scopeResolverCache.GetOrAdd(callSite.Cache.Key, this._buildTypeDelegate, callSite);
#else
                return this._scopeResolverCache.GetOrAdd(callSite.Cache.Key, key => this._buildTypeDelegate(key, callSite));
#endif
            }

            return this.BuildNoCache(callSite);
        }

        public Func<MultiTenantProviderEngineScope, object> BuildNoCache(ServiceCallSite callSite)
        {
            var expression = this.BuildExpression(callSite);
            DependencyInjectionEventSource.Log.ExpressionTreeGenerated(callSite.ServiceType, expression);
            return expression.Compile();
        }

        private Expression<Func<MultiTenantProviderEngineScope, object>> BuildExpression(ServiceCallSite callSite)
        {
            if (callSite.Cache.Location == CallSiteResultCacheLocation.Scope)
            {
                return Expression.Lambda<Func<MultiTenantProviderEngineScope, object>>(
                    Expression.Block(
                        new[] { ResolvedServices, Sync },
                        ResolvedServicesVariableAssignment,
                        SyncVariableAssignment,
						this.BuildScopedExpression(callSite)),
                    ScopeParameter);
            }

            return Expression.Lambda<Func<MultiTenantProviderEngineScope, object>>(
                Convert(this.VisitCallSite(callSite, null), typeof(object), forceValueTypeConversion: true),
                ScopeParameter);
        }

        protected internal override Expression VisitRootCache(ServiceCallSite singletonCallSite, object context) => Expression.Constant(CallSiteRuntimeResolver.Instance.Resolve(singletonCallSite, this._rootScope));

        protected internal override Expression VisitConstant(ConstantCallSite constantCallSite, object context) => Expression.Constant(constantCallSite.DefaultValue);

        protected internal override Expression VisitServiceProvider(ServiceProviderCallSite serviceProviderCallSite, object context) => ScopeParameter;

        protected internal override Expression VisitFactory(FactoryCallSite factoryCallSite, object context) => Expression.Invoke(Expression.Constant(factoryCallSite.Factory), ScopeParameter);

        protected internal override Expression VisitIEnumerable(IEnumerableCallSite callSite, object context)
        {
            if (callSite.ServiceCallSites.Length == 0)
            {
                return Expression.Constant(
                    ServiceLookupHelpers.GetArrayEmptyMethodInfo(callSite.ItemType)
                    .Invoke(obj: null, parameters: Array.Empty<object>()));
            }

            return Expression.NewArrayInit(
                callSite.ItemType,
                callSite.ServiceCallSites.Select(cs =>
                    Convert(
						this.VisitCallSite(cs, context),
                        callSite.ItemType)));
        }

		protected internal override Expression VisitTransposedShared(TransposedSharedCallSite transposedSharedCallSite, object context) {
            return this.VisitCallSite(transposedSharedCallSite.ServiceCallSite, context);
		}

        protected internal override Expression VisitDisposeCache(ServiceCallSite callSite, object context) =>
			// Elide calls to GetCaptureDisposable if the implementation type isn't disposable
			this.TryCaptureDisposable(
				callSite,
				ScopeParameter,
				this.VisitCallSiteMain(callSite, context));

		private Expression TryCaptureDisposable(ServiceCallSite callSite, ParameterExpression scope, Expression service)
        {
            if (!callSite.CaptureDisposable)
            {
                return service;
            }

            return Expression.Invoke(this.GetCaptureDisposable(scope), service);
        }

        protected internal override Expression VisitConstructor(ConstructorCallSite callSite, object context)
        {
            var parameters = callSite.ConstructorInfo.GetParameters();
            Expression[] parameterExpressions;
            if (callSite.ParameterCallSites.Length == 0)
            {
                parameterExpressions = Array.Empty<Expression>();
            }
            else
            {
                parameterExpressions = new Expression[callSite.ParameterCallSites.Length];
                for (var i = 0; i < parameterExpressions.Length; i++)
                {
                    parameterExpressions[i] = Convert(this.VisitCallSite(callSite.ParameterCallSites[i], context), parameters[i].ParameterType);
                }
            }
            return Expression.New(callSite.ConstructorInfo, parameterExpressions);
        }

        private static Expression Convert(Expression expression, Type type, bool forceValueTypeConversion = false)
        {
            // Don't convert if the expression is already assignable
            if (type.IsAssignableFrom(expression.Type)
                && (!expression.Type.IsValueType || !forceValueTypeConversion))
            {
                return expression;
            }

            return Expression.Convert(expression, type);
        }

        protected internal override Expression VisitScopeCache(ServiceCallSite callSite, object context)
        {
            var lambda = this.Build(callSite);
            return Expression.Invoke(Expression.Constant(lambda), ScopeParameter);
        }

        // Move off the main stack
        private Expression BuildScopedExpression(ServiceCallSite callSite)
        {
            var callSiteExpression = Expression.Constant(
                callSite,
                typeof(ServiceCallSite));

            // We want to directly use the callsite value if it's set and the scope is the root scope.
            // We've already called into the RuntimeResolver and pre-computed any singletons or root scope
            // Avoid the compilation for singletons (or promoted singletons)
            var resolveRootScopeExpression = Expression.Call(
                CallSiteRuntimeResolverInstanceExpression,
                ServiceLookupHelpers.ResolveCallSiteAndScopeMethodInfo,
                callSiteExpression,
                ScopeParameter);

            var keyExpression = Expression.Constant(
                callSite.Cache.Key,
                typeof(ServiceCacheKey));

            var resolvedVariable = Expression.Variable(typeof(object), "resolved");

            var resolvedServices = ResolvedServices;

            var tryGetValueExpression = Expression.Call(
                resolvedServices,
                ServiceLookupHelpers.TryGetValueMethodInfo,
                keyExpression,
                resolvedVariable);

            var captureDisposible = this.TryCaptureDisposable(callSite, ScopeParameter, this.VisitCallSiteMain(callSite, null));

            var assignExpression = Expression.Assign(
                resolvedVariable,
                captureDisposible);

            var addValueExpression = Expression.Call(
                resolvedServices,
                ServiceLookupHelpers.AddMethodInfo,
                keyExpression,
                resolvedVariable);

            var blockExpression = Expression.Block(
                typeof(object),
                new[]
                {
                    resolvedVariable
                },
                Expression.IfThen(
                    Expression.Not(tryGetValueExpression),
                    Expression.Block(
                        assignExpression,
                        addValueExpression)),
                resolvedVariable);


            // The C# compiler would copy the lock object to guard against mutation.
            // We don't, since we know the lock object is readonly.
            var lockWasTaken = Expression.Variable(typeof(bool), "lockWasTaken");
            var sync = Sync;

            var monitorEnter = Expression.Call(ServiceLookupHelpers.MonitorEnterMethodInfo, sync, lockWasTaken);
            var monitorExit = Expression.Call(ServiceLookupHelpers.MonitorExitMethodInfo, sync);

            var tryBody = Expression.Block(monitorEnter, blockExpression);
            var finallyBody = Expression.IfThen(lockWasTaken, monitorExit);

            return Expression.Condition(
                    Expression.Property(
                        ScopeParameter,
                        typeof(MultiTenantProviderEngineScope)
                            .GetProperty(nameof(MultiTenantProviderEngineScope.IsRootScope), BindingFlags.Instance | BindingFlags.Public)),
                    resolveRootScopeExpression,
                    Expression.Block(
                        typeof(object),
                        new[] { lockWasTaken },
                        Expression.TryFinally(tryBody, finallyBody))
                );
        }

        public Expression GetCaptureDisposable(ParameterExpression scope)
        {
            if (scope != ScopeParameter)
            {
                throw new NotSupportedException(SR.GetCaptureDisposableNotSupported());
            }
            return CaptureDisposable;
        }
    }
}
