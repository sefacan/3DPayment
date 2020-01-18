using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ThreeDPayment.Payment
{
    public class PaymentProviderFactory : IPaymentProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public PaymentProviderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPaymentProvider Create(Banks bank)
        {
            switch (bank)
            {
                case Banks.AkBank:
                case Banks.IsBankasi:
                case Banks.HalkBank:
                case Banks.TurkEkonomiBankasi:
                case Banks.DenizBank:
                case Banks.IngBank:
                case Banks.ZiraatBankasi:
                case Banks.FinansBank:
                case Banks.KuveytTurk:
                    return ActivatorUtilities.GetServiceOrCreateInstance<AssecoPaymentProvider>(_serviceProvider);
                //Posnet
                case Banks.Yapikredi:
                    return ActivatorUtilities.GetServiceOrCreateInstance<YapikrediPaymentProvider>(_serviceProvider);
                //GVP
                case Banks.Garanti:
                    return ActivatorUtilities.GetServiceOrCreateInstance<GarantiPaymentProvider>(_serviceProvider);
                //GET 7/24
                case Banks.VakifBank:
                    return ActivatorUtilities.GetServiceOrCreateInstance<VakifbankPaymentProvider>(_serviceProvider);
                default:
                    throw new NotSupportedException("Bank not supported");
            }
        }

        public string CreatePaymentForm(IDictionary<string, object> parameters, Uri paymentUrl, bool appendSubmitScript = true)
        {
            var formId = "PaymentForm";
            var formBuilder = new StringBuilder();
            formBuilder.Append($"<form id=\"{formId}\" name=\"{formId}\" action=\"{paymentUrl}\" role=\"form\" method=\"POST\">");
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
    }
}
