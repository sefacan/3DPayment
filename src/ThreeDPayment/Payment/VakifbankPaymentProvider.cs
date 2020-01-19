using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Xml;

namespace ThreeDPayment.Payment
{
    /// <summary>
    /// Vakıfbank sanal pos işlemleri asseco gibi yöntemlere göre biraz daha farklı.
    /// Vakıfbank girilen kart bilgisinin 3D doğrulamasını yapıp eğer sonuç başarılıysa banka sms sayfasına yönlendirme yapılmasını istiyor.
    /// Kart bilgisi 3D ödeme için uygun olması durumunda yönlendirilecek sayfa bilgisini bize xml içerisinde dönüyor
    /// </summary>
    public class VakifbankPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public VakifbankPaymentProvider(IHttpClientFactory httpClientFactory)
        {
            client = httpClientFactory.CreateClient();
        }

        public PaymentParameterResult GetPaymentParameters(PaymentRequest request)
        {
            var parameterResult = new PaymentParameterResult();

            string merchantId = "000100000013506";
            string merchantPassword = "123456";
            string successUrl = "https://localhost:5001/home/callback";//Başarılı Url
            string failUrl = "https://localhost:5001/home/callback";//Hata Url

            try
            {
                var httpParameters = new Dictionary<string, string>();

                //kart numarasından çizgi ve boşlukları kaldırıyoruz
                string cardNumber = request.CardNumber.Replace("-", string.Empty);
                cardNumber = cardNumber.Replace(" ", string.Empty).Trim();

                httpParameters.Add("Pan", cardNumber);
                httpParameters.Add("ExpiryDate", $"{request.ExpireMonth}{request.ExpireYear}");
                httpParameters.Add("PurchaseAmount", request.TotalAmount.ToString(new CultureInfo("en-US")));
                httpParameters.Add("Currency", request.CurrencyIsoCode);//TL 949 | EURO 978 | Dolar 840

                /*
                 * Visa 100
                 * Master Card 200
                 * American Express 300
                */
                httpParameters.Add("BrandName", "100");
                httpParameters.Add("VerifyEnrollmentRequestId", request.OrderNumber);//sipariş numarası
                httpParameters.Add("SessionInfo", "1");//banka dökümanları sabit bir değer
                httpParameters.Add("MerchantID", merchantId);
                httpParameters.Add("MerchantPassword", merchantPassword);
                httpParameters.Add("SuccessUrl", successUrl);
                httpParameters.Add("FailureUrl", failUrl);
                httpParameters.Add("InstallmentCount", request.Installment.ToString());

                //Canlı https://3dsecure.vakifbank.com.tr:4443/MPIAPI/MPI_Enrollment.aspx
                var enrollmentUrl = new Uri("https://3dsecuretest.vakifbank.com.tr:4443/MPIAPI/MPI_Enrollment.aspx");
                var response = client.PostAsync(enrollmentUrl, new FormUrlEncodedContent(httpParameters)).GetAwaiter().GetResult();
                string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(responseContent);
                var statusNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/Status");
                if (statusNode.InnerText != "Y")
                {
                    var messageErrorCodeNode = xmlDocument.SelectSingleNode("IPaySecure/MessageErrorCode");
                    var messageErrorNode = xmlDocument.SelectSingleNode("IPaySecure/ErrorMessage");
                    parameterResult.ErrorMessage = $"{messageErrorNode.InnerText} - {messageErrorCodeNode?.InnerText}";
                    parameterResult.Success = false;

                    return parameterResult;
                }

                var pareqNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/PaReq");
                var acsUrlNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/ACSUrl");
                var termUrlNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/TermUrl");
                var mdNode = xmlDocument.SelectSingleNode("IPaySecure/Message/VERes/MD");

                var parameters = new Dictionary<string, object>();
                parameters.Add("PaReq", pareqNode.InnerText);
                parameters.Add("TermUrl", termUrlNode.InnerText);
                parameters.Add("MD", mdNode.InnerText);

                parameterResult.Parameters = parameters;
                parameterResult.Success = true;

                //form post edilecek url xml response içerisinde bankadan dönüyor
                parameterResult.PaymentUrl = new Uri(acsUrlNode.InnerText);
            }
            catch (Exception ex)
            {
                parameterResult.Success = false;
                parameterResult.ErrorMessage = ex.ToString();
            }

            return parameterResult;
        }

        public PaymentResult GetPaymentResult(IFormCollection form)
        {
            var paymentResult = new PaymentResult();
            if (form == null)
            {
                paymentResult.ErrorMessage = "Form verisi alınamadı.";
                return paymentResult;
            }

            var status = form["Status"];
            if (StringValues.IsNullOrEmpty(status))
            {
                paymentResult.ErrorMessage = form["mdErrorMsg"];
                paymentResult.ErrorCode = form["ProcReturnCode"];
                return paymentResult;
            }

            if (status != "Y")
            {

            }

            string requestXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                                        "<VposRequest>" +
                                                        "<MerchantId>" + form["MerchantId"] + "</MerchantId>" +
                                                        "<Password>" + form["get724_merchant_password"] + "</Password>" +
                                                        "<TerminalNo>" + form["get724_user_name"] + "</TerminalNo>" +
                                                        "<Pan>" + form["Pan"] + "</Pan>" +
                                                        "<Expiry>" + form["expire_date"] + "</Expiry>" +
                                                        "<CurrencyAmount>" + form["amount"] + "</CurrencyAmount>" +
                                                        "<CurrencyCode>" + form["PurchCurrency"] + "</CurrencyCode>" +
                                                        "<TransactionType>Sale</TransactionType>" +
                                                        "<TransactionId></TransactionId>";

            //boş veya 0 olan taksit bilgisini gönderme
            if (int.TryParse(form["InstallmentCount"], out int installment) && installment > 0)
                requestXml += "<NumberOfInstallments>" + installment + "</NumberOfInstallments>";

            //Canlı https://onlineodeme.vakifbank.com.tr:4443/VposService/v3/Vposreq.aspx
            string requestUrl = "https://onlineodemetest.vakifbank.com.tr:4443/VposService/v3/Vposreq.aspx";//TEST

            var parameters = new Dictionary<string, string>();
            parameters.Add("prmstr", requestXml);
            var response = client.PostAsync(requestUrl, new FormUrlEncodedContent(parameters)).GetAwaiter().GetResult();
            string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(responseContent);
            var resultCodeNode = xmlDocument.SelectSingleNode("VposResponse/ResultCode");
            var resultDetailNode = xmlDocument.SelectSingleNode("VposResponse/ResultDetail");
            if (resultCodeNode.InnerText != "0000")
            {
                paymentResult.ErrorMessage = resultDetailNode.InnerText;
                paymentResult.ErrorCode = resultCodeNode.InnerText;
                return paymentResult;
            }

            paymentResult.Success = true;
            paymentResult.ResponseCode = resultCodeNode.InnerText;
            paymentResult.TransactionId = form["Xid"];
            paymentResult.ErrorMessage = resultDetailNode.InnerText;
            paymentResult.ErrorCode = resultCodeNode.InnerText;

            return paymentResult;
        }
    }
}