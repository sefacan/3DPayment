using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net;
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
            serviceCollection.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            var provider = paymentProviderFactory.Create((Banks)bankId);

            Assert.IsType<VakifbankPaymentProvider>(provider);
        }

        [Fact]
        public void Vakifbank_GetPaymentParameterResult_Success()
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
        public void Vakifbank_GetPaymentParameterResult_UnSuccess()
        {
            var httpClientFactory = new Mock<IHttpClientFactory>();
            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            var context = new DefaultHttpContext();
            httpContextAccessor.Setup(_ => _.HttpContext).Returns(context);

            var provider = new VakifbankPaymentProvider(httpClientFactory.Object, httpContextAccessor.Object);
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
