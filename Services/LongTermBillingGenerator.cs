using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Services;

public static class LongTermBillingGenerator
{
    public static async Task GenerateBillsForTenantAsync(
        ApplicationDbContext context,
        int tenantID,
        CancellationToken cancellationToken = default)
    {
        var tenant = await context.LongTermPayees
            .Include(t => t.AccountBillingTypes)
            .FirstOrDefaultAsync(t => t.LongTermPayeeID == tenantID, cancellationToken);

        if (tenant == null || !tenant.IsActive || !tenant.IsBillingEnabled)
            return;

        var activeBillingTypes = tenant.AccountBillingTypes?
            .Where(bt => bt.IsActive).ToList();

        if (activeBillingTypes == null || activeBillingTypes.Count == 0)
            return;

        if (!DateTime.TryParse(tenant.StartMonth + "-01", out var startDate))
            return;

        var billDay = tenant.BillGenerationDay > 0
            ? tenant.BillGenerationDay : 20;

        var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var cursor = new DateTime(startDate.Year, startDate.Month, 1);

        while (cursor <= currentMonth)
        {
            var day = Math.Min(billDay,
                DateTime.DaysInMonth(cursor.Year, cursor.Month));

            if (new DateTime(cursor.Year, cursor.Month, day) > DateTime.Today)
                break;

            var monthStr = cursor.ToString("yyyy-MM");

            foreach (var bt in activeBillingTypes)
            {
                var exists = await context.MonthlyBills.AnyAsync(b =>
                    b.LongTermPayeeID == tenantID &&
                    b.AccountBillingTypeID == bt.AccountBillingTypeID &&
                    b.BillingMonth == monthStr, cancellationToken);

                if (!exists)
                {
                    context.MonthlyBills.Add(new MonthlyBill
                    {
                        LongTermPayeeID = tenantID,
                        AccountBillingTypeID = bt.AccountBillingTypeID,
                        BillingMonth = monthStr,
                        BillingType = bt.BillingTypeName,
                        BilledAmount = bt.MonthlyRate,
                        Status = "Unpaid",
                        CreatedAt = DateTime.Now
                    });
                }
            }

            cursor = cursor.AddMonths(1);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
