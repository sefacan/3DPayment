using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ThreeDPayment.Models;

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

                var parameters = new Dictionary<string, object>();
                parameters.Add("ShopCode", shopCode);
                parameters.Add("PurchAmount", totalAmount);//kuruş ayrımı nokta olmalı!!!
                parameters.Add("OrderId", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("OkUrl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("FailUrl", request.CallbackUrl);//hatalı dönüş adresi
                parameters.Add("TxnType", txnType);//direk satış
                parameters.Add("taksitsayisi", request.Installment);//taksit sayısı | 1 veya boş tek çekim olur
                parameters.Add("Rnd", random);//rastgele bir sayı üretilmesi isteniyor

                string hashstr = $"{shopCode}{request.OrderNumber}{totalAmount}{request.CallbackUrl}{request.CallbackUrl}{txnType}{request.Installment}{random}{storeKey}";
                var cryptoServiceProvider = new SHA1CryptoServiceProvider();
                var inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(hashstr));
                var hashData = Convert.ToBase64String(inputbytes);

                parameters.Add("Hash", hashData);//hash data
                parameters.Add("Currency", request.CurrencyIsoCode);//TL ISO code | EURO 978 | Dolar 840
                parameters.Add("Pan", request.CardNumber);

                parameters.Add("Expiry", $"{request.ExpireMonth}{request.ExpireYear}");//kart bitiş ay-yıl birleşik
                parameters.Add("Cvv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("CartType", request.CartType);//kart tipi visa 1 | master 2 | amex 3
                parameters.Add("SecureType", secureType);
                parameters.Add("Lang", request.LanguageIsoCode.ToUpper());//iki haneli dil iso kodu

                return Task.FromResult(PaymentGatewayResult.Successed(parameters, request.BankParameters["gatewayUrl"]));
            }
            catch (Exception ex)
            {
                return Task.FromResult(PaymentGatewayResult.Failed(ex.ToString()));
            }
        }

        public Task<VerifyGatewayResult> VerifyGateway(VerifyGatewayRequest request, IFormCollection form)
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

            return Task.FromResult(VerifyGatewayResult.Successed(form["TransId"], form["TransId"],
                int.Parse(form["taksitsayisi"]), response,
                form["ProcReturnCode"]));
        }

        public async Task<CancelPaymentResult> CancelRequest(CancelPaymentRequest request)
        {
            string shopCode = request.BankParameters["shopCode"];
            string userCode = request.BankParameters["userCode"];
            string userPass = request.BankParameters["userPass"];

            var formBuilder = new StringBuilder();
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

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(formBuilder.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded"));
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseContent))
                return CancelPaymentResult.Failed("İptal işlemi başarısız.");

            responseContent = responseContent.Replace(";;", ";").Replace(";", "&");
            var responseParams = HttpUtility.ParseQueryString(responseContent);

            if (responseParams["ProcReturnCode"] != "00")
                return CancelPaymentResult.Failed(responseParams["ErrorMessage"]);

            return CancelPaymentResult.Successed(responseParams["TransId"]);
        }

        public async Task<RefundPaymentResult> RefundRequest(RefundPaymentRequest request)
        {
            string shopCode = request.BankParameters["shopCode"];
            string userCode = request.BankParameters["userCode"];
            string userPass = request.BankParameters["userPass"];

            var formBuilder = new StringBuilder();
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

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(formBuilder.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded"));
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseContent))
                return RefundPaymentResult.Failed("İade işlemi başarısız.");

            responseContent = responseContent.Replace(";;", ";").Replace(";", "&");
            var responseParams = HttpUtility.ParseQueryString(responseContent);

            if (responseParams["ProcReturnCode"] != "00")
                return RefundPaymentResult.Failed(responseParams["ErrorMessage"]);

            return RefundPaymentResult.Successed(responseParams["TransId"]);
        }

        public async Task<PaymentDetailResult> PaymentDetailRequest(PaymentDetailRequest request)
        {
            string shopCode = request.BankParameters["shopCode"];
            string userCode = request.BankParameters["userCode"];
            string userPass = request.BankParameters["userPass"];

            var formBuilder = new StringBuilder();
            formBuilder.AppendFormat("ShopCode={0}&", shopCode);
            formBuilder.AppendFormat("Currency={0}&", request.CurrencyIsoCode);
            formBuilder.Append("TxnType=StatusHistory&");
            formBuilder.AppendFormat("orgOrderId={0}&", request.OrderNumber);
            formBuilder.AppendFormat("UserCode={0}&", userCode);
            formBuilder.AppendFormat("UserPass={0}&", userPass);
            formBuilder.Append("SecureType=NonSecure&");
            formBuilder.AppendFormat("Lang={0}&", request.LanguageIsoCode.ToUpper());

            var response = await client.PostAsync(request.BankParameters["verifyUrl"], new StringContent(formBuilder.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded"));
            string responseContent = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseContent))
                return PaymentDetailResult.FailedResult(errorMessage: "İade işlemi başarısız.");

            responseContent = responseContent.Replace(";;", ";").Replace(";", "&");
            var responseParams = HttpUtility.ParseQueryString(responseContent);

            if (responseParams["ProcReturnCode"] != "00")
                return PaymentDetailResult.FailedResult(errorMessage: responseParams["ErrorMessage"], errorCode: responseParams["ErrorCode"]);

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

        private static readonly IDictionary<string, string> ErrorCodes = new Dictionary<string, string>
        {
            { "00", "Onaylandı" },
            { "01", "Bankanızı Arayın" },
            { "02", "Bankanızı Arayın(Özel)" },
            { "03", "Geçersiz Üye İşyeri" },
            { "04", "Karta El Koy" },
            { "05", "Onaylanmadı" },
            { "06", "Stop List Bildirim Hatası" },
            { "07", "Karta El Koy(Özel)" },
            { "08", "Kimlik Sorgula" },
            { "09", "Tekrar Deneyin" },
            { "11", "Onaylandı(VIP)" },
            { "12", "Geçersiz İşlem" },
            { "13", "Geçersiz Tutar" },
            { "14", "Geçersiz Hesap Numarası" },
            { "15", "Tanımsız Issuer" },
            { "25", "Kayıt Dosyada Bulunamadı" },
            { "28", "Orjinal Reddedildi" },
            { "29", "Orjinal Bulunmadı" },
            { "30", "Mesaj Hatası" },
            { "33", "Süresi Dolmuş Kart, El Koy" },
            { "36", "Kısıtlı Kart, El Koy" },
            { "38", "PIN Deneme Sayısı Aşıldı" },
            { "41", "Kayıp Kart, El Koy" },
            { "43", "Çalıntı Kart, El Koy" },
            { "51", "Limit Yetersiz" },
            { "52", "Tanımlı Hesap Yok" },
            { "53", "Tanımlı Hesap Yok" },
            { "54", "Süresi Dolmus Kart" },
            { "55", "Yanlış PIN" },
            { "57", "Karta İzin Verilmeyen İşlem" },
            { "58", "POSa İzin Verilmeyen İşlem" },
            { "61", "Para Çekme Limiti Aşıldı" },
            { "62", "Sınırlı Kart" },
            { "63", "Güvenlik İhlali" },
            { "65", "Para Çekme Limiti Aşıldı" },
            { "75", "PIN Deneme Limiti Aşıldı" },
            { "76", "Key Senkronizasyon Hatası" },
            { "77", "Red, Script Yok" },
            { "78", "Güvenli Olmayan PIN" },
            { "79", "ARQC Hatası" },
            { "81", "Aygıt Versiyon Uyuşmazlığı" },
            { "85", "Onaylandı" },
            { "91", "Issuer Çalışmıyor" },
            { "92", "Finansal Kurum Tanımıyor" },
            { "95", "POS Günsonu Hatası" },
            { "96", "Sistem Hatası" }
        };
    }
}