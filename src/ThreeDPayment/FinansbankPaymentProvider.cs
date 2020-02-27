using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThreeDPayment
{
    public class FinansbankPaymentProvider : IPaymentProvider
    {
        public PaymentParameterResult GetPaymentParameters(PaymentRequest request)
        {
            string processType = "Auth";//İşlem tipi
            string mbrId = "";//Mağaza numarası
            string merchantId = "";//Mağaza numarası
            string userCode = "";//
            string userPass = "";//Mağaza anahtarı
            string storeType = "3Dpay";//SMS onaylı ödeme modeli 3DPay olarak adlandırılıyor.
            string successUrl = "https://localhost:5001/home/callback";//Başarılı Url
            string failUrl = "https://localhost:5001/home/callback";//Hata Url

            var parameterResult = new PaymentParameterResult();
            try
            {
                var parameters = new Dictionary<string, object>();
                parameters.Add("MbrId", mbrId);
                parameters.Add("MerchantId", merchantId);
                parameters.Add("UserCode", userCode);
                parameters.Add("UserPass", userPass);
                parameters.Add("PurchAmount", request.TotalAmount.ToString(new CultureInfo("en-US")));//kuruş ayrımı nokta olmalı!!!
                parameters.Add("OrderId", request.OrderNumber);//sipariş numarası

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("OkUrl", successUrl);//başarılı dönüş adresi
                parameters.Add("FailUrl", failUrl);//hatalı dönüş adresi
                parameters.Add("TxnType", processType);//direk satış
                parameters.Add("InstallmentCount", request.Installment);//taksit sayısı | 0, 1 veya boş tek çekim olur
                parameters.Add("Currency", request.CurrencyIsoCode);//TL:949, USD:840, EUR:978

                //kart numarasından çizgi ve boşlukları kaldırıyoruz
                string cardNumber = request.CardNumber.Replace("-", string.Empty);
                cardNumber = cardNumber.Replace(" ", string.Empty).Trim();
                parameters.Add("Pan", cardNumber);//kart numarası

                parameters.Add("Expiry", request.ExpireMonth + request.ExpireYear);//kart bitiş ay'ı ve yıl'ı
                parameters.Add("Cvv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("SecureType", storeType);//NonSecure, 3Dpay, 3DModel, 3DHost
                parameters.Add("Lang", request.LanguageIsoCode.ToUpper());//iki haneli dil iso kodu

                parameterResult.Parameters = parameters;
                parameterResult.Success = true;

                //yeni finans bank test ve canlı ortam 3dgate adresi
                parameterResult.PaymentUrl = new Uri("https://finansbank.com");
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

            string merchantId = "";//Mağaza numarası
            string merchantPass = "";//Mağaza numarası

            var hashstr = merchantId + merchantPass + form["OrderId"] + form["AuthCode"] + form["ProcReturnCode"] + form["ResponseRnd"];
            var cryptoServiceProvider = new SHA1CryptoServiceProvider();
            var inputbytes = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(hashstr));
            var hashData = Convert.ToBase64String(inputbytes);

            if (!hashData.Equals(form["ResponseHash"]))
            {
                paymentResult.ErrorMessage = "3D güvenlik imzası geçersiz.";
                return paymentResult;
            }

            var result = form["TxnResult"];
            if (StringValues.IsNullOrEmpty(result) || !result.Equals("Approved"))
            {
                paymentResult.ErrorMessage = $"{result} - {form["ErrorMessage"]}";
                paymentResult.ErrorCode = form["ProcReturnCode"];
                return paymentResult;
            }

            paymentResult.Success = true;
            paymentResult.ResponseCode = form["ProcReturnCode"];
            paymentResult.TransactionId = form["OrderId"];
            paymentResult.ErrorMessage = $"{result} - {form["ErrMsg"]}";

            return paymentResult;
        }
    }
}