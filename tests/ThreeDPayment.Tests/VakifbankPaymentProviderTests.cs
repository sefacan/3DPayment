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
    public class VakifbankPaymentProviderTests
    {
        [Fact]
        public void PaymentProviderFactory_CreateVakifbankPaymentProvider()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.VakifBank);

            Assert.IsType<VakifbankPaymentProvider>(provider);
        }

        [Fact]
        public async Task Vakifbank_GetPaymentParameterResult_Success()
        {
            string successResponseXml = @"<IPaySecure>
                                          	<Message>
                                          		<VERes>
                                          			<Status>Y</Status>
                                          			<PaReq>DFHDFSDFJD436746732423TJ4354GDFDFH</PaReq>
                                          			<TermUrl>https://example.org</TermUrl>
                                          			<MD>DFHDFSDFJD436746732423TJ4354GDFDFH</MD>
                                          			<ACSUrl>https://example.org</ACSUrl>
                                          		</VERes>
                                          	</Message>
                                          	<MessageErrorCode></MessageErrorCode>
                                          	<ErrorMessage></ErrorMessage>
                                          </IPaySecure>";

            Mock<IHttpClientFactory> httpClientFactory = new Mock<IHttpClientFactory>();
            FakeResponseHandler messageHandler = new FakeResponseHandler();
            messageHandler.AddFakeResponse(new HttpResponseMessage(HttpStatusCode.OK), successResponseXml, true);

            HttpClient httpClient = new HttpClient(messageHandler, false);
            httpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            IPaymentProvider provider = new VakifbankPaymentProvider(httpClientFactory.Object);
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
                BankName = BankNames.VakifBank,
                BankParameters = provider.TestParameters,
                CallbackUrl = new Uri("https://google.com")
            });

            Assert.True(paymentGatewayResult.Success);
        }

        [Fact]
        public async Task Vakifbank_GetPaymentParameterResult_UnSuccess()
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
