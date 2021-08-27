using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Xunit;

namespace Microsoft.Extensions.DependencyInjection.MultiTenant.Tests {
	public class ServiceResolutionTests {
		[Fact]
		public void TestMultiTenantRegistration() {
			var services = new ServiceCollection {
				ServiceDescriptor.Singleton(typeof(MyCommonService), typeof(MyCommonService)),
				MultiTenantServiceDescriptor.Singleton(typeof(MyService), typeof(MyService)),
				MultiTenantServiceDescriptor.Transient(typeof(MyWrappingService), typeof(MyWrappingService)),
			};


			using var provider = services.BuildMultiTenantServiceProvider<string>();

			var myService1 = (MyService)provider.GetService("Hello", typeof(MyService));
			var myService2 = (MyService)provider.GetService("World", typeof(MyService));
			var myService3 = (MyService)provider.GetService("Hello", typeof(MyService));

			var myWrappingService1 = (MyWrappingService)provider.GetService("Hello", typeof(MyWrappingService));
			var myWrappingService2 = (MyWrappingService)provider.GetService("World", typeof(MyWrappingService));
			var myWrappingService3 = (MyWrappingService)provider.GetService("Hello", typeof(MyWrappingService));

			Assert.NotNull(myService1);
			Assert.NotNull(myService2);
			Assert.NotNull(myService3);

			Assert.Equal(myService1.Id, myService3.Id);
			Assert.NotEqual(myService2.Id, myService1.Id);
			Assert.NotEqual(myService2.Id, myService3.Id);
		}
	}

	public class MyCommonService {
		private static uint lastId = 0;

		public uint Id { get; } = Interlocked.Increment(ref lastId);

		public MyCommonService() { }
	}
	public class MyService {
		private static uint lastId = 0;
		private readonly ITenantKeyAcessor<string> tenantKeyAcessor;

		public uint Id { get; } = Interlocked.Increment(ref lastId);
		public string TeneantKey => this.tenantKeyAcessor.TenantKey;

		public MyService(ITenantKeyAcessor<string> tenantKeyAcessor) {
			this.tenantKeyAcessor = tenantKeyAcessor;
		}
	}
	public class MyWrappingService {

		public MyWrappingService(MyService myService, MyCommonService myCommonService) {
			this.MyService = myService;
			this.MyCommonService = myCommonService;
		}

		public MyService MyService { get; }
		public MyCommonService MyCommonService { get; }
	}
}
