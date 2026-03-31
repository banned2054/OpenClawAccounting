namespace OpenClawAccounting.Models;

// 流水明细表：复式记账的精髓（多借多贷）
public class Posting
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string      TransactionId { get; set; } = string.Empty; //外键关联 Transaction，需配置级联删除 Cascade
    public Transaction Transaction   { get; set; } = null!;

    public string  AccountId { get; set; } = string.Empty; //外键关联 Account
    public Account Account   { get; set; } = null!;

    public decimal Amount   { get; set; }          // 正数代表借 (Debit)，负数代表贷 (Credit)
    public string  Currency { get; set; } = "CNY"; // 支持多币种
}