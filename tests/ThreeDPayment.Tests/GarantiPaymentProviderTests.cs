using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading.Tasks;
using ThreeDPayment.Models;
using ThreeDPayment.Providers;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class GarantiPaymentProviderTests
    {
        [Fact]
        public void PaymentProviderFactory_CreateGarantiPaymentProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            serviceCollection.AddHttpContextAccessor();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create(BankNames.Garanti);

            Assert.IsType<GarantiPaymentProvider>(provider);
        }

        [Fact]
        public async Task Garanti_GetPaymentParameterResult_Success()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            serviceCollection.AddHttpContextAccessor();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create(BankNames.Garanti);

            var paymentGatewayResult = await provider.ThreeDGatewayRequest(new PaymentGatewayRequest
            {
                CardHolderName = "Sefa Can",
                CardNumber = "4508-0345-0803-4509",
                ExpireMonth = 12,
                ExpireYear = 21,
                CvvCode = "000",
                CardType = "1",
                Installment = 1,
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
        public async Task Garanti_GetPaymentParameterResult_UnSuccess()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            serviceCollection.AddHttpContextAccessor();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            var provider = paymentProviderFactory.Create(BankNames.Garanti);
            var paymentGatewayResult = await provider.ThreeDGatewayRequest(null);

            Assert.False(paymentGatewayResult.Success);
        }
    }
}
