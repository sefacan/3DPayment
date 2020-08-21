using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThreeDPayment
{
    public class GarantiPaymentProvider : IPaymentProvider
    {
        public PaymentParameterResult GetPaymentParameters(PaymentRequest request)
        {
            string terminaluserid = "TEST";
            string terminalid = "30691298";
            string terminalmerchantid = "7000679";
            string terminalprovuserid = "PROVAUT";
            string terminalprovpassword = "123qweASD/";
            string storekey = "12345678";//garanti sanal pos ekranından üreteceğimiz güvenlik anahtarı
            string mode = "TEST";//PROD | TEST
            string successUrl = "https://localhost:5001/home/callback";//Başarılı Url
            string errorurl = "https://localhost:5001/home/callback";//Hata Url
            string type = "sales";

            try
            {
                var parameters = new Dictionary<string, object>();

                //kart numarasından çizgi ve boşlukları kaldırıyoruz
                string cardNumber = request.CardNumber.Replace("-", string.Empty);
                cardNumber = cardNumber.Replace(" ", string.Empty).Trim();
                parameters.Add("cardnumber", cardNumber);

                parameters.Add("cardcvv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("cardexpiredatemonth", request.ExpireMonth);//kart bitiş ay'ı
                parameters.Add("cardexpiredateyear", request.ExpireYear);//kart bitiş yıl'ı
                parameters.Add("secure3dsecuritylevel", "3D_PAY");//SMS onaylı ödeme modeli 3DPay olarak adlandırılıyor.
                parameters.Add("mode", mode);
                parameters.Add("apiversion", "v0.01");
                parameters.Add("terminalprovuserid", terminalprovuserid);
                parameters.Add("terminaluserid", terminaluserid);
                parameters.Add("terminalmerchantid", terminalmerchantid);
                parameters.Add("terminalid", terminalid);
                parameters.Add("txntype", type);//direk satış
                parameters.Add("txncurrencycode", request.CurrencyIsoCode);//TL ISO code | EURO 978 | Dolar 840
                parameters.Add("motoind", "N");
                parameters.Add("customeripaddress", request.CustomerIpAddress);
                parameters.Add("orderaddressname1", request.CardHolderName);
                parameters.Add("orderid", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("successurl", successUrl);//başarılı dönüş adresi
                parameters.Add("errorurl", errorurl);//hatalı dönüş adresi

                //garanti bankasında tutar bilgisinde nokta, virgül gibi değerler istenmiyor. 1.10 TL'lik işlem 110 olarak gönderilmeli. Yani tutarı 100 ile çarpabiliriz.
                string amount = (request.TotalAmount * 100m).ToString("N");//virgülden sonraki sıfırlara gerek yok
                parameters.Add("txnamount", amount);

                string installment = request.Installment.ToString();
                if (request.Installment <= 1)
                    installment = string.Empty;//0 veya 1 olması durumunda taksit bilgisini boş gönderiyoruz

                parameters.Add("txninstallmentcount", installment);//taksit sayısı | boş tek çekim olur

                //garanti tarafından terminal numarasını 9 haneye tamamlamak için başına sıfır eklenmesi isteniyor. 9 haneli bir terminal numarasında buna ihtiyaç olmuyor.
                string _terminalid = "0" + terminalid;
                string securityData = GetSHA1(terminalprovpassword + _terminalid).ToUpper();//provizyon şifresi ve 9 haneli terminal numarasının birleşimi ile bir hash oluşturuluyor
                string hashstr = terminalid + request.OrderNumber + amount + successUrl + errorurl + type + installment + storekey + securityData;//ilgili veriler birleştirilip hash oluşturuluyor
                parameters.Add("secure3dhash", GetSHA1(hashstr).ToUpper());//ToUpper ile tüm karakterlerin büyük harf olması gerekiyor

                //Garanti Canlı https://sanalposprov.garanti.com.tr/servlet/gt3dengine

                return PaymentParameterResult.Successed(parameters, "https://sanalposprovtest.garanti.com.tr/servlet/gt3dengine");
            }
            catch (Exception ex)
            {
                return PaymentParameterResult.Failed(ex.ToString());
            }
        }

        private string GetSHA1(string text)
        {
            var cryptoServiceProvider = new SHA1CryptoServiceProvider();
            var inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(text));

            var builder = new StringBuilder();
            for (int i = 0; i < inputbytes.Length; i++)
            {
                builder.Append(string.Format("{0,2:x}", inputbytes[i]).Replace(" ", "0"));
            }

            return builder.ToString();
        }

        public PaymentResult GetPaymentResult(IFormCollection form)
        {
            var paymentResult = new PaymentResult();
            if (form == null)
            {
                paymentResult.ErrorMessage = "Form verisi alınamadı.";
                return paymentResult;
            }

            var mdStatus = form["mdStatus"];
            if (StringValues.IsNullOrEmpty(mdStatus))
            {
                paymentResult.ErrorMessage = form["mdErrorMsg"];
                paymentResult.ErrorCode = form["ProcReturnCode"];
                return paymentResult;
            }

            var response = form["Response"];
            //mdstatus 1,2,3 veya 4 olursa 3D doğrulama geçildi anlamına geliyor
            if (!mdStatus.Equals("1") || !mdStatus.Equals("2") || !mdStatus.Equals("3") || !mdStatus.Equals("4"))
            {
                paymentResult.ErrorMessage = $"{response} - {form["mdErrorMsg"]}";
                paymentResult.ErrorCode = form["ProcReturnCode"];
                return paymentResult;
            }

            if (StringValues.IsNullOrEmpty(response) || !response.Equals("Approved"))
            {
                paymentResult.ErrorMessage = $"{response} - {form["ErrMsg"]}";
                paymentResult.ErrorCode = form["ProcReturnCode"];
                return paymentResult;
            }

            paymentResult.Success = true;
            paymentResult.ResponseCode = mdStatus;
            paymentResult.TransactionId = form["TransId"];
            paymentResult.ErrorMessage = $"{response} - {form["ErrMsg"]}";

            return paymentResult;
        }
    }
}