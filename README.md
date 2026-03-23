# ASP.NET Core + MCP Server 一体化项目模板

## 架构概览

```
                    nginx (:80)
                       │
           ┌───────────┼───────────┐
           │                       │
     /api/*                  /mcp/*
     HTTP API               MCP 端点
           │                       │
           └───────────┬───────────┘
                       │
            ASP.NET Core (:10010)
                       │
              ┌────────┴────────┐
              │                 │
         Controllers        MCP Tools
         (HTTP 入口)       (MCP 入口)
              │                 │
              └────────┬────────┘
                       │
                 Service 层
              （共享业务逻辑）
                       │
                  数据库 / 外部 API
```

## 目录结构

```
McpWebApi/
├── Program.cs              # 入口：注册 HTTP + MCP 双通道
├── Controllers/            # HTTP API 控制器（前端调用）
│   └── Controllers.cs
├── Tools/                  # MCP 工具包装类（AI Agent 调用）
│   └── Tools.cs
├── Services/               # 共享业务逻辑层（两边都调这里）
│   └── Services.cs
├── Models/                 # 数据模型
│   └── Models.cs
├── nginx.conf              # nginx 反向代理配置
└── appsettings.json        # 应用配置（端口 10010）
```

## 核心概念

### 两个入口，一套逻辑

- **HTTP API**（`/api/*`）：给前端、移动端、其他微服务调用
- **MCP 端点**（`/mcp`）：给 AI Agent、Claude Desktop、Cursor 等 MCP Client 调用

两个入口调用的是**同一个 Service 层**，业务逻辑不重复。

### MCP 工具 = Service 的薄包装

`Tools/` 目录下的类只做两件事：
1. 用 `[McpServerTool]` 和 `[Description]` 特性描述工具（让 LLM 知道这个工具干什么）
2. 调用 Service 层方法，把结果序列化为字符串返回

## 快速开始

### 1. 安装依赖

```bash
cd McpWebApi
dotnet restore
```

### 2. 运行服务

```bash
dotnet run
```

服务启动后监听 `http://0.0.0.0:10010`

### 3. 测试 HTTP API

```bash
# 查询订单
curl http://localhost:10010/api/orders/ORD-001

# 搜索产品
curl http://localhost:10010/api/products?keyword=键盘

# 查询客户订单
curl http://localhost:10010/api/orders/by-customer/张三
```

### 4. 测试 MCP 端点

使用 MCP Inspector 测试：
```bash
npx @modelcontextprotocol/inspector
```

连接地址填入：`http://localhost:10010/mcp`

或者用 Postman 手动测试：
```bash
# 获取工具列表
curl -X POST http://localhost:10010/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2025-03-26",
      "capabilities": {},
      "clientInfo": { "name": "test", "version": "1.0" }
    }
  }'
```

### 5. 配置 nginx

```bash
# 将 nginx.conf 复制到 nginx 配置目录
sudo cp nginx.conf /etc/nginx/sites-available/mcpwebapi
sudo ln -s /etc/nginx/sites-available/mcpwebapi /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

## 如何添加新工具

假设你要暴露一个新的"用户管理"功能：

**第 1 步**：在 `Services/` 中添加 `IUserService` 接口和实现

**第 2 步**：在 `Controllers/` 中添加 `UsersController`（如果需要 HTTP API）

**第 3 步**：在 `Tools/` 中添加 `UserTools` 类：

```csharp
[McpServerToolType]
public class UserTools
{
    private readonly IUserService _userService;

    public UserTools(IUserService userService)
    {
        _userService = userService;
    }

    [McpServerTool, Description("根据用户ID查询用户信息")]
    public async Task<string> GetUser(
        [Description("用户ID")] string userId)
    {
        var user = await _userService.GetUserAsync(userId);
        return JsonSerializer.Serialize(user);
    }
}
```

**第 4 步**：在 `Program.cs` 中注册：

```csharp
builder.Services.AddScoped<IUserService, UserService>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<OrderTools>()
    .WithTools<ProductTools>()
    .WithTools<UserTools>();     // 新增
```

完成。HTTP API 和 MCP 端点同时可用，无需额外部署。

## 生产部署注意事项

1. **安全性**：MCP 端点需要加认证，可以通过 `MapMcp().RequireAuthorization()` 添加
2. **CORS**：如果前端跨域调用，需要配置 CORS 策略
3. **日志**：建议在 MCP Tools 中加入结构化日志，方便排查问题
4. **nginx SSE**：确保 nginx 配置中关闭了缓冲（`proxy_buffering off`），否则 SSE 不通
