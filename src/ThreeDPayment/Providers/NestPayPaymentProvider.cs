using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment.Providers
{
    public class NestPayPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public NestPayPaymentProvider(IHttpClientFactory httpClientFactory)
        {
            client = httpClientFactory.CreateClient();
        }

        public Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            try
            {
                string clientId = request.BankParameters["clientId"];
                string processType = request.BankParameters["processType"];
                string storeKey = request.BankParameters["storeKey"];
                string storeType = request.BankParameters["storeType"];
                string random = DateTime.Now.ToString();

                var parameters = new Dictionary<string, object>();
                parameters.Add("clientid", clientId);
                parameters.Add("oid", request.OrderNumber);//sipariş numarası

                if (!request.CommonPaymentPage)
                {
                    parameters.Add("pan", request.CardNumber);
                    parameters.Add("cardHolderName", request.CardHolderName);
                    parameters.Add("Ecom_Payment_Card_ExpDate_Month", request.ExpireMonth);//kart bitiş ay'ı
                    parameters.Add("Ecom_Payment_Card_ExpDate_Year", request.ExpireYear);//kart bitiş yıl'ı
                    parameters.Add("cv2", request.CvvCode);//kart güvenlik kodu
                    parameters.Add("cardType", "1");//kart tipi visa 1 | master 2 | amex 3
                }

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("okUrl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("failUrl", request.CallbackUrl);//hatalı dönüş adresi
                parameters.Add("islemtipi", processType);//direk satış
                parameters.Add("rnd", random);//rastgele bir sayı üretilmesi isteniyor
                parameters.Add("currency", request.CurrencyIsoCode);//ISO code TL 949 | EURO 978 | Dolar 840
                parameters.Add("storetype", storeType);
                parameters.Add("lang", request.LanguageIsoCode);//iki haneli dil iso kodu

                //kuruş ayrımı nokta olmalı!!!
                string totalAmount = request.TotalAmount.ToString(new CultureInfo("en-US"));
                parameters.Add("amount", totalAmount);

                string installment = request.Installment.ToString();
                if (request.Installment > 1)
                    parameters.Add("taksit", request.Installment);//taksit sayısı | 1 veya boş tek çekim olur
                else
                    installment = string.Empty;//0 veya 1 olması durumunda taksit bilgisini boş gönderiyoruz

                var hashBuilder = new StringBuilder();
                hashBuilder.Append(clientId);
                hashBuilder.Append(request.OrderNumber);
                hashBuilder.Append(totalAmount);
                hashBuilder.Append(request.CallbackUrl);
                hashBuilder.Append(request.CallbackUrl);
                hashBuilder.Append(processType);
                hashBuilder.Append(installment);
                hashBuilder.Append(random);
                hashBuilder.Append(storeKey);

                var hashData = GetSHA1(hashBuilder.ToString());
                parameters.Add("hash", hashData);//hash data

                return Task.FromResult(PaymentGatewayResult.Successed(parameters, request.BankParameters["gatewayUrl"]));
            }
            catch (Exception ex)
            {
                return Task.FromResult(PaymentGatewayResult.Failed(ex.ToString()));
            }
        }

        public Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, PaymentGatewayRequest gatewayRequest, IFormCollection form)
        {
            if (form == null)
            {
                return Task.FromResult(VerifyGatewayResult.Failed("Form verisi alınamadı."));
            }

            var mdStatus = form["mdStatus"].ToString();
            if (string.IsNullOrEmpty(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed(form["mdErrorMsg"], form["ProcReturnCode"]));
            }

            var response = form["Response"].ToString();
            //mdstatus 1,2,3 veya 4 olursa 3D doğrulama geçildi anlamına geliyor
            if (!mdStatusCodes.Contains(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["mdErrorMsg"]}", form["ProcReturnCode"]));
            }

            if (string.IsNullOrEmpty(response) || !response.Equals("Approved"))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["ErrMsg"]}", form["ProcReturnCode"]));
            }

            var hashBuilder = new StringBuilder();
            hashBuilder.Append(request.BankParameters["clientId"]);
            hashBuilder.Append(form["oid"].FirstOrDefault());
            hashBuilder.Append(form["AuthCode"].FirstOrDefault());
            hashBuilder.Append(form["ProcReturnCode"].FirstOrDefault());
            hashBuilder.Append(form["Response"].FirstOrDefault());
            hashBuilder.Append(form["mdStatus"].FirstOrDefault());
            hashBuilder.Append(form["cavv"].FirstOrDefault());
            hashBuilder.Append(form["eci"].FirstOrDefault());
            hashBuilder.Append(form["md"].FirstOrDefault());
            hashBuilder.Append(form["rnd"].FirstOrDefault());
            hashBuilder.Append(request.BankParameters["storeKey"]);

            var hashData = GetSHA1(hashBuilder.ToString());
            if (!form["HASH"].Equals(hashData))
            {
                return Task.FromResult(VerifyGatewayResult.Failed("Güvenlik imza doğrulaması geçersiz."));
            }

            int.TryParse(form["taksit"], out int installment);
            int.TryParse(form["EXTRA.HOSTMSG"], out int extraInstallment);

            return Task.FromResult(VerifyGatewayResult.Successed(form["TransId"], form["TransId"],
                installment, extraInstallment,
                response, form["ProcReturnCode"]));
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string clientId = request.BankParameters["clientId"];
            string userName = request.BankParameters["cancelUsername"];
            string password = request.BankParameters["cancelUserPassword"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <CC5Request>
                                      <Name>{userName}</Name>
                                      <Password>{password}</Password>
                                      <ClientId>{clientId}</ClientId>
                                      <Type>Void</Type>
                                      <OrderId>{request.OrderNumber}</OrderId>
                                    </CC5Request>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("CC5Response/Response") == null ||
                xmlDocument.SelectSingleNode("CC5Response/Response").InnerText != "Approved")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return CancelPaymentResult.Failed(errorMessage);
            }

            if (xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode") == null ||
                xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode").InnerText != "00")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return CancelPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("CC5Response/TransId")?.InnerText ?? string.Empty;
            return CancelPaymentResult.Successed(transactionId, transactionId);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string clientId = request.BankParameters["clientId"];
            string userName = request.BankParameters["refundUsername"];
            string password = request.BankParameters["refundUserPassword"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <CC5Request>
                                      <Name>{userName}</Name>
                                      <Password>{password}</Password>
                                      <ClientId>{clientId}</ClientId>
                                      <Type>Credit</Type>
                                      <OrderId>{request.OrderNumber}</OrderId>
                                    </CC5Request>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            if (xmlDocument.SelectSingleNode("CC5Response/Response") == null ||
                xmlDocument.SelectSingleNode("CC5Response/Response").InnerText != "Approved")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return RefundPaymentResult.Failed(errorMessage);
            }

            if (xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode") == null ||
                xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode").InnerText != "00")
            {
                var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(errorMessage))
                    errorMessage = "Bankadan hata mesajı alınamadı.";

                return RefundPaymentResult.Failed(errorMessage);
            }

            var transactionId = xmlDocument.SelectSingleNode("CC5Response/TransId")?.InnerText ?? string.Empty;
            return RefundPaymentResult.Successed(transactionId, transactionId);
        }

        public async Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            string clientId = request.BankParameters["clientId"];
            string userName = request.BankParameters["userName"];
            string password = request.BankParameters["password"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <CC5Request>
                                        <Name>{userName}</Name>
                                        <Password>{password}</Password>
                                        <ClientId>{clientId}</ClientId>
                                        <OrderId>{request.OrderNumber}</OrderId>
                                        <Extra>
                                            <ORDERDETAIL>QUERY</ORDERDETAIL>
                                        </Extra>
                                    </CC5Request>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            string finalStatus = xmlDocument.SelectSingleNode("CC5Response/Extra/ORDER_FINAL_STATUS")?.InnerText ?? string.Empty;
            string transactionId = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_TRAN_UID")?.InnerText;
            string referenceNumber = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_TRAN_UID")?.InnerText;
            string cardPrefix = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_CARDBIN")?.InnerText;
            string installment = xmlDocument.SelectSingleNode("CC5Response/Extra/TRX_1_INSTALMENT")?.InnerText ?? "0";
            string bankMessage = xmlDocument.SelectSingleNode("CC5Response/Response")?.InnerText;
            string responseCode = xmlDocument.SelectSingleNode("CC5Response/ProcReturnCode")?.InnerText;

            if (finalStatus.Equals("SALE", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(installment, out int installmentValue);
                return PaymentDetailResult.PaidResult(transactionId, referenceNumber, cardPrefix, installmentValue, 0, bankMessage, responseCode);
            }
            else if (finalStatus.Equals("VOID", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.CanceledResult(transactionId, referenceNumber, bankMessage, responseCode);
            }
            else if (finalStatus.Equals("REFUND", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentDetailResult.RefundedResult(transactionId, referenceNumber, bankMessage, responseCode);
            }

            var errorMessage = xmlDocument.SelectSingleNode("CC5Response/ErrMsg")?.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Bankadan hata mesajı alınamadı.";

            return PaymentDetailResult.FailedResult(errorMessage: errorMessage);
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "clientId", "700655000200" },
            { "processType", "Auth" },
            { "storeKey", "TRPS0200" },
            { "storeType", "3D_PAY" },
            { "gatewayUrl", "https://entegrasyon.asseco-see.com.tr/fim/est3Dgate" },
            { "userName", "ISBANKAPI" },
            { "password", "ISBANK07" },
            { "verifyUrl", "https://entegrasyon.asseco-see.com.tr/fim/api" }
        };

        private string GetSHA1(string text)
        {
            var cryptoServiceProvider = new SHA1CryptoServiceProvider();
            var inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(text));
            var hashData = Convert.ToBase64String(inputbytes);

            return hashData;
        }

        private static readonly string[] mdStatusCodes = new[] { "1", "2", "3", "4" };
    }
}