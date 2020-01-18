using System;
using System.Collections.Generic;

namespace ThreeDPayment.Payment
{
    public interface IPaymentProviderFactory
    {
        IPaymentProvider Create(Banks bank);
        string CreatePaymentForm(IDictionary<string, object> parameters, Uri paymentUrl, bool appendFormSubmitScript = true);
    }
}