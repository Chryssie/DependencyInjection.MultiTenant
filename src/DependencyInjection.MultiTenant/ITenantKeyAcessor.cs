namespace Microsoft.Extensions.DependencyInjection {
	public interface ITenantKeyAcessor<TTenantKey> {
		TTenantKey TenantKey { get; }
	}
}
