namespace OpenClawAccounting.Models;

// 账户表：资金的载体（资产、负债、支出等）
public class Account
{
    public string Id     { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public User   User   { get; set; } = null!;

    public string Name { get; set; } = string.Empty; // 例如: "资产:支付宝:花呗"
    public string Type { get; set; } = string.Empty; // Asset, Liability, Equity, Income, Expense

    public ICollection<Posting> Postings { get; set; } = new List<Posting>();
}