/*
   Support: fsefacan@gmail.com
*/

using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using ThreeDPayment.Sample.Data;
using ThreeDPayment.Sample.Domains;

namespace ThreeDPayment.Sample.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDataContext _dataContext;

        public PaymentService(AppDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public Task<PaymentTransaction> GetById(int id,
            bool includeBank = false)
        {
            if (id == 0)
                return Task.FromResult<PaymentTransaction>(null);

            var query = _dataContext.PaymentTransactions.AsQueryable();

            if (includeBank)
                query = query.Include(x => x.Bank);

            return query.FirstOrDefaultAsync(x => x.Id.Equals(id));
        }

        public Task<PaymentTransaction> GetByOrderNumber(Guid orderNumber,
            bool includeBank = false)
        {
            if (orderNumber == Guid.Empty)
                return Task.FromResult<PaymentTransaction>(null);

            var query = _dataContext.PaymentTransactions.AsQueryable();

            if (includeBank)
                query = query.Include(x => x.Bank);

            return query.FirstOrDefaultAsync(x => x.OrderNumber.Equals(orderNumber));
        }

        public Task Insert(PaymentTransaction paymentTransaction)
        {
            if (paymentTransaction == null)
                throw new ArgumentNullException(nameof(PaymentTransaction));

            _dataContext.PaymentTransactions.Add(paymentTransaction);
            return _dataContext.SaveChangesAsync();
        }

        public Task Update(PaymentTransaction paymentTransaction)
        {
            if (paymentTransaction == null)
                throw new ArgumentNullException(nameof(PaymentTransaction));

            _dataContext.PaymentTransactions.Update(paymentTransaction);
            return _dataContext.SaveChangesAsync();
        }
    }
}