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
        [InlineData(46)]
        [InlineData(64)]
        [InlineData(12)]
        [InlineData(10)]
        [InlineData(32)]
        [InlineData(99)]
        [InlineData(206)]
        [InlineData(135)]
        [InlineData(123)]
        [InlineData(59)]
        [InlineData(134)]
        [InlineData(111)]
        [InlineData(62)]
        [InlineData(205)]
        [InlineData(15)]
        [InlineData(67)]
        [InlineData(203)]
        public void PaymentProviderFactory_CreateProvider_ByBank(int bankId)
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            IPaymentProvider provider = paymentProviderFactory.Create((BankNames)bankId);

            //NestPay
            int[] banks = new[] { 46, 64, 12, 10, 32, 99, 206, 135, 123, 59 };
            if (banks.Contains(bankId))
            {
                Assert.IsType<NestPayPaymentProvider>(provider);
            }

            //InterVPOS
            if (bankId == 134)
            {
                Assert.IsType<DenizbankPaymentProvider>(provider);
            }

            //PayFor
            if (bankId == 111)
            {
                Assert.IsType<FinansbankPaymentProvider>(provider);
            }

            //GVP
            if (bankId == 62)
            {
                Assert.IsType<GarantiPaymentProvider>(provider);
            }

            //KuveytTurk
            if (bankId == 205)
            {
                Assert.IsType<KuveytTurkPaymentProvider>(provider);
            }

            //GET 7/24
            if (bankId == 15)
            {
                Assert.IsType<VakifbankPaymentProvider>(provider);
            }

            //Posnet
            if (bankId == 67 || bankId == 203)
            {
                Assert.IsType<PosnetPaymentProvider>(provider);
            }
        }

        [Fact]
        public void PaymentProviderFactory_CreatePaymentForm_EmptyParameters_ThrowNullException()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            Assert.Throws<ArgumentNullException>(() => paymentProviderFactory.CreatePaymentFormHtml(null, new Uri("https://google.com")));
        }

        [Fact]
        public void PaymentProviderFactory_CreatePaymentForm_PaymentUri_ThrowNullException()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("test", decimal.Zero);
            parameters.Add("test-1", int.MaxValue);
            parameters.Add("test-2", int.MinValue);
            parameters.Add("test-3", string.Empty);

            Assert.Throws<ArgumentNullException>(() => paymentProviderFactory.CreatePaymentFormHtml(parameters, null));
        }
    }
}
