using Microsoft.EntityFrameworkCore;
using OpenClawAccounting.Models;

namespace OpenClawAccounting.Services;

public class TransactionInputDto
{
    public DateTime              Date     { get; set; } = DateTime.UtcNow;
    public string                Payee    { get; set; } = string.Empty;
    public string                Note     { get; set; } = string.Empty;
    public List<PostingInputDto> Postings { get; set; } = [];
}

public class PostingInputDto
{
    public string  AccountName { get; set; } = string.Empty;
    public decimal Amount      { get; set; }
    public string  Currency    { get; set; } = "CNY";
}

public class AccountingService(AppDbContext dbContext)
{
    /// <summary>
    /// 用户冷启动逻辑：当遇到新用户时，自动为其在数据库中创建一套默认的基础账户树
    /// </summary>
    public async Task<User> InitializeUserAsync(string openClawUserId)
    {
        User? user       = null;
        var   connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id, OpenClawUserId, DefaultCurrency FROM Users WHERE OpenClawUserId = @id";
            var param = command.CreateParameter();
            param.ParameterName = "@id";
            param.Value         = openClawUserId;
            command.Parameters.Add(param);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                user = new User
                {
                    Id              = reader.GetString(0),
                    OpenClawUserId  = reader.GetString(1),
                    DefaultCurrency = reader.GetString(2)
                };
                dbContext.Users.Attach(user);
            }
        }

        if (user != null) return user;
        user = new User { OpenClawUserId = openClawUserId };
        dbContext.Users.Add(user);

        // 初始化默认账户树
        var defaultAccounts = new List<Account>
        {
            new() { User = user, Name = "资产:微信", Type  = "Asset" },
            new() { User = user, Name = "资产:支付宝", Type = "Asset" },
            new() { User = user, Name = "资产:银行卡", Type = "Asset" },
            new() { User = user, Name = "支出:餐饮", Type  = "Expense" },
            new() { User = user, Name = "支出:交通", Type  = "Expense" },
            new() { User = user, Name = "支出:购物", Type  = "Expense" },
            new() { User = user, Name = "支出:手续费", Type = "Expense" },
            new() { User = user, Name = "收入:工资", Type  = "Income" }
        };

        dbContext.Accounts.AddRange(defaultAccounts);
        await dbContext.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// 核心校验逻辑：记录交易，必须保证借贷平衡
    /// </summary>
    public async Task<Transaction> RecordTransactionAsync(string openClawUserId, TransactionInputDto input)
    {
        // 1. 获取用户，如果不存在则初始化
        var user = await InitializeUserAsync(openClawUserId);

        // 2. 硬性校验：检查传入的 Postings 的 Amount 累加和是否精确等于 0
        var totalAmount = input.Postings.Sum(p => p.Amount);
        if (totalAmount != 0)
        {
            throw new InvalidOperationException($"借贷不平衡！Postings 的 Amount 累加和必须为 0，当前为: {totalAmount}");
        }

        // 3. 准备交易实体
        var transaction = new Transaction
        {
            UserId = user.Id,
            Date   = input.Date,
            Payee  = input.Payee,
            Note   = input.Note
        };

        // 4. 处理 Postings
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();
        foreach (var postingDto in input.Postings)
        {
            var account =
                dbContext.Accounts.Local.FirstOrDefault(a => a.UserId == user.Id && a.Name == postingDto.AccountName);
            if (account == null)
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT Id, UserId, Name, Type FROM Accounts WHERE UserId = @userId AND Name = @name";
                var p1 = command.CreateParameter();
                p1.ParameterName = "@userId";
                p1.Value         = user.Id;
                command.Parameters.Add(p1);

                var p2 = command.CreateParameter();
                p2.ParameterName = "@name";
                p2.Value         = postingDto.AccountName;
                command.Parameters.Add(p2);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    account = new Account
                    {
                        Id     = reader.GetString(0),
                        UserId = reader.GetString(1),
                        Name   = reader.GetString(2),
                        Type   = reader.GetString(3)
                    };
                    dbContext.Accounts.Attach(account);
                }
            }

            if (account == null)
            {
                // 简单推断账户类型
                var type = "Unknown";
                if (postingDto.AccountName.StartsWith("资产")) type      = "Asset";
                else if (postingDto.AccountName.StartsWith("支出")) type = "Expense";
                else if (postingDto.AccountName.StartsWith("收入")) type = "Income";
                else if (postingDto.AccountName.StartsWith("负债")) type = "Liability";
                else if (postingDto.AccountName.StartsWith("权益")) type = "Equity";

                account = new Account
                {
                    UserId = user.Id,
                    Name   = postingDto.AccountName,
                    Type   = type
                };
                dbContext.Accounts.Add(account);
            }

            transaction.Postings.Add(new Posting
            {
                Account  = account,
                Amount   = postingDto.Amount,
                Currency = postingDto.Currency
            });
        }

        // 5. 保存到数据库
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();

        return transaction;
    }
}