using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreeDPayment.Models
{
    public class CreditCardBin
    {
        public string bin { get; set; }
        public int bankCode { get; set; }
        public string bankName { get; set; }
        public string type { get; set; }
        public string subType { get; set; }
        public string @virtual { get; set; }
        public string prepaid { get; set; }
    }
}
