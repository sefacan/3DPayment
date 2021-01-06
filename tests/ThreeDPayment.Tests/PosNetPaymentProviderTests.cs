using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ThreeDPayment.Providers;
using ThreeDPayment.Requests;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class PosNetPaymentProviderTests
    {
        [Fact]
        public void PaymentProviderFactory_CreatePosNetPaymentProvider()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.Yapikredi);
            Assert.IsType<PosnetPaymentProvider>(provider);

            provider = paymentProviderFactory.Create(BankNames.Albaraka);
            Assert.IsType<PosnetPaymentProvider>(provider);
        }

        [Fact]
        public async Task PosNet_GetPaymentParameterResult_Success()
        {
            string successResponseXml = @"<posnetResponse>
                                          	<approved>1</approved>
                                          	<respText>successed</respText>
                                          	<oosRequestDataResponse>
                                                  <data1>345345FDGSFSDF</data1>  
                                                  <data2>345345FDGSFSDF</data2>  
                                                  <sign>345345FDGSFSDF</sign>  
                                            </oosRequestDataResponse>
                                          </posnetResponse>";

            Mock<IHttpClientFactory> httpClientFactory = new Mock<IHttpClientFactory>();
            FakeResponseHandler messageHandler = new FakeResponseHandler();
            messageHandler.AddFakeResponse(new HttpResponseMessage(HttpStatusCode.OK), successResponseXml, true);

            HttpClient httpClient = new HttpClient(messageHandler, false);
            httpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            IPaymentProvider provider = new PosnetPaymentProvider(httpClientFactory.Object);
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
        public async Task PosNet_GetPaymentResult_UnSuccess()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.Garanti);
            var paymentGatewayResult = await provider.ThreeDGatewayRequest(null);

            Assert.False(paymentGatewayResult.Success);
        }
    }
}