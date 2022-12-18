/*
   Support: fsefacan@gmail.com
*/

using System;
using System.Threading.Tasks;
using ThreeDPayment.Sample.Domains;

namespace ThreeDPayment.Sample.Services
{
    public interface IPaymentService
    {
        Task<PaymentTransaction> GetById(int id,
            bool includeBank = false);
        Task<PaymentTransaction> GetByOrderNumber(Guid orderNumber,
            bool includeBank = false);
        Task Insert(PaymentTransaction paymentTransaction);
        Task Update(PaymentTransaction paymentTransaction);
    }
}