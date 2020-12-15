using System;
using System.Collections.Generic;

namespace ThreeDPayment
{
    public interface IPaymentProviderFactory
    {
        IPaymentProvider Create(BankNames bankName);
        string CreatePaymentFormHtml(IDictionary<string, object> parameters, Uri actionUrl, bool appendFormSubmitScript = true);
    }
}