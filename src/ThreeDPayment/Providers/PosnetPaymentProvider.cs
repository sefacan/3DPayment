using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment.Providers
{
    public class PosnetPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public PosnetPaymentProvider(IHttpClientFactory httpClientFactory)
        {
            client = httpClientFactory.CreateClient();
        }

        public async Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string terminalId = request.BankParameters["terminalId"];
            string posnetId = request.BankParameters["posnetId"];

            try
            {
                //yapıkredi bankasında tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
                string amount = (request.TotalAmount * 100m).ToString("0.##", new CultureInfo("en-US"));//virgülden sonraki sıfırlara gerek yok

                string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <posnetRequest>
                                            <mid>{merchantId}</mid>
                                            <tid>{terminalId}</tid>
                                            <oosRequestData>
                                                <posnetid>{posnetId}</posnetid>
                                                <XID>{request.OrderNumber}</XID>
                                                <amount>{amount}</amount>
                                                <currencyCode>{CurrencyCodes[request.CurrencyIsoCode]}</currencyCode>
                                                <installment>{string.Format("{0:00}", request.Installment)}</installment>
                                                <tranType>Sale</tranType>
                                                <cardHolderName>{request.CardHolderName}</cardHolderName>
                                                <ccno>{request.CardNumber}</ccno>
                                                <expDate>{request.ExpireMonth}{request.ExpireYear}</expDate>
                                                <cvc>{request.CvvCode}</cvc>
                                            </oosRequestData>
                                        </posnetRequest>";

                var httpParameters = new Dictionary<string, string>();
                httpParameters.Add("xmldata", requestXml);

                var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(httpParameters));
                string responseContent = await response.Content.ReadAsStringAsync();

                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(responseContent);

                if (xmlDocument.SelectSingleNode("posnetResponse/approved") == null ||
                    xmlDocument.SelectSingleNode("posnetResponse/approved").InnerText != "1")
                {
                    string errorMessage = xmlDocument.SelectSingleNode("posnetResponse/respText")?.InnerText ?? string.Empty;
                    if (string.IsNullOrEmpty(errorMessage))
                        errorMessage = "Bankadan hata mesajı alınamadı.";

                    return PaymentGatewayResult.Failed(errorMessage);
                }

                var data1Node = xmlDocument.SelectSingleNode("posnetResponse/oosRequestDataResponse/data1");
                var data2Node = xmlDocument.SelectSingleNode("posnetResponse/oosRequestDataResponse/data2");
                var signNode = xmlDocument.SelectSingleNode("posnetResponse/oosRequestDataResponse/sign");

                var parameters = new Dictionary<string, object>();
                parameters.Add("posnetData", data1Node.InnerText);
                parameters.Add("posnetData2", data2Node.InnerText);
                parameters.Add("digest", signNode.InnerText);

                parameters.Add("mid", merchantId);
                parameters.Add("posnetID", posnetId);

                //Vade Farklı işlemler için kullanılacak olan kampanya kodunu belirler.
                //Üye İşyeri için tanımlı olan kampanya kodu, İşyeri Yönetici Ekranlarına giriş yapıldıktan sonra, Üye İşyeri bilgileri sayfasından öğrenilebilinir.
                parameters.Add("vftCode", string.Empty);

                parameters.Add("merchantReturnURL", request.CallbackUrl);//geri dönüş adresi
                parameters.Add("lang", request.LanguageIsoCode);
                parameters.Add("url", string.Empty);//openANewWindow 1 olarak ayarlanırsa buraya gidilecek url verilmeli
                parameters.Add("openANewWindow", "0");//POST edilecek formun yeni bir sayfaya mı yoksa mevcut sayfayı mı yönlendirileceği
                parameters.Add("useJokerVadaa", "1");//yapıkredi kartlarında vadaa kullanılabilirse izin verir

                return PaymentGatewayResult.Successed(parameters, request.BankParameters["gatewayUrl"]);
            }
            catch (Exception ex)
            {
                return PaymentGatewayResult.Failed(ex.ToString());
            }
        }

        public async Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, PaymentGatewayRequest gatewayRequest, IFormCollection form)
        {
            if (form == null)
            {
                return VerifyGatewayResult.Failed("Form verisi alınamadı.");
            }

            if (!form.ContainsKey("BankPacket") || !form.ContainsKey("MerchantPacket") || !form.ContainsKey("Sign"))
            {
                return VerifyGatewayResult.Failed("Form verisi alınamadı.");
            }

            var merchantId = request.BankParameters["merchantId"];
            var terminalId = request.BankParameters["terminalId"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <posnetRequest>
                                        <mid>{merchantId}</mid>
                                        <tid>{terminalId}</tid>
                                        <oosResolveMerchantData>
                                            <bankData>{form["BankPacket"]}</bankData>
                                            <merchantData>{form["MerchantPacket"]}</merchantData>
                                            <sign>{form["Sign"]}</sign>
                                        </oosResolveMerchantData>
                                    </posnetRequest>";

            var httpParameters = new Dictionary<string, string>();
            httpParameters.Add("xmldata", requestXml);

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(httpParameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("posnetResponse/approved") == null ||
                xmlDocument.SelectSingleNode("posnetResponse/approved").InnerText != "1" ||
                xmlDocument.SelectSingleNode("posnetResponse/approved").InnerText != "2")
            {
                string errorMessage = "3D doğrulama başarısız.";
                if (xmlDocument.SelectSingleNode("posnetResponse/respText") != null)
                    errorMessage = xmlDocument.SelectSingleNode("posnetResponse/respText").InnerText;

                return VerifyGatewayResult.Failed(errorMessage, form["ApprovedCode"],
                    xmlDocument.SelectSingleNode("posnetResponse/approved").InnerText);
            }

            int.TryParse(form["InstalmentNumber"], out int instalmentNumber);

            return VerifyGatewayResult.Successed(form["HostLogKey"], $"{form["HostLogKey"]}-{form["AuthCode"]}",
                instalmentNumber, 0,
                xmlDocument.SelectSingleNode("posnetResponse/respText")?.InnerText,
                form["ApprovedCode"]);
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string terminalId = request.BankParameters["terminalId"];

            var xmlBuilder = new StringBuilder();
            xmlBuilder.Append($@"<?xml version=""1.0"" encoding=""utf-8""?>
                                     <posnetRequest>
                                         <mid>{merchantId}</mid>
                                         <tid>{terminalId}</tid>
                                         <reverse>
                                             <transaction>sale</transaction>
                                             <hostLogKey>{request.ReferenceNumber.Split('-').First().Trim()}</hostLogKey>");

            //taksitli işlemde 6 haneli auth kodu isteniyor
            if (request.Installment > 0)
                xmlBuilder.Append($"<authCode>{request.ReferenceNumber.Split('-').Last().Trim()}</authCode>");

            xmlBuilder.Append(@"</reverse>
                                </posnetRequest>");

            var httpParameters = new Dictionary<string, string>();
            httpParameters.Add("xmldata", xmlBuilder.ToString());

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(httpParameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("posnetResponse/approved") == null ||
                xmlDocument.SelectSingleNode("posnetResponse/approved").InnerText != "1")
            {
                string errorMessage = xmlDocument.SelectSingleNode("posnetResponse/respText")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return CancelPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("posnetResponse/hostlogkey")?.InnerText;
            return CancelPaymentResult.Successed(transactionId, transactionId);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string terminalId = request.BankParameters["terminalId"];

            //yapıkredi bankasında tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
            string amount = (request.TotalAmount * 100m).ToString("N");//virgülden sonraki sıfırlara gerek yok

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <posnetRequest>
                                            <mid>{merchantId}</mid>
                                            <tid>{terminalId}</tid>
                                            <tranDateRequired>1</tranDateRequired>
                                            <return>
                                                <amount>{amount}</amount>
                                                <currencyCode>{CurrencyCodes[request.CurrencyIsoCode]}</currencyCode>
                                                <hostLogKey>{request.ReferenceNumber.Split('-').First().Trim()}</hostLogKey>
                                            </return>
                                        </posnetRequest>";

            var httpParameters = new Dictionary<string, string>();
            httpParameters.Add("xmldata", requestXml);

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(httpParameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("posnetResponse/approved") == null ||
                xmlDocument.SelectSingleNode("posnetResponse/approved").InnerText != "1")
            {
                string errorMessage = xmlDocument.SelectSingleNode("posnetResponse/respText")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return RefundPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("posnetResponse/hostlogkey")?.InnerText;
            return RefundPaymentResult.Successed(transactionId, transactionId);
        }

        public async Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string terminalId = request.BankParameters["terminalId"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <posnetRequest>
                                            <mid>{merchantId}</mid>
                                            <tid>{terminalId}</tid>
                                            <agreement>
                                                <orderID>TDSC{request.OrderNumber}</orderID>
                                            </agreement>
                                        </posnetRequest>";

            var httpParameters = new Dictionary<string, string>();
            httpParameters.Add("xmldata", requestXml);

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new FormUrlEncodedContent(httpParameters));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            string bankMessage = xmlDocument.SelectSingleNode("posnetResponse/respText")?.InnerText;
            string responseCode = xmlDocument.SelectSingleNode("posnetResponse/respCode")?.InnerText;
            string approved = xmlDocument.SelectSingleNode("posnetResponse/approved")?.InnerText ?? string.Empty;

            if (!approved.Equals("1"))
            {
                if (string.IsNullOrEmpty(bankMessage))
                    bankMessage = "Bankadan hata mesajı alınamadı.";

                return PaymentDetailResult.FailedResult(errorMessage: bankMessage, errorCode: responseCode);
            }

            string finalStatus = xmlDocument.SelectSingleNode("posnetResponse/transactions/transaction/state")?.InnerText ?? string.Empty;
            if (!finalStatus.Equals("SALE", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(bankMessage))
                    bankMessage = "Bankadan hata mesajı alınamadı.";

                return PaymentDetailResult.FailedResult(errorMessage: bankMessage, errorCode: responseCode);
            }

            string transactionId = xmlDocument.SelectSingleNode("posnetResponse/transactions/transaction/hostLogKey")?.InnerText;
            string referenceNumber = xmlDocument.SelectSingleNode("posnetResponse/transactions/transaction/hostLogKey")?.InnerText;
            string authCode = xmlDocument.SelectSingleNode("posnetResponse/transactions/transaction/authCode")?.InnerText;
            string cardPrefix = xmlDocument.SelectSingleNode("posnetResponse/transactions/transaction/ccno")?.InnerText;

            return PaymentDetailResult.PaidResult(transactionId, $"{referenceNumber}-{authCode}", cardPrefix, bankMessage: bankMessage, responseCode: responseCode);
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "merchantId", "" },
            { "terminalId", "" },
            { "posnetId", "" },
            { "verifyUrl", "https://posnettest.yapikredi.com.tr/PosnetWebService/XML" },
            { "gatewayUrl", "https://posnettest.yapikredi.com.tr/PosnetWebService/XML" }
        };

        private static readonly IDictionary<string, string> CurrencyCodes = new Dictionary<string, string>
        {
            { "949", "TL" },
            { "840", "USD" },
            { "978", "EUR" },
            { "826", "GBP" }
        };
    }
}