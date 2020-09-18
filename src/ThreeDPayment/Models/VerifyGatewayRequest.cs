using System.Collections.Generic;

namespace ThreeDPayment.Models
{
    public class VerifyGatewayRequest
    {
        public BankNames BankName { get; set; }
        public Dictionary<string, string> BankParameters { get; set; } = new Dictionary<string, string>();
    }
}