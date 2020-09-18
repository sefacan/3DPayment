using Microsoft.Extensions.DependencyInjection;

namespace ThreeDPayment
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPaymentServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddHttpContextAccessor();
            services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

            return services;
        }
    }
}