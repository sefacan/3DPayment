using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeDPayment
{
    public interface ICreditCardResolver
    {
        public BankNames GetBankName(string cardNumber);
    }
}
