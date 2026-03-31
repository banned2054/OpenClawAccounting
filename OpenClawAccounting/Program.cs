using OpenClawAccounting;
using OpenClawAccounting.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

// 1. 初始化数据库连接和依赖
using var db = new AppDbContext();
db.Database.EnsureCreated();

var accountingService = new AccountingService(db);
var queryService = new QueryService(db);
var modificationService = new ModificationService(db);

// 2. 检查 CLI 参数
if (args.Length == 0)
{
    PrintUsage();
    return;
}

string command = args[0];
string jsonInput = args.Length > 1 ? args[1] : "";

try
{
    switch (command.ToLower())
    {
        case "record":
        case "add":
            await HandleRecordAsync(accountingService, jsonInput);
            break;

        case "query":
        case "list":
            await HandleQueryAsync(queryService, jsonInput);
            break;

        case "balance":
            await HandleBalanceAsync(queryService, jsonInput);
            break;

        case "summary":
            await HandleSummaryAsync(queryService, jsonInput);
            break;

        case "update":
            await HandleUpdateAsync(modificationService, jsonInput);
            break;

        case "delete":
            await HandleDeleteAsync(modificationService, jsonInput);
            break;

        case "reverse":
            await HandleReverseAsync(modificationService, jsonInput);
            break;

        case "batch-update":
            await HandleBatchUpdateAsync(modificationService, jsonInput);
            break;

        case "help":
        case "--help":
        case "-h":
            PrintUsage();
            break;

        default:
            // 兼容旧版本：如果没有指定命令，尝试解析为记账请求
            await HandleRecordAsync(accountingService, command);
            break;
    }
}
catch (JsonException ex)
{
    Console.WriteLine($"JSON 解析错误: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"业务校验失败: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"发生未知错误: {ex.Message}");
}

// ==================== 命令处理函数 ====================

async Task HandleRecordAsync(AccountingService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AccountingRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId) || request.Transaction == null)
    {
        Console.WriteLine("错误: JSON 格式不正确或缺少必要字段 (UserId, Transaction)。");
        return;
    }

    Console.WriteLine($"=== 开始为用户 {request.UserId} 记账 ===");
    var transaction = await service.RecordTransactionAsync(request.UserId, request.Transaction);

    Console.WriteLine("=== 记账成功！===");
    Console.WriteLine($"交易ID: {transaction.Id}");
    Console.WriteLine($"时间: {transaction.Date}");
    Console.WriteLine($"交易对手: {transaction.Payee}");
    Console.WriteLine($"备注: {transaction.Note}");

    foreach (var posting in request.Transaction.Postings)
    {
        string direction = posting.Amount > 0 ? "借 (去向)" : "贷 (来源)";
        Console.WriteLine($"{direction}: {posting.AccountName} {posting.Amount:+#.##;-#.##;0}");
    }
}

async Task HandleQueryAsync(QueryService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.QueryRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId))
    {
        Console.WriteLine("错误: 缺少必要字段 UserId。");
        return;
    }

    var results = await service.QueryTransactionsAsync(
        request.UserId,
        request.StartDate,
        request.EndDate,
        request.Payee,
        request.AccountName,
        request.Limit ?? 100);

    Console.WriteLine($"=== 查询到 {results.Count} 条交易记录 ===");
    foreach (var t in results)
    {
        Console.WriteLine($"\n[{t.Date:yyyy-MM-dd HH:mm}] {t.Payee}");
        Console.WriteLine($"  ID: {t.Id}");
        if (!string.IsNullOrEmpty(t.Note)) Console.WriteLine($"  备注: {t.Note}");
        foreach (var p in t.Postings)
        {
            string direction = p.Amount > 0 ? "借" : "贷";
            Console.WriteLine($"  {direction}: {p.AccountName} {p.Amount:+#.##;-#.##;0} {p.Currency}");
        }
    }
}

async Task HandleBalanceAsync(QueryService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.BalanceRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId))
    {
        Console.WriteLine("错误: 缺少必要字段 UserId。");
        return;
    }

    var results = await service.QueryBalanceAsync(request.UserId, request.AccountName);

    Console.WriteLine($"=== 账户余额 ({results.Count} 个账户) ===");
    foreach (var b in results.OrderBy(r => r.AccountType).ThenBy(r => r.AccountName))
    {
        Console.WriteLine($"[{b.AccountType}] {b.AccountName}: {b.Balance:F2} {b.Currency}");
    }
}

