using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading.Tasks;
using ThreeDPayment.Models;
using ThreeDPayment.Providers;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class DenizbankPaymentProviderTests
    {
        [Fact]
        public void PaymentProviderFactory_CreateAssecoPaymentProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create(BankNames.DenizBank);

            Assert.IsType<DenizbankPaymentProvider>(provider);
        }

        [Fact]
        public async Task Finansbank_GetPaymentParameterResult_Success()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create(BankNames.DenizBank);

            var paymentGatewayResult = await provider.ThreeDGatewayRequest(new PaymentGatewayRequest
            {
                CardHolderName = "Sefa Can",
                CardNumber = "4508-0345-0803-4509",
                ExpireMonth = 12,
                ExpireYear = 21,
                CvvCode = "000",
                Installment = 1,
                CardType = "1",
                TotalAmount = 1.60m,
                CustomerIpAddress = IPAddress.Parse("127.0.0.1"),
                CurrencyIsoCode = "949",
                LanguageIsoCode = "tr",
                OrderNumber = Guid.NewGuid().ToString(),
                BankName = BankNames.IsBankasi,
                BankParameters = provider.TestParameters,
                CallbackUrl = new Uri("https://google.com")
            });

            Assert.True(paymentGatewayResult.Success);
        }

        [Fact]
        public async Task Finansbank_GetPaymentParameterResult_UnSuccess()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            var provider = paymentProviderFactory.Create(BankNames.DenizBank);
            var paymentGatewayResult = await provider.ThreeDGatewayRequest(null);

            Assert.False(paymentGatewayResult.Success);
        }
    }
}