using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using ThreeDPayment.Sample.Domains;
using ThreeDPayment.Sample.Helpers;

namespace ThreeDPayment.Sample.Data
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder InitializeDatabase(this IApplicationBuilder app)
        {
            using (IServiceScope scope = app.ApplicationServices.CreateScope())
            using (AppDataContext context = scope.ServiceProvider.GetRequiredService<AppDataContext>())
            {
                try
                {
                    //apply migrations
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                //seed data
                SeedData(context);
            }

            return app;
        }

        private static void SeedData(AppDataContext dataContext)
        {
            try
            {
                //banks
                if (!dataContext.Banks.Any())
                {
                    IOrderedEnumerable<BankNames> bankNames = Enum.GetValues(typeof(BankNames)).Cast<BankNames>().OrderBy(b => b.GetDisplayName());
                    foreach (BankNames bankName in bankNames)
                    {
                        //skip if exists
                        if (dataContext.Banks.Any(b => b.SystemName.Equals(bankName)))
                        {
                            continue;
                        }

                        dataContext.Banks.Add(new Bank
                        {
                            LogoPath = $"/media/banks/{bankName.ToString().ToLower()}.jpg",
                            Name = bankName.GetDisplayName(),
                            SystemName = bankName.ToString(),
                            BankCode = (int)bankName,
                            CreateDate = DateTime.Now,
                            UpdateDate = DateTime.Now,
                            Active = true
                        });

                        //do not move to out of the foreach. ef core doesn't insert by order
                        dataContext.SaveChanges();
                    }

                    //set default bank
                    dataContext.Banks.FirstOrDefault(x => x.SystemName.Equals(BankNames.IsBankasi.ToString())).DefaultBank = true;
                    dataContext.SaveChanges();
                }

                //bank parameters
                if (!dataContext.BankParameters.Any())
                {
                    Bank defaultBank = dataContext.Banks.FirstOrDefault(x => x.SystemName.Equals(BankNames.IsBankasi.ToString()));
                    defaultBank.Parameters.Add(new BankParameter("clientId", "190200000"));
                    defaultBank.Parameters.Add(new BankParameter("processType", "Auth"));
                    defaultBank.Parameters.Add(new BankParameter("storeKey", "123456"));
                    defaultBank.Parameters.Add(new BankParameter("storeType", "3D_PAY"));
                    defaultBank.Parameters.Add(new BankParameter("gatewayUrl", "https://entegrasyon.asseco-see.com.tr/fim/est3Dgate"));

                    dataContext.SaveChanges();
                }

                //credit cards, installments, prefixes
                if (!dataContext.CreditCards.Any())
                {
                    Bank defaultBank = dataContext.Banks.FirstOrDefault(x => x.SystemName.Equals(BankNames.IsBankasi.ToString()));

                    CreditCard creditCard = new CreditCard
                    {
                        BankId = defaultBank.Id,
                        Name = "İşbank Maximum",
                        Active = true,
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    };

                    creditCard.Prefixes.Add(new CreditCardPrefix
                    {
                        Prefix = "450803",
                        Active = true,
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    });

                    creditCard.Installments.Add(new CreditCardInstallment
                    {
                        Installment = 3,
                        InstallmentRate = 0.12m,
                        Active = true,
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    });

                    creditCard.Installments.Add(new CreditCardInstallment
                    {
                        Installment = 6,
                        InstallmentRate = 1.01m,
                        Active = true,
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    });

                    creditCard.Installments.Add(new CreditCardInstallment
                    {
                        Installment = 9,
                        InstallmentRate = 1.13m,
                        Active = true,
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    });

                    creditCard.Installments.Add(new CreditCardInstallment
                    {
                        Installment = 12,
                        InstallmentRate = 1.68m,
                        Active = true,
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    });

                    dataContext.CreditCards.Add(creditCard);
                    dataContext.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}