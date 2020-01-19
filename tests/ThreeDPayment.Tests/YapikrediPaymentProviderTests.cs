using Microsoft.Extensions.DependencyInjection;
using System;
using ThreeDPayment.Payment;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class YapikrediPaymentProviderTests
    {
        [Theory]
        [InlineData(10)]
        public void PaymentProviderFactory_CreateYapikrediPaymentProvider(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            Assert.IsType<YapikrediPaymentProvider>(provider);
        }

        [Fact]
        public void Yapikredi_GetPaymentParameterResult_ThrowsNotImpl()
        {
            var provider = new YapikrediPaymentProvider();
            Assert.Throws<NotImplementedException>(() => provider.GetPaymentParameters(null));
        }

        [Fact]
        public void Yapikredi_GetPaymentResult_ThrowsNotImpl()
        {
            var provider = new YapikrediPaymentProvider();
            Assert.Throws<NotImplementedException>(() => provider.GetPaymentResult(null));
        }
    }
}