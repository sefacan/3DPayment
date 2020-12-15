using System.Collections.Generic;

namespace ThreeDPayment.Requests
{
    public class VerifyGatewayRequest
    {
        public string CustomerIpAddress { get; set; }
        public bool ManufacturerCard { get; set; }
        public BankNames BankName { get; set; }
        public Dictionary<string, string> BankParameters { get; set; } = new Dictionary<string, string>();
    }
}