// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Internal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup {
	internal sealed class MultiTenantProviderEngineScope : IServiceScope, IServiceProvider, IAsyncDisposable, IServiceScopeFactory
    {
        // For testing only
        internal IList<object> Disposables => this._disposables ?? (IList<object>)Array.Empty<object>();

        private bool _disposed;
        private List<object> _disposables;

        public MultiTenantProviderEngineScope(MultiTenantServiceProvider provider, bool isRootScope)
        {
			this.ResolvedServices = new Dictionary<ServiceCacheKey, object>();
			this.RootProvider = provider;
			this.IsRootScope = isRootScope;
        }

        internal Dictionary<ServiceCacheKey, object> ResolvedServices { get; }

        // This lock protects state on the scope, in particular, for the root scope, it protects
        // the list of disposable entries only, since ResolvedServices are cached on CallSites
        // For other scopes, it protects ResolvedServices and the list of disposables
        internal object Sync => this.ResolvedServices;

        public bool IsRootScope { get; }

        internal MultiTenantServiceProvider RootProvider { get; }

        public object GetService(in ServiceIdentifier serviceIdentifier)
        {
            if (this._disposed)
            {
                ThrowHelper.ThrowObjectDisposedException();
            }

            return this.RootProvider.GetService(serviceIdentifier, this);
        }

        public IServiceProvider ServiceProvider => this;

        public IServiceScope CreateScope() => this.RootProvider.CreateScope();

        object IServiceProvider.GetService(Type serviceType) => this.GetService(new (serviceType));

        internal object CaptureDisposable(object service)
        {
            if (ReferenceEquals(this, service) || !(service is IDisposable || service is IAsyncDisposable))
            {
                return service;
            }

            var disposed = false;
            lock (this.Sync)
            {
                if (this._disposed)
                {
                    disposed = true;
                }
                else
                {
					this._disposables ??= new List<object>();

					this._disposables.Add(service);
                }
            }

            // Don't run customer code under the lock
            if (disposed)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else
                {
                    // sync over async, for the rare case that an object only implements IAsyncDisposable and may end up starving the thread pool.
                    Task.Run(() => ((IAsyncDisposable)service).DisposeAsync().AsTask()).GetAwaiter().GetResult();
                }

                ThrowHelper.ThrowObjectDisposedException();
            }

            return service;
        }

        public void Dispose()
        {
            var toDispose = this.BeginDispose();

            if (toDispose != null)
            {
                for (var i = toDispose.Count - 1; i >= 0; i--)
                {
                    if (toDispose[i] is IDisposable disposable) {
                        disposable.Dispose();
                    }
                    else {
                        throw new InvalidOperationException(SR.AsyncDisposableServiceDispose(TypeNameHelper.GetTypeDisplayName(toDispose[i])));
                    }
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            var toDispose = this.BeginDispose();

            if (toDispose != null)
            {
                try
                {
                    for (var i = toDispose.Count - 1; i >= 0; i--)
                    {
                        var disposable = toDispose[i];
                        if (disposable is IAsyncDisposable asyncDisposable)
                        {
                            var vt = asyncDisposable.DisposeAsync();
                            if (!vt.IsCompletedSuccessfully)
                            {
                                return Await(i, vt, toDispose);
                            }

                            // If its a IValueTaskSource backed ValueTask,
                            // inform it its result has been read so it can reset
                            vt.GetAwaiter().GetResult();
                        }
                        else
                        {
                            ((IDisposable)disposable).Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    return new ValueTask(Task.FromException(ex));
                }
            }

            return default;

            static async ValueTask Await(int i, ValueTask vt, List<object> toDispose)
            {
                await vt.ConfigureAwait(false);
                // vt is acting on the disposable at index i,
                // decrement it and move to the next iteration
                i--;

                for (; i >= 0; i--)
                {
                    var disposable = toDispose[i];
                    if (disposable is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        ((IDisposable)disposable).Dispose();
                    }
                }
            }
        }

        private List<object> BeginDispose()
        {
            lock (this.Sync)
            {
                if (this._disposed)
                {
                    return null;
                }

                // Track statistics about the scope (number of disposable objects and number of disposed services)
                DependencyInjectionEventSource.Log.ScopeDisposed(this.RootProvider.GetHashCode(), this.ResolvedServices.Count, this._disposables?.Count ?? 0);

				// We've transitioned to the disposed state, so future calls to
				// CaptureDisposable will immediately dispose the object.
				// No further changes to _state.Disposables, are allowed.
				this._disposed = true;

                // ResolvedServices is never cleared for singletons because there might be a compilation running in background
                // trying to get a cached singleton service. If it doesn't find it
                // it will try to create a new one which will result in an ObjectDisposedException.

                return this._disposables;
            }
        }
	}
}
