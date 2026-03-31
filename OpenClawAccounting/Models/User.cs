namespace OpenClawAccounting.Models;

// 用户表：支持多租户开源
public class User
{
    public string Id              { get; set; } = Guid.NewGuid().ToString();
    public string OpenClawUserId  { get; set; } = string.Empty; // 绑定的外部ID
    public string DefaultCurrency { get; set; } = "CNY";        // 默认本位币

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}