using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Threading.Tasks;
using ThreeDPayment.Providers;
using ThreeDPayment.Requests;
using Xunit;

namespace ThreeDPayment.Tests
{
    public class KuveytTurkPaymentProviderTests
    {
        [Fact]
        public void PaymentProviderFactory_CreateKuveytTurkPaymentProvider()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);
            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.KuveytTurk);

            Assert.IsType<KuveytTurkPaymentProvider>(provider);
        }

        [Fact]
        public async Task KuveytTurk_GetPaymentParameterResult_Success()
        {
            //it must be here for different encoding options, KuveytTurk uses different encoding type that doesn't come in default encoding collection
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.KuveytTurk);
            var paymentGatewayResult = await provider.ThreeDGatewayRequest(new PaymentGatewayRequest
            {
                CardHolderName = "Sefa Can",
                CardNumber = "4033602562020327",
                ExpireMonth = 1,
                ExpireYear = 30,
                CvvCode = "861",
                CardType = "Troy",
                Installment = 1,
                TotalAmount = 1.60m,
                CustomerIpAddress = "127.0.0.1",
                CurrencyIsoCode = "949",
                LanguageIsoCode = "tr",
                OrderNumber = Guid.NewGuid().ToString(),
                BankName = BankNames.KuveytTurk,
                BankParameters = provider.TestParameters,
                CallbackUrl = new Uri("https://google.com")
            });

            Assert.NotEmpty(paymentGatewayResult.HtmlFormContent);
            Assert.True(paymentGatewayResult.HtmlContent);
            Assert.True(paymentGatewayResult.Success);
        }

        /*
         * 
         <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml">
                <head runat="server">
                    <title></title>
                </head>
                <body onload="OnLoadEvent();">
                    <form name="downloadForm"
                        action="https://www.nsoftware.com/3dsecure/testing/acs.asp"
                        method="POST">
                        <input type="hidden"
                            name="PaReq"
                            value="eJxUUd1uskAQfRXiA7C7SMuHGTehXZPPGKyx2KSXZJ0otYIui8jbd5ZKbfdqzvycnXMGsr1BVK+o
G4MSUqzrfIdesZ2OAh5wEYh/nF7Io5gLHo0krJI1niVc0NRFVUrhcz8ANkBiMHqfl1ZCrs9P86V0
45wDu0Eo8yPKxeZt9p552Wa98FbrFwWsT4OumtKaTkYxcQ4AGvMp99ae6gljbdv6h+aCnbWNOfi6
OvrWMGCuB9j991Xjopo4r8VWph9Jl6pZl2a7NlVJu1TJ+CVLrqnSU2CuA7a5RTlo9oSYBPEkfADW
5yE/umWkeHRKvmM4uS+SX4XfCSBDDZa6k3EYk5YBAV5PVYnUQQp/YmD3fZ//O++0nSsZ0jKcfB9H
IhRC0MSt4FgKsmZM1Z7GAWBulN0ORF70t6Toz42/AAAA//8FwIEAAAAAAJD/awCxUaHt">
                        <input type="hidden"
                            name="TermUrl" value="https://boatest.kuveytturk.com.tr/boa.virtualpos.services/Home/ThreeDModelResponseGate">
                        <input type="hidden"
                            name="MD"
                            value="YqMz8x8e3tp54AajW8ustT/e+BKsegpH8aIQ71xahOHruzn7ZE4BdFZi5NomH297">
                        <!-- To support javascript unaware/disabled browsers -->
                        <noscript>
                    <center>Please click the submit button below.<br>
                    <input type="submit" name="submit" value="Submit"></center>
                  </noscript>
                    </form>

                    <script language="Javascript">                
                    <!--
                    function OnLoadEvent() {
                        document.downloadForm.submit();
                    }
                    //-->
                    </script>
                </body>
                </html>
    */

        [Fact]
        public async Task KuveytTurk_GetPaymentParameterResult_UnSuccess()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            PaymentProviderFactory paymentProviderFactory = new PaymentProviderFactory(serviceProvider);

            IPaymentProvider provider = paymentProviderFactory.Create(BankNames.KuveytTurk);
            var paymentGatewayResult = await provider.ThreeDGatewayRequest(null);

            Assert.False(paymentGatewayResult.Success);
        }
    }
}
