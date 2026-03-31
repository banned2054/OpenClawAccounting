namespace OpenClawAccounting.Models;

// 交易凭证表：记录一次“事件”
public class Transaction
{
    public string Id     { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty; //外键关联 User

    public DateTime Date  { get; set; } = DateTime.UtcNow; //交易发生时间
    public string   Payee { get; set; } = string.Empty;    // 交易对手，如“星巴克”
    public string   Note  { get; set; } = string.Empty;    // 备注，如“买拿铁”

    public ICollection<Posting> Postings { get; set; } = new List<Posting>();
}