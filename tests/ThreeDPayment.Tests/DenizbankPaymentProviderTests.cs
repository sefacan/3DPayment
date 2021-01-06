using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using ThreeDPayment.Providers;
using ThreeDPayment.Requests;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class DenizbankPaymentProviderTests
    {
        [Fact]
        public void PaymentProviderFactory_CreateDenizbankPaymentProvider()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.DenizBank);

            Assert.IsType<DenizbankPaymentProvider>(provider);
        }

        [Fact]
        public async Task Denizbank_GetPaymentParameterResult_Success()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.DenizBank);

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
                CustomerIpAddress = "127.0.0.1",
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
        public async Task Denizbank_GetPaymentParameterResult_UnSuccess()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.DenizBank);
            var paymentGatewayResult = await provider.ThreeDGatewayRequest(null);

            Assert.False(paymentGatewayResult.Success);
        }
    }
}