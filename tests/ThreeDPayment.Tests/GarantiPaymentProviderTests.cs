using Microsoft.Extensions.DependencyInjection;
using System;
using ThreeDPayment.Payment;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class GarantiPaymentProviderTests
    {
        [Theory]
        [InlineData(11)]
        public void PaymentProviderFactory_CreateGarantiPaymentProvider(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            Assert.IsType<GarantiPaymentProvider>(provider);
        }

        [Fact]
        public void Garanti_GetPaymentParameterResult_Success()
        {
            var provider = new GarantiPaymentProvider();
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

            Assert.True(parameterResult.Success);
        }

        [Fact]
        public void Garanti_GetPaymentParameterResult_UnSuccess()
        {
            var provider = new GarantiPaymentProvider();
            var parameterResult = provider.GetPaymentParameters(null);

            Assert.False(parameterResult.Success);
        }
    }
}
