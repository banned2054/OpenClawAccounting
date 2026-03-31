using Microsoft.EntityFrameworkCore;
using OpenClawAccounting.Models;

namespace OpenClawAccounting.Services;

// 查询结果DTO
public class TransactionQueryResult
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Payee { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public List<PostingQueryResult> Postings { get; set; } = [];
}

public class PostingQueryResult
{
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class BalanceQueryResult
{
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class SummaryQueryResult
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}

public class QueryService(AppDbContext dbContext)
{
    /// <summary>
    /// 查询交易记录
    /// </summary>
    public async Task<List<TransactionQueryResult>> QueryTransactionsAsync(
        string openClawUserId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? payee = null,
        string? accountName = null,
        int limit = 100)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) return [];

        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date <= endDate.Value);

        if (!string.IsNullOrEmpty(payee))
            query = query.Where(t => t.Payee.Contains(payee));

        if (!string.IsNullOrEmpty(accountName))
        {
            query = query.Where(t => t.Postings.Any(p => p.Account.Name == accountName));
        }

        var transactions = await query
            .OrderByDescending(t => t.Date)
            .Take(limit)
            .Include(t => t.Postings)
            .ThenInclude(p => p.Account)
            .ToListAsync();

        return transactions.Select(t => new TransactionQueryResult
        {
            Id = t.Id,
            Date = t.Date,
            Payee = t.Payee,
            Note = t.Note,
            Postings = t.Postings.Select(p => new PostingQueryResult
            {
                AccountName = p.Account.Name,
                AccountType = p.Account.Type,
                Amount = p.Amount,
                Currency = p.Currency
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// 查询账户余额
    /// </summary>
    public async Task<List<BalanceQueryResult>> QueryBalanceAsync(
        string openClawUserId,
        string? accountName = null)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) return [];

        var query = dbContext.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .AsQueryable();

        if (!string.IsNullOrEmpty(accountName))
            query = query.Where(a => a.Name == accountName);

        var accounts = await query
            .Include(a => a.Postings)
            .ToListAsync();

        return accounts.Select(a => new BalanceQueryResult
        {
            AccountName = a.Name,
            AccountType = a.Type,
            Balance = a.Postings.Sum(p => p.Amount),
            Currency = a.Postings.FirstOrDefault()?.Currency ?? "CNY"
        }).ToList();
    }

    /// <summary>
    /// 查询消费统计（按账户类型分组）
    /// </summary>
    public async Task<List<SummaryQueryResult>> QuerySummaryAsync(
        string openClawUserId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) return [];

        var query = dbContext.Postings
            .AsNoTracking()
            .Where(p => p.Transaction.UserId == user.Id)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(p => p.Transaction.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.Transaction.Date <= endDate.Value);

        var summary = await query
            .GroupBy(p => p.Account.Type)
            .Select(g => new SummaryQueryResult
            {
                Category = g.Key,
                TotalAmount = g.Sum(p => p.Amount),
                TransactionCount = g.Count()
            })
            .ToListAsync();

        return summary;
    }

    /// <summary>
    /// 按商家统计消费
    /// </summary>
    public async Task<List<SummaryQueryResult>> QueryPayeeSummaryAsync(
        string openClawUserId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) return [];

        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date <= endDate.Value);

        var summary = await query
            .GroupBy(t => t.Payee)
            .Select(g => new SummaryQueryResult
            {
                Category = g.Key,
                TotalAmount = g.SelectMany(t => t.Postings).Sum(p => p.Amount),
                TransactionCount = g.Count()
            })
            .ToListAsync();

        return summary;
    }
}
