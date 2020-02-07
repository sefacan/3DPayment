using Microsoft.Extensions.DependencyInjection;
using System;
using ThreeDPayment.Payment;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class FinansbankPaymentProviderTests
    {
        [Theory]
        [InlineData(8)]
        public void PaymentProviderFactory_CreateAssecoPaymentProvider(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            Assert.IsType<FinansbankPaymentProvider>(provider);
        }

        [Fact]
        public void Finansbank_GetPaymentParameterResult_Success()
        {
            var provider = new FinansbankPaymentProvider();
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
        public void Finansbank_GetPaymentParameterResult_UnSuccess()
        {
            var provider = new FinansbankPaymentProvider();
            var parameterResult = provider.GetPaymentParameters(null);

            Assert.False(parameterResult.Success);
        }
    }
}