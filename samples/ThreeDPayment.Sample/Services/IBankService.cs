/*
   Support: fsefacan@gmail.com
*/

using System.Collections.Generic;
using System.Threading.Tasks;
using ThreeDPayment.Sample.Domains;

namespace ThreeDPayment.Sample.Services
{
    public interface IBankService
    {
        Task<Bank> GetDefaultBank();
        Task<Bank> GetById(int id);
        Task<List<BankParameter>> GetBankParameters(int bankId);
        Task<CreditCard> GetCreditCardByPrefix(string prefix,
            bool includeInstallments = false);
    }
}