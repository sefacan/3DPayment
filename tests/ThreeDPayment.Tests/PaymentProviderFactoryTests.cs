using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using ThreeDPayment.Payment;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class PaymentProviderFactoryTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        public void PaymentProviderFactory_CreateProvider_ByBank(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            var banks = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            if (banks.Contains(bankId))
                Assert.IsType<AssecoPaymentProvider>(provider);

            if (bankId == 10)
                Assert.IsType<YapikrediPaymentProvider>(provider);

            if (bankId == 11)
                Assert.IsType<GarantiPaymentProvider>(provider);

            if (bankId == 12)
                Assert.IsType<VakifbankPaymentProvider>(provider);
        }

        [Fact]
        public void PaymentProviderFactory_CreatePaymentForm_EmptyParameters_ThrowNullException()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            Assert.Throws<ArgumentNullException>(() => paymentProviderFactory.CreatePaymentForm(null, new Uri("https://google.com")));
        }

        [Fact]
        public void PaymentProviderFactory_CreatePaymentForm_PaymentUri_ThrowNullException()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            var parameters = new Dictionary<string, object>();
            parameters.Add("test", decimal.Zero);
            Assert.Throws<ArgumentNullException>(() => paymentProviderFactory.CreatePaymentForm(parameters, null));
        }
    }
}
