# OpenClawAccounting

一个基于复式记账法的轻量级 CLI 记账工具，专为 OpenClaw 系统设计。

## 特性

- **复式记账**：严格遵循会计恒等式，支持多借多贷
- **多租户支持**：通过 UserId 隔离不同用户数据
- **多币种支持**：默认 CNY，可扩展其他货币
- **AOT 兼容**：支持 Native AOT 编译，快速启动
- **SQLite 存储**：本地数据库，无需额外配置

## 快速开始

### 构建

```bash
cd OpenClawAccounting
dotnet build
```

### 发布（AOT）

```bash
# Windows
dotnet publish -c Release -r win-x64 -p:PublishAot=true

# Linux
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
```

## 命令参考

### 1. 记账 (record/add)

记录一笔交易，必须保证借贷平衡（Postings 金额总和为 0）。

```bash
OpenClawAccounting record '{"UserId":"user001","Transaction":{"Payee":"麦当劳","Note":"午餐","Postings":[{"AccountName":"支出:餐饮","Amount":35.5},{"AccountName":"资产:微信","Amount":-35.5}]}}'
```

**参数说明：**
- `UserId`: 用户标识
- `Payee`: 交易对手/商家
- `Note`: 备注
- `Postings`: 分录列表
  - `AccountName`: 账户名称（如 `支出:餐饮`、`资产:微信`）
  - `Amount`: 金额（正数=借，负数=贷）
  - `Currency`: 货币（默认 CNY）

### 2. 查询交易 (query/list)

```bash
# 查询最近 10 条
OpenClawAccounting query '{"UserId":"user001","Limit":10}'

# 按日期范围查询
OpenClawAccounting query '{"UserId":"user001","StartDate":"2026-03-01","EndDate":"2026-03-31"}'

# 按商家查询
OpenClawAccounting query '{"UserId":"user001","Payee":"麦当劳"}'

# 按账户查询
OpenClawAccounting query '{"UserId":"user001","AccountName":"支出:餐饮"}'
```

### 3. 查询余额 (balance)

```bash
# 查询所有账户余额
OpenClawAccounting balance '{"UserId":"user001"}'

# 查询指定账户
OpenClawAccounting balance '{"UserId":"user001","AccountName":"资产:微信"}'
```

### 4. 消费统计 (summary)

```bash
# 按账户类型统计
OpenClawAccounting summary '{"UserId":"user001","StartDate":"2026-03-01","EndDate":"2026-03-31"}'

# 同时按商家统计
OpenClawAccounting summary '{"UserId":"user001","StartDate":"2026-03-01","EndDate":"2026-03-31","GroupByPayee":true}'
```

### 5. 修改交易 (update)

```bash
# 修改商家名称
OpenClawAccounting update '{"UserId":"user001","TransactionId":"xxx","Payee":"新商家"}'

# 修改备注
OpenClawAccounting update '{"UserId":"user001","TransactionId":"xxx","Note":"新备注"}'

# 修改日期
OpenClawAccounting update '{"UserId":"user001","TransactionId":"xxx","Date":"2026-03-15T10:30:00"}'

# 修改账户名称
OpenClawAccounting update '{"UserId":"user001","OldAccountName":"资产:微信","NewAccountName":"资产:微信支付"}'
```

### 6. 批量更新 (batch-update)

```bash
# 统一商家名称
OpenClawAccounting batch-update '{"UserId":"user001","OldPayee":"麦当劳","NewPayee":"麦当劳(金拱门)"}'
```

### 7. 删除交易 (delete)

```bash
# 删除指定交易
OpenClawAccounting delete '{"UserId":"user001","TransactionId":"xxx"}'

# 按条件批量删除
OpenClawAccounting delete '{"UserId":"user001","Payee":"测试商家"}'
OpenClawAccounting delete '{"UserId":"user001","Date":"2026-03-31"}'

# 删除空账户
OpenClawAccounting delete '{"UserId":"user001","AccountName":"资产:测试"}'
```

### 8. 冲正交易 (reverse)

会计标准做法，创建一笔反向交易而非直接删除，保留审计痕迹。

```bash
OpenClawAccounting reverse '{"UserId":"user001","TransactionId":"xxx","Reason":"录入错误"}'
```

### 9. 帮助

```bash
OpenClawAccounting help
```

## 账户命名规范

使用冒号分隔的层级命名，系统自动推断账户类型：

| 前缀 | 类型 | 示例 |
|------|------|------|
| `资产:` | Asset | `资产:微信`、`资产:支付宝`、`资产:银行卡` |
| `负债:` | Liability | `负债:信用卡`、`负债:花呗` |
| `权益:` | Equity | `权益:初始投资` |
| `收入:` | Income | `收入:工资`、`收入:投资收益` |
| `支出:` | Expense | `支出:餐饮`、`支出:交通`、`支出:购物` |

## 数据库结构

```sql
-- 用户表
Users (Id, OpenClawUserId, DefaultCurrency)

-- 账户表
Accounts (Id, UserId, Name, Type)

-- 交易表
Transactions (Id, UserId, Date, Payee, Note)

-- 分录表（复式记账核心）
Postings (Id, TransactionId, AccountId, Amount, Currency)
```

## 环境变量

| 变量名 | 说明 | 默认值 |
|--------|------|--------|
| `ACCOUNTING_DB_PATH` | 数据库文件路径 | `./accounting.db` |

## 示例工作流

```bash
# 1. 记录一笔午餐消费
OpenClawAccounting record '{"UserId":"banned","Transaction":{"Payee":"寿司郎","Note":"晚餐","Postings":[{"AccountName":"支出:餐饮","Amount":128},{"AccountName":"资产:信用卡","Amount":-128}]}}'

# 2. 查询最近消费
OpenClawAccounting query '{"UserId":"banned","Limit":5}'

# 3. 查看账户余额
OpenClawAccounting balance '{"UserId":"banned"}'

# 4. 查看本月支出统计
OpenClawAccounting summary '{"UserId":"banned","StartDate":"2026-03-01","EndDate":"2026-03-31","GroupByPayee":true}'

# 5. 发现商家名称不统一，批量修改
OpenClawAccounting batch-update '{"UserId":"banned","OldPayee":"麦当劳","NewPayee":"麦当劳(金拱门)"}'

# 6. 发现录入错误，创建冲正交易
OpenClawAccounting reverse '{"UserId":"banned","TransactionId":"xxx","Reason":"金额错误，应为 138 元"}'

# 7. 重新记录正确金额
OpenClawAccounting record '{"UserId":"banned","Transaction":{"Payee":"寿司郎","Note":"晚餐（修正）","Postings":[{"AccountName":"支出:餐饮","Amount":138},{"AccountName":"资产:信用卡","Amount":-138}]}}'
```

## 技术栈

- **.NET 10** - 运行时
- **Entity Framework Core** - ORM
- **SQLite** - 数据库
- **System.Text.Json** - JSON 序列化（AOT 兼容）

## 许可证

Apache-2.0
