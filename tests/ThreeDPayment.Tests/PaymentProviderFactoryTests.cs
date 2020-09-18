using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using ThreeDPayment.Providers;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class PaymentProviderFactoryTests
    {
        [Theory]
        [InlineData(0)]
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
        [InlineData(13)]
        [InlineData(14)]
        public void PaymentProviderFactory_CreateProvider_ByBank(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            serviceCollection.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((BankNames)bankId);

            var banks = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            if (banks.Contains(bankId))
                Assert.IsType<AssecoPaymentProvider>(provider);

            if (bankId == 9)
                Assert.IsType<DenizbankPaymentProvider>(provider);

            if (bankId == 10)
                Assert.IsType<FinansbankPaymentProvider>(provider);

            if (bankId == 11)
                Assert.IsType<GarantiPaymentProvider>(provider);

            if (bankId == 12)
                Assert.IsType<KuveytTurkPaymentProvider>(provider);

            if (bankId == 13)
                Assert.IsType<VakifbankPaymentProvider>(provider);

            if (bankId == 14)
                Assert.IsType<YapikrediPaymentProvider>(provider);
        }

        [Fact]
        public void PaymentProviderFactory_CreatePaymentForm_EmptyParameters_ThrowNullException()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            Assert.Throws<ArgumentNullException>(() => paymentProviderFactory.CreatePaymentFormHtml(null, new Uri("https://google.com")));
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
            parameters.Add("test-1", int.MaxValue);
            parameters.Add("test-2", int.MinValue);
            parameters.Add("test-3", string.Empty);

            Assert.Throws<ArgumentNullException>(() => paymentProviderFactory.CreatePaymentFormHtml(parameters, null));
        }
    }
}
