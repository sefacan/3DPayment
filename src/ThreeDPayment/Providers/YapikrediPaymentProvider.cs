using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using ThreeDPayment.Models;

namespace ThreeDPayment.Providers
{
    public class YapikrediPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public YapikrediPaymentProvider(IHttpClientFactory httpClientFactory)
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
                string amount = (request.TotalAmount * 100m).ToString("N");//virgülden sonraki sıfırlara gerek yok

                string currencyCode = PaymentProviderFactory.CurrencyCodes[request.CurrencyIsoCode];
                if (currencyCode == "TRY")
                    currencyCode = "TL";//yapıkredi halen eski Türk lirası kodunu kullanıyor

                string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <posnetRequest>
                                            <mid>{merchantId}</mid>
                                            <tid>{terminalId}</tid>
                                            <oosRequestData>
                                                <posnetid>{posnetId}</posnetid>
                                                <XID>{request.OrderNumber}</XID>
                                                <amount>{amount}</amount>
                                                <currencyCode>{currencyCode}</currencyCode>
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

        public async Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, IFormCollection form)
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

            return VerifyGatewayResult.Successed(form["HostLogKey"], $"{form["HostLogKey"]}-{form["AuthCode"]}",
                int.Parse(form["InstalmentNumber"]),
                xmlDocument.SelectSingleNode("posnetResponse/respText")?.InnerText,
                form["ApprovedCode"]);
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string terminalId = request.BankParameters["terminalId"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <posnetRequest>
                                            <mid>{merchantId}</mid>
                                            <tid>{terminalId}</tid>
                                            <reverse>
                                                <transaction>sale</transaction>
                                                <hostLogKey>{request.ReferenceNumber.Split('-').First().Trim()}</hostLogKey>";

            //taksitli işlemde 6 haneli auth kodu isteniyor
            if (request.Installment > 0)
                requestXml += $"<authCode>{request.ReferenceNumber.Split('-').Last().Trim()}</authCode>";

            requestXml += "</reverse>";
            requestXml += "</posnetRequest>";

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

                return CancelPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("posnetResponse/hostlogkey")?.InnerText;
            return CancelPaymentResult.Successed(transactionId);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string merchantId = request.BankParameters["merchantId"];
            string terminalId = request.BankParameters["terminalId"];

            //yapıkredi bankasında tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
            string amount = (request.TotalAmount * 100m).ToString("N");//virgülden sonraki sıfırlara gerek yok

            string currencyCode = PaymentProviderFactory.CurrencyCodes[request.CurrencyIsoCode];
            if (currencyCode == "TRY")
                currencyCode = "TL";//yapıkredi halen eski Türk lirası kodunu kullanıyor

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                        <posnetRequest>
                                            <mid>{merchantId}</mid>
                                            <tid>{terminalId}</tid>
                                            <tranDateRequired>1</tranDateRequired>
                                            <return>
                                                <amount>{amount}</amount>
                                                <currencyCode>{currencyCode}</currencyCode>
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
            return RefundPaymentResult.Successed(transactionId);
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

        private static readonly IDictionary<string, string> ErrorCodes = new Dictionary<string, string>
        {
            { "146", "Hatali Sifreleme: Kullanici Ismi & Sifre Veya No Generated Record" },
            { "146", "Crypto Hatasi: Kullanici Ismi & Sifre" },
            { "147", "Hatali Kullanici Ismi & Sifre" },
            { "148", "Crypto Hatasi: Mid" },
            { "148", "Hatali Mid" },
            { "148", "Mid, Tid, Ip Hatali: X.x.x.x" },
            { "150", "Paket Hatali" },
            { "150", "Invalid Mid Tid Ip" },
            { "0095", "Red-onaylanmadi" },
            { "0100", "Host Receive Problem" },
            { "0110", "Red-yetersiz Bakiye" },
            { "0122", "Database De Istenilen Kayit Yok" },
            { "0122", "Mid Does Not Match" },
            { "0123", "Orjinal Islem Bulunamadi" },
            { "0124", "Host Session Open Problem" },
            { "0125", "Orderid Var Hostlogkey Yok Db Err" },
            { "0126", "Orderid Var Kk Sifreleme Hatasi" },
            { "0127", "Orderid Daha Once Kullanilmis" },
            { "0129", "Kredi Karti Merchant Blacklistte" },
            { "0170", "Bankanizi Arayin" },
            { "0173", "Red-onaylanmadi" },
            { "0200", "Gecersiz Islem" },
            { "0205", "Gecersiz Tutar" },
            { "0211", "Grup Kapama Yapilmis" },
            { "0213", "Red-yetersiz Bakiye" },
            { "0217", "Red-karta El Koyun" },
            { "0220", "Red Gecersiz Tutar" },
            { "0220", "Iptal Islemi Yapilmis" },
            { "0223", "Onaylanmadi" },
            { "0225", "Red-kart No Hatali" },
            { "0229", "Red-gecersiz Islem" },
            { "0232", "Kredikarti Işlem Siniri Aşildi" },
            { "0267", "Red-kart Gecerli Degil" },
            { "0363", "Red-kart Gecerli Degil" },
            { "0277", "Bankanizi Arayin" },
            { "0291", "İşyeri̇ Statüsü Hatali" },
            { "0360", "Bankanizi Arayin" },
            { "0362", "Isl. Yapilamiyor" },
            { "0370", "Islem Iptali Yapilmis" },
            { "0400", "Db Error" },
            { "0411", "Islem Henuz Finansallasmamis" },
            { "0444", "Bankanizi Arayin" },
            { "0450", "Iade Islemi Yapilamiyor" },
            { "0534", "Geçersi̇z Kart Ti̇pi̇ -x" },
            { "0551", "Krd Karti Degil" },
            { "0787", "Provizyon Bulunamadi" },
            { "0788", "Finansal Islem Yapilmis" },
            { "0789", "Provizyon Tutari Yetersiz" },
            { "0800", "Lutfen Bankanizi Arayin" },
            { "0876", "Onaylanmadi İşlem Yapilamiyor" },
            { "0877", "Onaylanmadi İşlem Yapilamiyor" },
            { "0995", "Islem Yapilamiyor" }
        };
    }
}