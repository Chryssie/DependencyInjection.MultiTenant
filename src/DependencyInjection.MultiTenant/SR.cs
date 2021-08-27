using Microsoft.Extensions.DependencyInjection.Resources;
using System.Globalization;

namespace Microsoft.Extensions.DependencyInjection {
	internal static class SR {
		public static string Format(string format, object? arg0)
			=> string.Format(CultureInfo.CurrentCulture, format, arg0);
		public static string Format(string format, object? arg0, object? arg1)
			=> string.Format(CultureInfo.CurrentCulture, format, arg0, arg1);
		public static string Format(string format, object? arg0, object? arg1, object? arg2)
			=> string.Format(CultureInfo.CurrentCulture, format, arg0, arg1, arg2);
		public static string Format(string format, params object?[] args)
			=> string.Format(CultureInfo.CurrentCulture, format, args);

		public static string AmbiguousConstructorException(object? arg0)
			=> Format(Strings.AmbiguousConstructorException, arg0);
		public static string ArityOfOpenGenericServiceNotEqualArityOfOpenGenericImplementation(object? arg0, object arg1)
			=> Format(Strings.ArityOfOpenGenericServiceNotEqualArityOfOpenGenericImplementation, arg0, arg1);
		public static string AsyncDisposableServiceDispose(object? arg0)
			=> Format(Strings.AsyncDisposableServiceDispose, arg0);
		public static string CallSiteTypeNotSupported(object? arg0)
			=> Format(Strings.CallSiteTypeNotSupported, arg0);
		public static string CannotResolveService(object? arg0, object? arg1)
			=> Format(Strings.CannotResolveService, arg0, arg1);
		public static string CannotResolveTenantService(object? arg0, object? arg1)
			=> Format(Strings.CannotResolveTenantService, arg0, arg1);
		public static string CircularDependencyException(object? arg0)
			=> Format(Strings.CircularDependencyException, arg0);
		public static string ConstantCantBeConvertedToServiceType(object? arg0, object? arg1)
			=> Format(Strings.ConstantCantBeConvertedToServiceType, arg0, arg1);
		public static string DirectScopedResolvedFromRootException(object? arg0, object? arg1)
			=> Format(Strings.DirectScopedResolvedFromRootException, arg0, arg1);
		public static string GetCaptureDisposableNotSupported()
			=> Format(Strings.GetCaptureDisposableNotSupported);
		public static string ImplementationTypeCantBeConvertedToServiceType(object? arg0, object? arg1)
			=> Format(Strings.ImplementationTypeCantBeConvertedToServiceType, arg0, arg1);
		public static string InvalidServiceDescriptor()
			=> Format(Strings.InvalidServiceDescriptor);
		public static string NoConstructorMatch(object? arg0)
			=> Format(Strings.NoConstructorMatch, arg0);
		public static string OpenGenericServiceRequiresOpenGenericImplementation(object? arg0)
			=> Format(Strings.OpenGenericServiceRequiresOpenGenericImplementation, arg0);
		public static string ScopedInSingletonException(object? arg0, object? arg1, object? arg2, object? arg3)
			=> Format(Strings.ScopedInSingletonException, arg0, arg1, arg2, arg3);
		public static string ScopedResolvedFromRootException(object? arg0, object? arg1, object? arg2)
			=> Format(Strings.ScopedResolvedFromRootException, arg0, arg1, arg2);
		public static string ServiceDescriptorNotExist()
			=> Format(Strings.ServiceDescriptorNotExist);
		public static string TypeCannotBeActivated(object? arg0, object? arg1)
			=> Format(Strings.TypeCannotBeActivated, arg0, arg1);
		public static string UnableToActivateTypeException(object? arg0)
			=> Format(Strings.UnableToActivateTypeException, arg0);
	}
}
