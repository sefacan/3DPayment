/*
   Support: fsefacan@gmail.com
*/

namespace ThreeDPayment.Sample.Domains
{
    public class BankParameter : BaseEntity
    {
        public BankParameter()
        {
        }

        public BankParameter(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public int BankId { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }

        public Bank Bank { get; set; }
    }
}