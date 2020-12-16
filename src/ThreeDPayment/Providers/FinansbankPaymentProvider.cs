using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ThreeDPayment.Requests;
using ThreeDPayment.Results;

namespace ThreeDPayment.Providers
{
    public class FinansbankPaymentProvider : IPaymentProvider
    {
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

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("MbrId", mbrId);
                parameters.Add("MerchantId", merchantId);
                parameters.Add("UserCode", userCode);
                parameters.Add("UserPass", userPass);
                parameters.Add("PurchAmount", totalAmount);//kuruş ayrımı nokta olmalı!!!
                parameters.Add("Currency", request.CurrencyIsoCode);//TL:949, USD:840, EUR:978
                parameters.Add("OrderId", request.OrderNumber);//sipariş numarası
                parameters.Add("InstallmentCount", request.Installment);//taksit sayısı | 0, 1 veya boş tek çekim olur
                parameters.Add("TxnType", txnType);//direk satış
                parameters.Add("SecureType", secureType);//NonSecure, 3Dpay, 3DModel, 3DHost
                parameters.Add("Pan", request.CardNumber);//kart numarası
                parameters.Add("Expiry", $"{request.ExpireMonth}{request.ExpireYear}");//kart bitiş ay-yıl birleşik
                parameters.Add("Cvv2", request.CvvCode);//kart güvenlik kodu
                parameters.Add("Lang", request.LanguageIsoCode);//iki haneli dil iso kodu

                //işlem başarılı da olsa başarısız da olsa callback sayfasına yönlendirerek kendi tarafımızda işlem sonucunu kontrol ediyoruz
                parameters.Add("OkUrl", request.CallbackUrl);//başarılı dönüş adresi
                parameters.Add("FailUrl", request.CallbackUrl);//hatalı dönüş adresi

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

        private static readonly string[] mdStatusCodes = new[] { "1", "2", "3", "4" };
    }
}