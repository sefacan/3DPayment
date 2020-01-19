using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThreeDPayment.Payment
{
    public class AssecoPaymentProvider : IPaymentProvider
    {
        public PaymentParameterResult GetPaymentParameters(PaymentRequest request)
        {
            string processType = "Auth";//İşlem tipi
            string clientId = "700655000200";//Mağaza numarası
            string storeKey = "TRPS1234";//Mağaza anahtarı
            string storeType = "3D_PAY";//SMS onaylı ödeme modeli 3DPay olarak adlandırılıyor.
            string successUrl = "https://localhost:5001/home/callback";//Başarılı Url
            string failUrl = "https://localhost:5001/home/callback";//Hata Url
            string random = DateTime.Now.ToString();

            var parameterResult = new PaymentParameterResult();
            try
            {
                var parameters = new Dictionary<string, object>();
                parameters.Add("clientid", clientId);
                parameters.Add("amount", request.TotalAmount.ToString(new CultureInfo("en-US")));//kuruş ayrımı nokta olmalı!!!
                parameters.Add("oid", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("okUrl", successUrl);//başarılı dönüş adresi
                parameters.Add("failUrl", failUrl);//hatalı dönüş adresi
                parameters.Add("islemtipi", processType);//direk satış
                parameters.Add("taksit", request.Installment);//taksit sayısı | 1 veya boş tek çekim olur
                parameters.Add("rnd", random);//rastgele bir sayı üretilmesi isteniyor

                string hashstr = clientId + request.OrderNumber + request.TotalAmount + successUrl + failUrl + processType + request.Installment + random + storeKey;
                var cryptoServiceProvider = new SHA1CryptoServiceProvider();
                var inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(hashstr));
                var hashData = Convert.ToBase64String(inputbytes);

                parameters.Add("hash", hashData);//hash data
                parameters.Add("currency", request.CurrencyIsoCode);//TL ISO code | EURO 978 | Dolar 840

                //kart numarasından çizgi ve boşlukları kaldırıyoruz
                string cardNumber = request.CardNumber.Replace("-", string.Empty);
                cardNumber = cardNumber.Replace(" ", string.Empty).Trim();
                parameters.Add("pan", cardNumber);

                parameters.Add("cardHolderName", request.CardHolderName);
                parameters.Add("Ecom_Payment_Card_ExpDate_Month", request.ExpireMonth);//kart bitiş ay'ı
                parameters.Add("Ecom_Payment_Card_ExpDate_Year", request.ExpireYear);//kart bitiş yıl'ı
                parameters.Add("cv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("cardType", "1");//kart tipi visa 1 | master 2 | amex 3
                parameters.Add("storetype", storeType);
                parameters.Add("lang", request.LanguageIsoCode);//iki haneli dil iso kodu

                parameterResult.Parameters = parameters;
                parameterResult.Success = true;

                //İş Bankası Canlı https://sanalpos.isbank.com.tr/fim/est3Dgate
                parameterResult.PaymentUrl = new Uri("https://entegrasyon.asseco-see.com.tr/fim/est3Dgate");
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