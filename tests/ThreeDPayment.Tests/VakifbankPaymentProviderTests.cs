using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ThreeDPayment.Models;
using ThreeDPayment.Providers;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class VakifbankPaymentProviderTests
    {
        [Fact]
        public void PaymentProviderFactory_CreateVakifbankPaymentProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            serviceCollection.AddHttpContextAccessor();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create(BankNames.VakifBank);

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

            var httpClientFactory = new Mock<IHttpClientFactory>();
            var messageHandler = new FakeResponseHandler();
            messageHandler.AddFakeResponse(new HttpResponseMessage(HttpStatusCode.OK), successResponseXml, true);

            var httpClient = new HttpClient(new FakeResponseHandler(), false);
            httpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            var context = new DefaultHttpContext();
            httpContextAccessor.Setup(_ => _.HttpContext).Returns(context);

            var provider = new VakifbankPaymentProvider(httpClientFactory.Object, httpContextAccessor.Object);
            var paymentGatewayResult = await provider.ThreeDGatewayRequest(new PaymentGatewayRequest
            {
                CardHolderName = "Sefa Can",
                CardNumber = "4508-0345-0803-4509",
                ExpireMonth = 12,
                ExpireYear = 21,
                CvvCode = "000",
                Installment = 1,
                TotalAmount = 1.60m,
                CustomerIpAddress = IPAddress.Parse("127.0.0.1"),
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
