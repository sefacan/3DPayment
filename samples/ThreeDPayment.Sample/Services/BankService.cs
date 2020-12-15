using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThreeDPayment.Sample.Data;
using ThreeDPayment.Sample.Domains;

namespace ThreeDPayment.Sample.Services
{
    public class BankService : IBankService
    {
        private readonly AppDataContext _dataContext;

        public BankService(AppDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public Task<Bank> GetDefaultBank()
        {
            var query = _dataContext.Banks.Where(b => b.DefaultBank);
            return query.FirstOrDefaultAsync();
        }

        public Task<Bank> GetById(int id)
        {
            if (id == 0)
                return Task.FromResult<Bank>(null);

            return _dataContext.Banks
                .FirstOrDefaultAsync(x => x.Id.Equals(id));
        }

        public Task<List<BankParameter>> GetBankParameters(int bankId)
        {
            if (bankId == 0)
                return Task.FromResult(Array.Empty<BankParameter>().ToList());

            return _dataContext.BankParameters
                .Where(bp => bp.BankId.Equals(bankId))
                .ToListAsync();
        }

        public Task<CreditCard> GetCreditCardByPrefix(string prefix,
            bool includeInstallments = false)
        {
            if (string.IsNullOrEmpty(prefix))
                return Task.FromResult<CreditCard>(null);

            prefix = prefix.Trim();

            var query = _dataContext.CreditCards
                .Where(x => x.Prefixes.Any(cp => cp.Prefix.Equals(prefix)));

            if (includeInstallments)
                query = query.Include(x => x.Installments);

            return query.FirstOrDefaultAsync();
        }
    }
}