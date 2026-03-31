using Microsoft.EntityFrameworkCore;
using OpenClawAccounting.Models;

namespace OpenClawAccounting;

public class AppDbContext : DbContext
{
    // 定义数据库中的四张表
    public DbSet<User>        Users        { get; set; } = null!;
    public DbSet<Account>     Accounts     { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<Posting>     Postings     { get; set; } = null!;

    // 配置连接到本地的 SQLite 数据库文件
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 在 AOT 或 Docker 环境下，最好使用绝对路径或明确的工作目录
        // 这里我们优先从环境变量获取数据库路径，如果没有则默认在当前执行目录下生成 accounting.db
        var dbPath = Environment.GetEnvironmentVariable("ACCOUNTING_DB_PATH");
        if (string.IsNullOrEmpty(dbPath))
        {
            // 获取当前执行文件所在的目录
            var baseDir = AppContext.BaseDirectory;
            dbPath = Path.Combine(baseDir, "accounting.db");
        }

        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    // 配置表之间的关系和字段约束
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite 原生不支持 Decimal，EF Core 会自动将其转为 TEXT 存储以保证财务精度不丢失
        // 我们这里配置一下级联删除：删掉 Transaction 时，自动删掉对应的 Postings
        modelBuilder.Entity<Transaction>()
                    .HasMany(t => t.Postings)
                    .WithOne(p => p.Transaction)
                    .HasForeignKey(p => p.TransactionId)
                    .OnDelete(DeleteBehavior.Cascade);
    }
}