async Task HandleSummaryAsync(QueryService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.SummaryRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId))
    {
        Console.WriteLine("错误: 缺少必要字段 UserId。");
        return;
    }

    Console.WriteLine("=== 按账户类型统计 ===");
    var typeSummary = await service.QuerySummaryAsync(request.UserId, request.StartDate, request.EndDate);
    foreach (var s in typeSummary.OrderByDescending(s => s.TotalAmount))
    {
        Console.WriteLine($"{s.Category}: {s.TotalAmount:F2} ({s.TransactionCount} 笔)");
    }

    if (request.GroupByPayee == true)
    {
        Console.WriteLine("\n=== 按商家统计 ===");
        var payeeSummary = await service.QueryPayeeSummaryAsync(request.UserId, request.StartDate, request.EndDate);
        foreach (var s in payeeSummary.OrderByDescending(s => Math.Abs(s.TotalAmount)).Take(20))
        {
            Console.WriteLine($"{s.Category}: {s.TotalAmount:F2} ({s.TransactionCount} 笔)");
        }
    }
}

async Task HandleUpdateAsync(ModificationService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.UpdateRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId))
    {
        Console.WriteLine("错误: 缺少必要字段 UserId。");
        return;
    }

    if (!string.IsNullOrEmpty(request.TransactionId))
    {
        // 修改交易
        var updateReq = new UpdateTransactionRequest
        {
            Payee = request.Payee,
            Note = request.Note,
            Date = request.Date
        };

        var result = await service.UpdateTransactionAsync(request.UserId, request.TransactionId, updateReq);
        if (result != null)
        {
            Console.WriteLine($"=== 交易更新成功 ===");
            Console.WriteLine($"交易ID: {result.Id}");
            Console.WriteLine($"商家: {result.Payee}");
            Console.WriteLine($"备注: {result.Note}");
        }
        else
        {
            Console.WriteLine("错误: 交易不存在或无权访问。");
        }
    }
    else if (!string.IsNullOrEmpty(request.OldAccountName) && !string.IsNullOrEmpty(request.NewAccountName))
    {
        // 修改账户名称
        var result = await service.UpdateAccountNameAsync(request.UserId, request.OldAccountName, request.NewAccountName);
        if (result != null)
        {
            Console.WriteLine($"=== 账户名称修改成功 ===");
            Console.WriteLine($"{request.OldAccountName} -> {result.Name}");
            Console.WriteLine($"类型: {result.Type}");
        }
        else
        {
            Console.WriteLine("错误: 账户不存在。");
        }
    }
    else
    {
        Console.WriteLine("错误: 请指定 TransactionId 或 OldAccountName/NewAccountName。");
    }
}

async Task HandleDeleteAsync(ModificationService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DeleteRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId))
    {
        Console.WriteLine("错误: 缺少必要字段 UserId。");
        return;
    }

    if (!string.IsNullOrEmpty(request.TransactionId))
    {
        // 删除指定交易
        var success = await service.DeleteTransactionAsync(request.UserId, request.TransactionId);
        Console.WriteLine(success ? "=== 交易删除成功 ===" : "错误: 交易不存在或无权访问。");
    }
    else if (!string.IsNullOrEmpty(request.AccountName))
    {
        // 删除账户
        var success = await service.DeleteAccountAsync(request.UserId, request.AccountName);
        Console.WriteLine(success ? $"=== 账户 '{request.AccountName}' 删除成功 ===" : "错误: 账户不存在或存在交易记录。");
    }
    else if (request.Date.HasValue || !string.IsNullOrEmpty(request.Payee))
    {
        // 批量删除
        var count = await service.DeleteTransactionsByCriteriaAsync(request.UserId, request.Date, request.Payee);
        Console.WriteLine($"=== 已删除 {count} 条交易记录 ===");
    }
    else
    {
        Console.WriteLine("错误: 请指定 TransactionId、AccountName 或删除条件(Date/Payee)。");
    }
}

async Task HandleReverseAsync(ModificationService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ReverseRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.TransactionId))
    {
        Console.WriteLine("错误: 缺少必要字段 UserId 或 TransactionId。");
        return;
    }

    var result = await service.ReverseTransactionAsync(request.UserId, request.TransactionId, request.Reason);

    if (result != null)
    {
        Console.WriteLine("=== 冲正交易创建成功 ===");
        Console.WriteLine($"新交易ID: {result.Id}");
        Console.WriteLine($"商家: {result.Payee}");
        Console.WriteLine($"备注: {result.Note}");
        Console.WriteLine($"时间: {result.Date}");
    }
    else
    {
        Console.WriteLine("错误: 原交易不存在或无权访问。");
    }
}

