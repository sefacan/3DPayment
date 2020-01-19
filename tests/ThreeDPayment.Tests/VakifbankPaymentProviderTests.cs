using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net.Http;
using ThreeDPayment.Payment;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class VakifbankPaymentProviderTests
    {
        [Theory]
        [InlineData(12)]
        public void PaymentProviderFactory_CreateVakifbankPaymentProvider(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            Assert.IsType<VakifbankPaymentProvider>(provider);
        }

        [Fact]
        public void Vakifbank_GetPaymentParameterResult_UnSuccess()
        {
            var httpClientFactory = new Mock<IHttpClientFactory>();
            var provider = new VakifbankPaymentProvider(httpClientFactory.Object);

            var parameterResult = provider.GetPaymentParameters(new PaymentRequest
            {
                CardHolderName = "Sefa Can",
                CardNumber = "4508-0345-0803-4509",
                ExpireMonth = 12,
                ExpireYear = 21,
                CvvCode = "000",
                Installment = 1,
                TotalAmount = 1.60m,
                CustomerIpAddress = string.Empty,
                CurrencyIsoCode = "949",
                LanguageIsoCode = "tr",
                OrderNumber = Guid.NewGuid().ToString()
            });

            Assert.False(parameterResult.Success);
        }
    }
}
