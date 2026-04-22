using Microsoft.EntityFrameworkCore;
using LGUTreasury.Models;

namespace LGUTreasury.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<Payee> Payees { get; set; }
        public DbSet<RevenueCategory> RevenueCategories { get; set; }
        public DbSet<RevenueType> RevenueTypes { get; set; }
        public DbSet<RevenuePolicy> RevenuePolicies { get; set; }
        public DbSet<PaymentRecord> PaymentRecords { get; set; }
        public DbSet<RecordLineItem> RecordLineItems { get; set; }
    }
}