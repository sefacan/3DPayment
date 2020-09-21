using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ThreeDPayment.Providers;

namespace ThreeDPayment
{
    public class PaymentProviderFactory : IPaymentProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public PaymentProviderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPaymentProvider Create(BankNames bankName)
        {
            switch (bankName)
            {
                case BankNames.AkBank:
                case BankNames.IsBankasi:
                case BankNames.HalkBank:
                case BankNames.ZiraatBankasi:
                case BankNames.TurkEkonomiBankasi:
                case BankNames.IngBank:
                case BankNames.TurkiyeFinans:
                case BankNames.AnadoluBank:
                case BankNames.HSBC:
                {
                    //NestPay(AkBank, IsBankasi, HalkBank, ZiraatBankasi, TurkEkonomiBankasi, IngBank, TurkiyeFinans, AnadoluBank, HSBC)
                    return ActivatorUtilities.CreateInstance<NestPayPaymentProvider>(_serviceProvider);
                }
                case BankNames.DenizBank:
                {
                    //Denizbank(InterVpos)
                    return ActivatorUtilities.CreateInstance<DenizbankPaymentProvider>(_serviceProvider);
                }
                case BankNames.FinansBank:
                {
                    //nsbank(PayFor)
                    return ActivatorUtilities.CreateInstance<FinansbankPaymentProvider>(_serviceProvider);
                }
                case BankNames.Garanti:
                {
                    //Garanti(GVP)
                    return ActivatorUtilities.CreateInstance<GarantiPaymentProvider>(_serviceProvider);
                }
                case BankNames.KuveytTurk:
                {
                    //KuveytTurk
                    return ActivatorUtilities.CreateInstance<KuveytTurkPaymentProvider>(_serviceProvider);
                }
                case BankNames.VakifBank:
                {
                    //Vakıfbank(GET 7/24)
                    return ActivatorUtilities.CreateInstance<VakifbankPaymentProvider>(_serviceProvider);
                }
                case BankNames.Yapikredi:
                {
                    //Yapıkredi(YKB POSNET)
                    return ActivatorUtilities.CreateInstance<YapikrediPaymentProvider>(_serviceProvider);
                }
            }

            throw new NotSupportedException("Bank not supported");
        }

        public string CreatePaymentFormHtml(IDictionary<string, object> parameters, Uri actionUrl, bool appendSubmitScript = true)
        {
            if (parameters == null || !parameters.Any())
                throw new ArgumentNullException(nameof(parameters));

            if (actionUrl == null)
                throw new ArgumentNullException(nameof(actionUrl));

            var formId = "PaymentForm";
            var formBuilder = new StringBuilder();
            formBuilder.Append($"<form id=\"{formId}\" name=\"{formId}\" action=\"{actionUrl}\" role=\"form\" method=\"POST\">");

            foreach (var parameter in parameters)
            {
                formBuilder.Append($"<input type=\"hidden\" name=\"{parameter.Key}\" value=\"{parameter.Value}\">");
            }

            formBuilder.Append("</form>");

            if (appendSubmitScript)
            {
                var scriptBuilder = new StringBuilder();
                scriptBuilder.Append("<script>");
                scriptBuilder.Append($"document.{formId}.submit();");
                scriptBuilder.Append("</script>");
                formBuilder.Append(scriptBuilder.ToString());
            }

            return formBuilder.ToString();
        }

        public static readonly IDictionary<string, string> CurrencyCodes = new Dictionary<string, string>
        {
            { "949", "TRY" },
            { "840", "USD" },
            { "978", "EUR" },
            { "826", "GBP" }
        };

        public static readonly IDictionary<string, string> MDStatuses = new Dictionary<string, string>
        {
           { "0", "Doğrulama başarısız, 3-D Secure imzası geçersiz" },
           { "1", "Tam doğrulama" },
           { "2", "Kart sahibi banka veya kart 3D-Secure üyesi değil" },
           { "3", "Kartın bankası 3D-Secure üyesi değil" },
           { "4", "Kart sahibi banka sisteme daha sonra kayıt olmayı seçmiş" },
           { "5", "Doğrulama yapılamıyor" },
           { "7", "Sistem hatası" },
           { "8", "Bilinmeyen kart numarası" },
           { "9", "Üye işyeri 3D-Secure üyesi değil" }
        };
    }
}