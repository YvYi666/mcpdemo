# CLAUDE.md

## 项目概述

ASP.NET Core + MCP Server 模块化单体项目（McpWebApi），同时提供 HTTP API 和 MCP 端点，按业务站点隔离为独立模块，共享同一套 Service 业务逻辑层。

## 技术栈

- **框架**: ASP.NET Core (.NET 10)
- **MCP SDK**: ModelContextProtocol 1.1.0 + ModelContextProtocol.AspNetCore 1.1.0
- **API 文档**: Swashbuckle (Swagger)
- **反向代理**: nginx（配置见 `nginx_1.conf`）
- **数据存储**: 内存模拟数据（示例项目）

## 项目结构

```
demo_mcp/
├── McpWebApi.slnx                         → 解决方案文件
│
├── src/
│   ├── McpWebApi/                         → 主入口项目（Host）
│   │   ├── Program.cs                     → 启动，注册所有模块
│   │   ├── McpWebApi.csproj               → 引用各模块
│   │   └── appsettings.json
│   │
│   ├── McpWebApi.Modules.Shared/          → 公共模块
│   │   ├── McpWebApi.Modules.Shared.csproj
│   │   ├── ExternalApiOptions.cs          → 外部 API 配置模型
│   │   └── Models/
│   │       ├── Order.cs
│   │       └── Product.cs
│   │
│   ├── McpWebApi.Modules.InpNurse/        → 住院护士站模块
│   │   ├── McpWebApi.Modules.InpNurse.csproj
│   │   ├── InpNurseModule.cs              → 模块注册入口
│   │   ├── Services/
│   │   │   ├── INurseService.cs
│   │   │   └── NurseService.cs
│   │   └── Tools/
│   │       └── NurseTools.cs
│   │
│   ├── McpWebApi.Modules.InpDoctor/       → 住院医生站模块
│   │   ├── McpWebApi.Modules.InpDoctor.csproj
│   │   ├── InpDoctorModule.cs             → 模块注册入口
│   │   ├── Services/
│   │   │   ├── ICrisisValueService.cs
│   │   │   └── CrisisValueService.cs
│   │   └── Tools/
│   │       └── CrisisValueTools.cs
│   │
│   └── McpWebApi.Modules.Demo/            → 示例模块（保留原有 Order/Product 演示）
│       ├── McpWebApi.Modules.Demo.csproj
│       ├── DemoModule.cs
│       ├── Services/
│       │   ├── OrderService.cs
│       │   └── ProductService.cs
│       ├── Tools/
│       │   ├── OrderTools.cs
│       │   └── ProductTools.cs
│       └── Controllers/
│           ├── OrdersController.cs
│           └── ProductsController.cs
│
├── nginx_1.conf
└── CLAUDE.md
```

## 架构要点

- **模块化单体**: 按业务站点（护士站、医生站、示例）隔离为独立 class library 项目
- **两个入口，一套逻辑**: HTTP API (`/api/*`) 和 MCP 端点 (`/mcp`) 共享 Service 层
- **MCP Tools 是 Service 的薄包装**: 仅负责添加 `[McpServerTool]` 描述 + 序列化返回值
- **模块注册模式**: 每个模块提供 `Add{Module}Module(WebApplicationBuilder, IMcpServerBuilder)` 扩展方法
- **DI 注入**: 所有 Service 通过模块注册方法注册，Controller 和 Tool 通过构造函数注入

## 常用命令

```bash
# 安装依赖
dotnet restore McpWebApi.slnx

# 编译
dotnet build McpWebApi.slnx

# 运行服务（监听 http://0.0.0.0:10010）
dotnet run --project src/McpWebApi

# 测试 HTTP API
curl http://localhost:10010/api/orders/ORD-001
curl http://localhost:10010/api/products?keyword=键盘

# 测试 MCP 端点
npx @modelcontextprotocol/inspector   # 连接地址: http://localhost:10010/mcp
```

## 添加新模块的标准流程

1. 在 `src/` 下创建 `McpWebApi.Modules.{ModuleName}/` 目录
2. 创建 `McpWebApi.Modules.{ModuleName}.csproj`（class library，引用 Shared + MCP SDK + ASP.NET Core FrameworkReference）
3. 添加 `Services/` 目录 — 接口 + 实现
4. 添加 `Tools/` 目录 — MCP Tool 包装类（如需要）
5. 添加 `Controllers/` 目录 — HTTP API Controller（如需要）
6. 创建 `{ModuleName}Module.cs` — 提供 `Add{ModuleName}Module(WebApplicationBuilder, IMcpServerBuilder)` 扩展方法
7. 在 `src/McpWebApi/McpWebApi.csproj` 添加项目引用
8. 在 `src/McpWebApi/Program.cs` 调用 `builder.Add{ModuleName}Module(mcpBuilder)`
9. 将模块项目添加到解决方案: `dotnet sln add src/McpWebApi.Modules.{ModuleName}/...csproj`

## 编码规范

- 中文注释和 Description 描述（面向中文用户）
- JSON 序列化使用 `UnsafeRelaxedJsonEscaping` 以正确输出中文
- MCP Tool 方法参数必须添加 `[Description]` 特性
- 命名空间: `McpWebApi.Modules.{ModuleName}.Services` / `.Tools` / `.Controllers`
- 共享模型命名空间: `McpWebApi.Models`