async Task HandleBatchUpdateAsync(ModificationService service, string json)
{
    var request = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.BatchUpdateRequest);

    if (request == null || string.IsNullOrEmpty(request.UserId))
    {
        Console.WriteLine("错误: 缺少必要字段 UserId。");
        return;
    }

    if (!string.IsNullOrEmpty(request.OldPayee) && !string.IsNullOrEmpty(request.NewPayee))
    {
        var count = await service.BatchUpdatePayeeAsync(request.UserId, request.OldPayee, request.NewPayee);
        Console.WriteLine($"=== 批量更新完成 ===");
        Console.WriteLine($"将 {count} 条交易的商家从 '{request.OldPayee}' 改为 '{request.NewPayee}'");
    }
    else
    {
        Console.WriteLine("错误: 请指定 OldPayee 和 NewPayee。");
    }
}

void PrintUsage()
{
    Console.WriteLine("""
    OpenClawAccounting - 复式记账 CLI 工具

    用法: OpenClawAccounting <命令> <json参数>

    命令:
      record, add     记录新交易
      query, list     查询交易记录
      balance         查询账户余额
      summary         查询消费统计
      update          修改交易或账户信息
      delete          删除交易或账户
      reverse         创建冲正交易
      batch-update    批量更新商家名称
      help            显示此帮助信息

    示例:
      # 记账
      OpenClawAccounting record "{\"UserId\":\"user001\",\"Transaction\":{\"Payee\":\"麦当劳\",\"Note\":\"午餐\",\"Postings\":[{\"AccountName\":\"支出:餐饮\",\"Amount\":30},{\"AccountName\":\"资产:微信\",\"Amount\":-30}]}}"

      # 查询最近交易
      OpenClawAccounting query "{\"UserId\":\"user001\",\"Limit\":10}"

      # 查询余额
      OpenClawAccounting balance "{\"UserId\":\"user001\"}"

      # 查询本月统计
      OpenClawAccounting summary "{\"UserId\":\"user001\",\"StartDate\":\"2026-03-01\",\"EndDate\":\"2026-03-31\",\"GroupByPayee\":true}"

      # 修改商家名称
      OpenClawAccounting update "{\"UserId\":\"user001\",\"TransactionId\":\"xxx\",\"Payee\":\"新商家\"}"

      # 删除交易
      OpenClawAccounting delete "{\"UserId\":\"user001\",\"TransactionId\":\"xxx\"}"

      # 冲正交易
      OpenClawAccounting reverse "{\"UserId\":\"user001\",\"TransactionId\":\"xxx\",\"Reason\":\"录入错误\"}"

      # 批量修改商家
      OpenClawAccounting batch-update "{\"UserId\":\"user001\",\"OldPayee\":\"麦当劳\",\"NewPayee\":\"麦当劳(金拱门)\"}"
    """);
}

// ==================== 请求模型定义 ====================

public class AccountingRequest
{
    public string UserId { get; set; } = string.Empty;
    public TransactionInputDto Transaction { get; set; } = null!;
}

public class QueryRequest
{
    public string UserId { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Payee { get; set; }
    public string? AccountName { get; set; }
    public int? Limit { get; set; }
}

public class BalanceRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? AccountName { get; set; }
}

public class SummaryRequest
{
    public string UserId { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? GroupByPayee { get; set; }
}

public class UpdateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? Payee { get; set; }
    public string? Note { get; set; }
    public DateTime? Date { get; set; }
    public string? OldAccountName { get; set; }
    public string? NewAccountName { get; set; }
}

public class DeleteRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? AccountName { get; set; }
    public DateTime? Date { get; set; }
    public string? Payee { get; set; }
}

public class ReverseRequest
{
    public string UserId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class BatchUpdateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? OldPayee { get; set; }
    public string? NewPayee { get; set; }
}

// AOT 兼容的 JSON 序列化上下文
[JsonSerializable(typeof(AccountingRequest))]
[JsonSerializable(typeof(TransactionInputDto))]
[JsonSerializable(typeof(PostingInputDto))]
[JsonSerializable(typeof(QueryRequest))]
[JsonSerializable(typeof(BalanceRequest))]
[JsonSerializable(typeof(SummaryRequest))]
[JsonSerializable(typeof(UpdateRequest))]
[JsonSerializable(typeof(DeleteRequest))]
[JsonSerializable(typeof(ReverseRequest))]
[JsonSerializable(typeof(BatchUpdateRequest))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
