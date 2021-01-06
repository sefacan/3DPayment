using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment.Providers
{
    public class FinansbankPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public FinansbankPaymentProvider(IHttpClientFactory httpClientFactory)
        {
            client = httpClientFactory.CreateClient();
        }

        public Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            try
            {
                string mbrId = request.BankParameters["mbrId"];//Mağaza numarası
                string merchantId = request.BankParameters["merchantId"];//Mağaza numarası
                string userCode = request.BankParameters["userCode"];//
                string userPass = request.BankParameters["userPass"];//Mağaza anahtarı
                string txnType = request.BankParameters["txnType"];//İşlem tipi
                string secureType = request.BankParameters["secureType"];
                string totalAmount = request.TotalAmount.ToString(new CultureInfo("en-US"));

                var parameters = new Dictionary<string, object>();
                parameters.Add("MbrId", mbrId);
                parameters.Add("MerchantId", merchantId);
                parameters.Add("UserCode", userCode);
                parameters.Add("UserPass", userPass);
                parameters.Add("PurchAmount", totalAmount);//kuruş ayrımı nokta olmalı!!!
                parameters.Add("Currency", request.CurrencyIsoCode);//TL:949, USD:840, EUR:978
                parameters.Add("OrderId", request.OrderNumber);//sipariş numarası
                parameters.Add("TxnType", txnType);//direk satış
                parameters.Add("SecureType", secureType);//NonSecure, 3Dpay, 3DModel, 3DHost
                parameters.Add("Pan", request.CardNumber);//kart numarası
                parameters.Add("Expiry", $"{request.ExpireMonth}{request.ExpireYear}");//kart bitiş ay-yıl birleşik
                parameters.Add("Cvv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("Lang", request.LanguageIsoCode);//iki haneli dil iso kodu

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("OkUrl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("FailUrl", request.CallbackUrl);//hatalı dönüş adresi

                string installment = request.Installment.ToString();
                if (request.Installment < 2)
                    installment = string.Empty;//0 veya 1 olması durumunda taksit bilgisini boş gönderiyoruz

                parameters.Add("InstallmentCount", installment);//taksit sayısı | 0, 1 veya boş tek çekim olur

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

            var mdStatus = form["mdStatus"];
            if (StringValues.IsNullOrEmpty(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed(form["mdErrorMsg"], form["ProcReturnCode"]));
            }

            var response = form["Response"];
            //mdstatus 1,2,3 veya 4 olursa 3D doğrulama geçildi anlamına geliyor
            if (!mdStatus.Equals("1") || !mdStatus.Equals("2") || !mdStatus.Equals("3") || !mdStatus.Equals("4"))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["mdErrorMsg"]}", form["ProcReturnCode"]));
            }

            if (StringValues.IsNullOrEmpty(response) || !response.Equals("Approved"))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["ErrorMessage"]}", form["ProcReturnCode"]));
            }

            int.TryParse(form["taksitsayisi"], out int taksitSayisi);

            return Task.FromResult(VerifyGatewayResult.Successed(form["TransId"], form["TransId"],
                taksitSayisi, 0, response,
                form["ProcReturnCode"]));
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string mbrId = request.BankParameters["mbrId"];//Mağaza numarası
            string merchantId = request.BankParameters["merchantId"];//Mağaza numarası
            string userCode = request.BankParameters["userCode"];//
            string userPass = request.BankParameters["userPass"];//Mağaza anahtarı
            string txnType = request.BankParameters["txnType"];//İşlem tipi
            string secureType = request.BankParameters["secureType"];

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
                                    <PayforIptal>
                                        <MbrId>{mbrId}</MbrId>
                                        <MerchantID>{merchantId}</MerchantID>
                                        <UserCode>{userCode}</UserCode>
                                        <UserPass>{userPass}</UserPass>
                                        <OrgOrderId>{request.OrderNumber}</OrgOrderId>
                                        <SecureType>NonSecure</SecureType>
                                        <TxnType>Void</TxnType>
                                        <Currency>{request.CurrencyIsoCode}</Currency>
                                        <Lang>{request.LanguageIsoCode.ToUpper()}</Lang>
                                    </PayforIptal>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            //TODO Finansbank response
            //if (xmlDocument.SelectSingleNode("VposResponse/ResultCode") == null ||
            //    xmlDocument.SelectSingleNode("VposResponse/ResultCode").InnerText != "0000")
            //{
            //    string errorMessage = xmlDocument.SelectSingleNode("VposResponse/ResultDetail")?.InnerText ?? string.Empty;
            //    if (string.IsNullOrEmpty(errorMessage))
            //        errorMessage = "Bankadan hata mesajı alınamadı.";

            //    return CancelPaymentResult.Failed(errorMessage);
            //}

            var transactionId = xmlDocument.SelectSingleNode("VposResponse/TransactionId")?.InnerText;
            return CancelPaymentResult.Successed(transactionId, transactionId);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string mbrId = request.BankParameters["mbrId"];//Mağaza numarası
            string merchantId = request.BankParameters["merchantId"];//Mağaza numarası
            string userCode = request.BankParameters["userCode"];//
            string userPass = request.BankParameters["userPass"];//Mağaza anahtarı
            string txnType = request.BankParameters["txnType"];//İşlem tipi
            string secureType = request.BankParameters["secureType"];
            string totalAmount = request.TotalAmount.ToString(new CultureInfo("en-US"));

            string requestXml = $@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
                                    <PayforIade>
                                        <MbrId>{mbrId}</MbrId>
                                        <MerchantID>{merchantId}</MerchantID>
                                        <UserCode>{userCode}</UserCode>
                                        <UserPass>{userPass}</UserPass>
                                        <OrgOrderId>{request.OrderNumber}</OrgOrderId>
                                        <SecureType>NonSecure</SecureType>
                                        <TxnType>Refund</TxnType>
                                        <PurchAmount>{totalAmount}</PurchAmount>
                                        <Currency>{request.CurrencyIsoCode}</Currency>
                                        <Lang>{request.LanguageIsoCode.ToUpper()}</Lang>
                                    </PayforIade>";

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(requestXml, Encoding.UTF8, "text/xml"));
            string responseContent = await response.Content.ReadAsStringAsync();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);

            //TODO Finansbank response
            //if (xmlDocument.SelectSingleNode("VposResponse/ResultCode") == null ||
            //    xmlDocument.SelectSingleNode("VposResponse/ResultCode").InnerText != "0000")
            //{
            //    string errorMessage = xmlDocument.SelectSingleNode("VposResponse/ResultDetail")?.InnerText ?? string.Empty;
            //    if (string.IsNullOrEmpty(errorMessage))
            //        errorMessage = "Bankadan hata mesajı alınamadı.";

            //    return RefundPaymentResult.Failed(errorMessage);
            //}

            var transactionId = xmlDocument.SelectSingleNode("VposResponse/TransactionId")?.InnerText;
            return RefundPaymentResult.Successed(transactionId, transactionId);
        }

        public Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "mbrId", "" },
            { "merchantId", "" },
            { "userCode", "" },
            { "userPass", "" },
            { "txnType", "" },
            { "secureType", "" },
            { "gatewayUrl", "https://google.com" },
            { "verifyUrl", "https://google.com" },
        };
    }
}