using Microsoft.Extensions.DependencyInjection;
using System;
using ThreeDPayment.Payment;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class AssecoPaymentProviderTests
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
        public void PaymentProviderFactory_CreateAssecoPaymentProvider(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            Assert.IsType<AssecoPaymentProvider>(provider);
        }

        [Fact]
        public void Asseco_GetPaymentParameterResult_Success()
        {
            var provider = new AssecoPaymentProvider();
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
        public void Asseco_GetPaymentParameterResult_UnSuccess()
        {
            var provider = new AssecoPaymentProvider();
            var parameterResult = provider.GetPaymentParameters(null);

            Assert.False(parameterResult.Success);
        }
    }
}