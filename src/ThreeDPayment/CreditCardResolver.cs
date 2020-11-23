using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ThreeDPayment.Models;

namespace ThreeDPayment
{
    public class CreditCardResolver : ICreditCardResolver
    {
        private readonly IEnumerable<CreditCardBin> binData;

        public CreditCardResolver()
        {
            using (StreamReader r = new StreamReader("binlist.json"))
            {
                string json = r.ReadToEnd();
                binData = JsonSerializer.Deserialize<IEnumerable<CreditCardBin>>(json);
            }
        }

        public BankNames GetBankName(string cardNumber)
        {
            cardNumber = new string(cardNumber.Where(c => char.IsDigit(c)).ToArray()).Substring(0, 6);

            var creditCardBin = binData
                .Where(c => c.bin.Equals(cardNumber.Substring(0, 6)))
                .FirstOrDefault();

            if (creditCardBin is null)
            {
                throw new NotSupportedException("Credit card not supported");
            }

            if (Enum.IsDefined(typeof(BankNames), creditCardBin.bankCode))
            {
                return (BankNames)creditCardBin.bankCode;
            }
            else
            {
                throw new NotSupportedException("Credit card not supported");
            }
        }
    }
}
