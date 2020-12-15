using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment.Providers
{
    public class DenizbankPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient client;

        public DenizbankPaymentProvider(IHttpClientFactory httpClientFactory)
        {
            client = httpClientFactory.CreateClient();
        }

        public Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            try
            {
                string shopCode = request.BankParameters["shopCode"];
                string txnType = request.BankParameters["txnType"];
                string storeKey = request.BankParameters["storeKey"];
                string secureType = request.BankParameters["secureType"];
                string totalAmount = request.TotalAmount.ToString(new CultureInfo("en-US"));
                string random = DateTime.Now.ToString();

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("ShopCode", shopCode);
                parameters.Add("PurchAmount", totalAmount);//kuruş ayrımı nokta olmalı!!!
                parameters.Add("OrderId", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("OkUrl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("FailUrl", request.CallbackUrl);//hatalı dönüş adresi
                parameters.Add("TxnType", txnType);//direk satış
                parameters.Add("Rnd", random);//rastgele bir sayı üretilmesi isteniyor

                //üretici kartı taksit desteği
                if (request.ManufacturerCard)
                {
                    parameters.Add("AgricultureTxnFlag", "T");
                    parameters.Add("PaymentFrequency", request.Installment);
                    parameters.Add("MaturityPeriod", request.Installment);
                }
                else
                {
                    //normal taksit
                    parameters.Add("InstallmentCount", request.Installment);//taksit sayısı | 1 veya boş tek çekim olur
                }

                string hashstr = $"{shopCode}{request.OrderNumber}{totalAmount}{request.CallbackUrl}{request.CallbackUrl}{txnType}{request.Installment}{random}{storeKey}";
                SHA1CryptoServiceProvider cryptoServiceProvider = new SHA1CryptoServiceProvider();
                byte[] inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(hashstr));
                string hashData = Convert.ToBase64String(inputbytes);

                parameters.Add("Hash", hashData);//hash data
                parameters.Add("Currency", request.CurrencyIsoCode);//TL ISO code | EURO 978 | Dolar 840
                parameters.Add("Pan", request.CardNumber);

                parameters.Add("Expiry", $"{request.ExpireMonth}{request.ExpireYear}");//kart bitiş ay-yıl birleşik
                parameters.Add("Cvv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("CartType", request.CardType);//kart tipi visa 1 | master 2 | amex 3
                parameters.Add("SecureType", secureType);
                parameters.Add("Lang", request.LanguageIsoCode.ToUpper());//iki haneli dil iso kodu

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

            string mdStatus = form["mdStatus"].ToString();
            if (string.IsNullOrEmpty(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed(form["mdErrorMsg"], form["ProcReturnCode"]));
            }

            string response = form["Response"].ToString();
            //mdstatus 1,2,3 veya 4 olursa 3D doğrulama geçildi anlamına geliyor
            if (!mdStatusCodes.Contains(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["mdErrorMsg"]}", form["ProcReturnCode"]));
            }

            if (string.IsNullOrEmpty(response) || !response.Equals("Approved"))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["ErrMsg"]}", form["ProcReturnCode"]));
            }

            int.TryParse(form["taksitsayisi"], out int taksitSayisi);
            int.TryParse(form["EXTRA.ARTITAKSIT"], out int extraTaksitSayisi);

            return Task.FromResult(VerifyGatewayResult.Successed(form["TransId"], form["TransId"],
                taksitSayisi, extraTaksitSayisi,
                response, form["ProcReturnCode"]));
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string shopCode = request.BankParameters["shopCode"];
            string userCode = request.BankParameters["cancelUserCode"];
            string userPass = request.BankParameters["cancelUserPass"];

            StringBuilder formBuilder = new StringBuilder();
            formBuilder.AppendFormat("ShopCode={0}&", shopCode);
            formBuilder.AppendFormat("PurchAmount={0}&", request.TotalAmount.ToString(new CultureInfo("en-US")));
            formBuilder.AppendFormat("Currency={0}&", request.CurrencyIsoCode);
            formBuilder.Append("OrderId=&");
            formBuilder.Append("TxnType=Void&");
            formBuilder.AppendFormat("orgOrderId={0}&", request.OrderNumber);
            formBuilder.AppendFormat("UserCode={0}&", userCode);
            formBuilder.AppendFormat("UserPass={0}&", userPass);
            formBuilder.Append("SecureType=NonSecure&");
            formBuilder.AppendFormat("Lang={0}&", request.LanguageIsoCode.ToUpper());
            formBuilder.Append("MOTO=0");

            HttpResponseMessage response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(formBuilder.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded"));
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseContent))
            {
                return CancelPaymentResult.Failed("İptal işlemi başarısız.");
            }

            responseContent = responseContent.Replace(";;", ";").Replace(";", "&");
            System.Collections.Specialized.NameValueCollection responseParams = HttpUtility.ParseQueryString(responseContent);

            if (responseParams["ProcReturnCode"] != "00")
            {
                return CancelPaymentResult.Failed(responseParams["ErrorMessage"]);
            }

            return CancelPaymentResult.Successed(responseParams["TransId"], responseParams["TransId"]);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string shopCode = request.BankParameters["shopCode"];
            string userCode = request.BankParameters["refundUserCode"];
            string userPass = request.BankParameters["refundUserPass"];

            StringBuilder formBuilder = new StringBuilder();
            formBuilder.AppendFormat("ShopCode={0}&", shopCode);
            formBuilder.AppendFormat("PurchAmount={0}&", request.TotalAmount.ToString(new CultureInfo("en-US")));
            formBuilder.AppendFormat("Currency={0}&", request.CurrencyIsoCode);
            formBuilder.Append("OrderId=&");
            formBuilder.Append("TxnType=Refund&");
            formBuilder.AppendFormat("orgOrderId={0}&", request.OrderNumber);
            formBuilder.AppendFormat("UserCode={0}&", userCode);
            formBuilder.AppendFormat("UserPass={0}&", userPass);
            formBuilder.Append("SecureType=NonSecure&");
            formBuilder.AppendFormat("Lang={0}&", request.LanguageIsoCode.ToUpper());
            formBuilder.Append("MOTO=0");

            HttpResponseMessage response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(formBuilder.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded"));
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseContent))
            {
                return RefundPaymentResult.Failed("İade işlemi başarısız.");
            }

            responseContent = responseContent.Replace(";;", ";").Replace(";", "&");
            System.Collections.Specialized.NameValueCollection responseParams = HttpUtility.ParseQueryString(responseContent);

            if (responseParams["ProcReturnCode"] != "00")
            {
                return RefundPaymentResult.Failed(responseParams["ErrorMessage"]);
            }

            return RefundPaymentResult.Successed(responseParams["TransId"], responseParams["TransId"]);
        }

        public async Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            string shopCode = request.BankParameters["shopCode"];
            string userCode = request.BankParameters["userCode"];
            string userPass = request.BankParameters["userPass"];

            StringBuilder formBuilder = new StringBuilder();
            formBuilder.AppendFormat("ShopCode={0}&", shopCode);
            formBuilder.AppendFormat("Currency={0}&", request.CurrencyIsoCode);
            formBuilder.Append("TxnType=StatusHistory&");
            formBuilder.AppendFormat("orgOrderId={0}&", request.OrderNumber);
            formBuilder.AppendFormat("UserCode={0}&", userCode);
            formBuilder.AppendFormat("UserPass={0}&", userPass);
            formBuilder.Append("SecureType=NonSecure&");
            formBuilder.AppendFormat("Lang={0}&", request.LanguageIsoCode.ToUpper());

            HttpResponseMessage response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(formBuilder.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded"));
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseContent))
            {
                return PaymentDetailResult.FailedResult(errorMessage: "İade işlemi başarısız.");
            }

            responseContent = responseContent.Replace(";;", ";").Replace(";", "&");
            System.Collections.Specialized.NameValueCollection responseParams = HttpUtility.ParseQueryString(responseContent);

            if (responseParams["ProcReturnCode"] != "00")
            {
                return PaymentDetailResult.FailedResult(errorMessage: responseParams["ErrorMessage"], errorCode: responseParams["ErrorCode"]);
            }

            return PaymentDetailResult.PaidResult(responseParams["TransId"], responseParams["TransId"]);
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "shopCode", "" },
            { "txnType", "" },
            { "storeKey", "" },
            { "secureType", "" },
            { "gatewayUrl", "https://spos.denizbank.com/mpi/Default.aspx" },
            { "userCode", "" },
            { "userPass", "" },
            { "verifyUrl", "https://spos.denizbank.com/mpi/Default.aspx" }
        };

        private static readonly string[] mdStatusCodes = new[] { "1", "2", "3", "4" };
    }
}