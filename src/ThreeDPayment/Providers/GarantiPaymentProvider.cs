using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment.Providers
{
    public class GarantiPaymentProvider : IPaymentProvider
    {
        public Task<PaymentGatewayResult> ThreeDGatewayRequest(PaymentGatewayRequest request)
        {
            try
            {
                string terminalUserId = request.BankParameters["terminalUserId"];
                string terminalId = request.BankParameters["terminalId"];
                string terminalMerchantId = request.BankParameters["terminalMerchantId"];
                string terminalProvUserId = request.BankParameters["terminalProvUserId"];
                string terminalProvPassword = request.BankParameters["terminalProvPassword"];
                string storeKey = request.BankParameters["storeKey"];//garanti sanal pos ekranından üreteceğimiz güvenlik anahtarı
                string mode = request.BankParameters["mode"];//PROD | TEST
                string type = "sales";

                Dictionary<string, object> parameters = new Dictionary<string, object>();

                if (!request.CommonPaymentPage)
                {
                    parameters.Add("cardnumber", request.CardNumber);
                    parameters.Add("cardcvv2", request.CvvCode);//kart güvenlik kodu
                    parameters.Add("cardexpiredatemonth", request.ExpireMonth);//kart bitiş ay'ı
                    parameters.Add("cardexpiredateyear", request.ExpireYear);//kart bitiş yıl'ı
                }

                parameters.Add("secure3dsecuritylevel", "3D_PAY");//SMS onaylı ödeme modeli 3DPay olarak adlandırılıyor.
                parameters.Add("mode", mode);
                parameters.Add("apiversion", "v0.01");
                parameters.Add("terminalprovuserid", terminalProvUserId);
                parameters.Add("terminaluserid", terminalUserId);
                parameters.Add("terminalmerchantid", terminalMerchantId);
                parameters.Add("terminalid", terminalId);
                parameters.Add("txntype", type);//direk satış
                parameters.Add("txncurrencycode", request.CurrencyIsoCode);//TL ISO code | EURO 978 | Dolar 840
                parameters.Add("lang", request.LanguageIsoCode);
                parameters.Add("motoind", "N");
                parameters.Add("customeripaddress", request.CustomerIpAddress);
                parameters.Add("orderaddressname1", request.CardHolderName);
                parameters.Add("orderid", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("successurl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("errorurl", request.CallbackUrl);//hatalı dönüş adresi

                //garanti bankasında tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
                string amount = (request.TotalAmount * 100m).ToString("0.##", new CultureInfo("en-US"));//virgülden sonraki sıfırlara gerek yok
                parameters.Add("txnamount", amount);

                string installment = request.Installment.ToString();
                if (request.Installment <= 1)
                {
                    installment = string.Empty;//0 veya 1 olması durumunda taksit bilgisini boş gönderiyoruz
                }

                parameters.Add("txninstallmentcount", installment);//taksit sayısı | boş tek çekim olur

                //garanti tarafından terminal numarasını 9 haneye tamamlamak için başına sıfır eklenmesi isteniyor.
                string _terminalid = string.Format("{0:000000000}", int.Parse(terminalId));

                //provizyon şifresi ve 9 haneli terminal numarasının birleşimi ile bir hash oluşturuluyor
                string securityData = GetSHA1($"{terminalProvPassword}{_terminalid}");

                //ilgili veriler birleştirilip hash oluşturuluyor
                string hashstr = GetSHA1($"{terminalId}{request.OrderNumber}{amount}{request.CallbackUrl}{request.CallbackUrl}{type}{installment}{storeKey}{securityData}");
                parameters.Add("secure3dhash", hashstr);

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

            string mdStatus = form["mdstatus"].ToString();
            if (string.IsNullOrEmpty(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed(form["mderrormessage"], form["procreturncode"]));
            }

            StringValues response = form["response"];
            //mdstatus 1,2,3 veya 4 olursa 3D doğrulama geçildi anlamına geliyor
            if (!mdStatusCodes.Contains(mdStatus))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["mderrormessage"]}", form["procreturncode"]));
            }

            if (StringValues.IsNullOrEmpty(response) || !response.Equals("Approved"))
            {
                return Task.FromResult(VerifyGatewayResult.Failed($"{response} - {form["errmsg"]}", form["procreturncode"]));
            }

            int.TryParse(form["txninstallmentcount"], out int installment);

            return Task.FromResult(VerifyGatewayResult.Successed(form["transid"], form["hostrefnum"],
                installment, 0, response,
                form["procreturncode"], form["campaignchooselink"]));
        }

        private string GetSHA1(string text)
        {
            SHA1CryptoServiceProvider cryptoServiceProvider = new SHA1CryptoServiceProvider();
            byte[] inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(text));

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < inputbytes.Length; i++)
            {
                builder.Append(string.Format("{0,2:x}", inputbytes[i]).Replace(" ", "0"));
            }

            return builder.ToString().ToUpper();
        }

        public Dictionary<string, string> TestParameters => new Dictionary<string, string>
        {
            { "terminalUserId", "1" },
            { "terminalId", "1" },
            { "terminalMerchantId", "1" },
            { "terminalProvUserId", "1" },
            { "terminalProvPassword", "1" },
            { "storeKey", "1" },
            { "mode", "TEST" },
            { "gatewayUrl", "https://sanalposprov.garanti.com.tr/VPServlet" },
            { "verifyUrl", "https://sanalposprov.garanti.com.tr/VPServlet" }
        };

        private static readonly string[] mdStatusCodes = new[] { "1", "2", "3", "4" };
    }
}