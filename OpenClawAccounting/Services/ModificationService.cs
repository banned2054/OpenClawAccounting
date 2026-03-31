using Microsoft.EntityFrameworkCore;
using OpenClawAccounting.Models;

namespace OpenClawAccounting.Services;

// 修改请求DTO
public class UpdateTransactionRequest
{
    public string? Payee { get; set; }
    public string? Note { get; set; }
    public DateTime? Date { get; set; }
}

public class UpdateAccountRequest
{
    public string OldName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
}

public class ModificationService(AppDbContext dbContext)
{
    /// <summary>
    /// 修改交易信息（不修改分录金额）
    /// </summary>
    public async Task<Transaction?> UpdateTransactionAsync(
        string openClawUserId,
        string transactionId,
        UpdateTransactionRequest request)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) throw new InvalidOperationException("用户不存在");

        var transaction = await dbContext.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == user.Id);

        if (transaction == null) return null;

        // 更新字段
        if (!string.IsNullOrEmpty(request.Payee))
            transaction.Payee = request.Payee;

        if (!string.IsNullOrEmpty(request.Note))
            transaction.Note = request.Note;

        if (request.Date.HasValue)
            transaction.Date = request.Date.Value;

        await dbContext.SaveChangesAsync();
        return transaction;
    }

    /// <summary>
    /// 修改账户名称
    /// </summary>
    public async Task<Account?> UpdateAccountNameAsync(
        string openClawUserId,
        string oldName,
        string newName)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) throw new InvalidOperationException("用户不存在");

        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.Name == oldName);

        if (account == null) return null;

        // 检查新名称是否已存在
        var existingAccount = await dbContext.Accounts
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.Name == newName);

        if (existingAccount != null && existingAccount.Id != account.Id)
            throw new InvalidOperationException($"账户 '{newName}' 已存在");

        // 推断新账户类型
        var newType = InferAccountType(newName);
        if (newType != "Unknown")
            account.Type = newType;

        account.Name = newName;
        await dbContext.SaveChangesAsync();
        return account;
    }

    /// <summary>
    /// 批量修改商家名称
    /// </summary>
    public async Task<int> BatchUpdatePayeeAsync(
        string openClawUserId,
        string oldPayee,
        string newPayee)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) throw new InvalidOperationException("用户不存在");

        var transactions = await dbContext.Transactions
            .Where(t => t.UserId == user.Id && t.Payee == oldPayee)
            .ToListAsync();

        foreach (var transaction in transactions)
        {
            transaction.Payee = newPayee;
        }

        await dbContext.SaveChangesAsync();
        return transactions.Count;
    }

    /// <summary>
    /// 删除指定交易
    /// </summary>
    public async Task<bool> DeleteTransactionAsync(
        string openClawUserId,
        string transactionId)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) throw new InvalidOperationException("用户不存在");

        var transaction = await dbContext.Transactions
            .Include(t => t.Postings)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == user.Id);

        if (transaction == null) return false;

        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 按条件批量删除交易
    /// </summary>
    public async Task<int> DeleteTransactionsByCriteriaAsync(
        string openClawUserId,
        DateTime? date = null,
        string? payee = null)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) throw new InvalidOperationException("用户不存在");

        var query = dbContext.Transactions
            .Where(t => t.UserId == user.Id)
            .AsQueryable();

        if (date.HasValue)
            query = query.Where(t => t.Date.Date == date.Value.Date);

        if (!string.IsNullOrEmpty(payee))
            query = query.Where(t => t.Payee.Contains(payee));

        var transactions = await query.ToListAsync();

        dbContext.Transactions.RemoveRange(transactions);
        await dbContext.SaveChangesAsync();
        return transactions.Count;
    }

    /// <summary>
    /// 删除账户（需确保没有关联交易）
    /// </summary>
    public async Task<bool> DeleteAccountAsync(
        string openClawUserId,
        string accountName)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) throw new InvalidOperationException("用户不存在");

        var account = await dbContext.Accounts
            .Include(a => a.Postings)
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.Name == accountName);

        if (account == null) return false;

        if (account.Postings.Any())
            throw new InvalidOperationException($"账户 '{accountName}' 存在交易记录，无法删除");

        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 创建冲正交易（会计标准做法）
    /// </summary>
    public async Task<Transaction?> ReverseTransactionAsync(
        string openClawUserId,
        string originalTransactionId,
        string? reason = null)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OpenClawUserId == openClawUserId);

        if (user == null) throw new InvalidOperationException("用户不存在");

        var originalTransaction = await dbContext.Transactions
            .Include(t => t.Postings)
            .ThenInclude(p => p.Account)
            .FirstOrDefaultAsync(t => t.Id == originalTransactionId && t.UserId == user.Id);

        if (originalTransaction == null) return null;

        // 创建冲正交易：金额取反
        var reversalTransaction = new Transaction
        {
            UserId = user.Id,
            Date = DateTime.UtcNow,
            Payee = $"[冲正] {originalTransaction.Payee}",
            Note = $"冲正交易 ID:{originalTransactionId}. {reason}",
            Postings = originalTransaction.Postings.Select(p => new Posting
            {
                Account = p.Account,
                Amount = -p.Amount, // 金额取反
                Currency = p.Currency
            }).ToList()
        };

        dbContext.Transactions.Add(reversalTransaction);
        await dbContext.SaveChangesAsync();
        return reversalTransaction;
    }

    private static string InferAccountType(string accountName)
    {
        if (accountName.StartsWith("资产")) return "Asset";
        if (accountName.StartsWith("支出")) return "Expense";
        if (accountName.StartsWith("收入")) return "Income";
        if (accountName.StartsWith("负债")) return "Liability";
        if (accountName.StartsWith("权益")) return "Equity";
        return "Unknown";
    }
}
