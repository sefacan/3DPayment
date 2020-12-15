using Microsoft.EntityFrameworkCore;
using ThreeDPayment.Sample.Domains;

namespace ThreeDPayment.Sample.Data
{
    public class AppDataContext : DbContext
    {
        public AppDataContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        }

        // banks
        public DbSet<Bank> Banks { get; set; }
        public DbSet<BankParameter> BankParameters { get; set; }
        public DbSet<CreditCard> CreditCards { get; set; }
        public DbSet<CreditCardPrefix> CreditCardPrefixes { get; set; }
        public DbSet<CreditCardInstallment> CreditCardInstallments { get; set; }

        // payments
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    }
}