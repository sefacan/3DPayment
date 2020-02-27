using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net.Http;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class YapikrediPaymentProviderTests
    {
        [Theory]
        [InlineData(10)]
        public void PaymentProviderFactory_CreateYapikrediPaymentProvider(int bankId)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            serviceCollection.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            Assert.IsType<YapikrediPaymentProvider>(provider);
        }

        [Fact]
        public void Yapikredi_GetPaymentParameterResult_UnSuccess()
        {
            var httpClientFactory = new Mock<IHttpClientFactory>();
            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            var context = new DefaultHttpContext();
            httpContextAccessor.Setup(_ => _.HttpContext).Returns(context);

            var provider = new YapikrediPaymentProvider(httpClientFactory.Object);
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

        [Fact]
        public void Yapikredi_GetPaymentResult_ThrowsNotImpl()
        {
            var httpClientFactory = new Mock<IHttpClientFactory>();
            var provider = new YapikrediPaymentProvider(httpClientFactory.Object);
            Assert.Throws<NotImplementedException>(() => provider.GetPaymentResult(null));
        }
    }
}