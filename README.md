# HIS MCP 智能体平台

基于 ASP.NET Core + MCP (Model Context Protocol) 的医院信息系统智能体服务平台。提供 MCP 工具服务、智能体管理 REST API、角色权限过滤，配合前端 MCP 管理模块（`hf.inpnurse.ui`）实现智能体编排和 MCP 服务注册。

## 架构总览

```
┌─────────────────────────────────────────────────────────────────────┐
│                     前端 (hf.inpnurse.ui)                           │
│                                                                     │
│  ┌─────────────────────┐     ┌──────────────────────────────┐       │
│  │  智能体编排           │     │  MCP 服务注册                 │       │
│  │  AgentOrchestration  │     │  McpServiceRegistry           │       │
│  │                     │     │                              │       │
│  │  - 角色多选(字典API) │     │  - 注册外部 MCP 服务          │       │
│  │  - MCP 数据源多选    │     │  - Streamable HTTP / SSE 连接 │       │
│  │  - 工具勾选(内+外)   │     │  - 飞书 MCP 代理认证          │       │
│  │  - 提示词编辑        │     │  - 实时工具发现预览            │       │
│  │  - 工具描述编辑      │     │  - 卡片/表格双视图            │       │
│  │  - 卡片/KendoGrid   │     │                              │       │
│  └──────────┬──────────┘     └──────────────┬───────────────┘       │
└─────────────┼───────────────────────────────┼───────────────────────┘
              │ REST API                      │ REST API + MCP 协议
              ▼                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   McpWebApi (ASP.NET Core, :10011)                   │
│                                                                     │
│  ┌──────────────────┐  ┌───────────────┐  ┌──────────────────────┐  │
│  │  REST API         │  │  MCP 端点     │  │  代理服务             │  │
│  │  /api/agents     │  │  /mcp         │  │  /api/feishu/token   │  │
│  │  /api/tool-catalog│  │  (Streamable  │  │  /api/feishu/mcp-proxy│ │
│  │  /api/mcp-services│  │   HTTP)       │  │                      │  │
│  │  /api/tools      │  │              │  │                      │  │
│  └────────┬─────────┘  └──────┬───────┘  └──────────────────────┘  │
│           │                    │                                     │
│  ┌────────┴────────────────────┴────────────────────────────────┐   │
│  │                       核心组件                                │   │
│  │  TokenProvider        - HIS 登录、Token 管理、角色提取        │   │
│  │  ToolRoleMapProvider  - 扫描 [AllowedRoles]，共享角色工具映射  │   │
│  │  RoleToolFilter       - MCP 运行时角色过滤 (Layer 1)          │   │
│  │  ToolDescriptionOverride - 工具描述热重载                     │   │
│  └──────────────────────────────────────────────────────────────┘   │
│           │                                                         │
│  ┌────────┴─────────────────────────────────────────────────────┐   │
│  │                       业务模块                                │   │
│  │  InpNurse  [角色 B]  query_in_hospital_patient_list          │   │
│  │  InpDoctor [角色 A]  get_crisis_value_list                   │   │
│  │                      get_crisis_value_detail                 │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
              │
              ▼
    HIS 后端 (ExternalApi)
    - 登录认证 /api/up/Login/Login
    - 字典数据 /api/center/Public/GetDicItemsList
    - 业务 API
```

## 目录结构

```
mcpdemo/src/
├── McpWebApi/                          # 主项目（启动入口）
│   ├── Program.cs                      # 服务注册 + 中间件管道
│   ├── Controllers/
│   │   └── AgentsController.cs         # REST API（智能体 CRUD + 工具目录 + MCP 服务 + 飞书代理）
│   ├── agents.json                     # 智能体配置数据
│   ├── mcp-services.json               # 已注册的外部 MCP 服务连接配置
│   ├── tool-descriptions.json          # 工具描述覆盖（支持热重载）
│   └── appsettings.json                # 应用配置（端口、HIS 认证）
│
├── McpWebApi.Modules.Shared/           # 共享模块
│   ├── Models/
│   │   ├── AgentConfig.cs              # 智能体数据模型（roleCodes 数组、mcpServiceIds、tools）
│   │   └── McpServiceConfig.cs         # MCP 服务连接配置模型
│   ├── ToolRoleMapProvider.cs          # 角色-工具映射共享单例
│   ├── RoleToolFilter.cs               # MCP 运行时角色过滤器
│   ├── ToolDescriptionOverride.cs      # 工具描述 JSON 热重载
│   ├── TokenProvider.cs                # HIS Token 自动获取与刷新
│   ├── TokenAuthHandler.cs             # HTTP 请求自动注入 Token
│   ├── AllowedRolesAttribute.cs        # [AllowedRoles("A,B")] 角色标记
│   └── ExternalApiOptions.cs           # HIS 外部 API 配置
│
├── McpWebApi.Modules.InpNurse/         # 住院护士模块
│   ├── Tools/NurseTools.cs             # [AllowedRoles("B")] query_in_hospital_patient_list
│   └── Services/NurseService.cs        # 护士业务逻辑
│
└── McpWebApi.Modules.InpDoctor/        # 住院医生模块
    ├── Tools/CrisisValueTools.cs       # [AllowedRoles("A")] get_crisis_value_list, get_crisis_value_detail
    └── Services/CrisisValueService.cs  # 危急值业务逻辑
```

## 核心功能

### 1. MCP 工具服务 (`/mcp`)

通过 Streamable HTTP 协议暴露 MCP 工具，供 AI Agent（Claude Desktop、Cursor 等）调用。

| 工具名 | 角色 | 功能 |
|--------|------|------|
| `query_in_hospital_patient_list` | B (护士) | 查询住院患者列表 |
| `get_crisis_value_list` | A (医生) | 查询危急值预警列表 |
| `get_crisis_value_detail` | A (医生) | 查询危急值详情 |

### 2. 两层权限过滤

```
MCP Client 请求 tools/list
  │
  ▼ Layer 1: RoleToolFilter（硬安全）
     基于登录用户 Token 中的角色（data.prop 字段）
     护士(B) 只能看到护士工具，医生(A) 只能看到医生工具
  │
  ▼ Layer 2: AgentToolFilter（软裁剪，待实现）
     基于智能体配置中勾选的工具子集
     即使角色允许 3 个工具，智能体只勾选了 2 个，则只返回 2 个
  │
  ▼ 最终结果 = 两层交集
```

### 3. 智能体管理 REST API

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/agents` | GET | 获取全部智能体 |
| `/api/agents/{id}` | GET / PUT / DELETE | 单个智能体 CRUD |
| `/api/agents` | POST | 新建智能体 |
| `/api/tool-catalog` | GET | 统一工具目录（支持 `?roleCodes=A,B` 过滤） |
| `/api/tools` | GET | 可用工具列表（兼容旧接口） |
| `/api/tool-descriptions` | GET / PUT | 工具描述读写（PUT 触发热重载） |

### 4. MCP 服务注册 REST API

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/mcp-services` | GET | 获取已注册的外部 MCP 服务列表 |
| `/api/mcp-services` | POST | 注册新的 MCP 服务（只存连接配置，不存工具） |
| `/api/mcp-services/{id}` | PUT / DELETE | 更新/删除 MCP 服务 |

### 5. 飞书 MCP 代理（解决浏览器 CORS）

| 端点 | 说明 |
|------|------|
| `/api/feishu/token` | 代理请求飞书 Token API，用 App ID + Secret 换取 Tenant Access Token |
| `/api/feishu/mcp-proxy` | 代理转发 MCP 请求到 `mcp.feishu.cn`，携带 `X-Lark-MCP-TAT` 等自定义 Header |

## 前端模块 (hf.inpnurse.ui)

位于 `hf.inpnurse.ui/views/nurseStation/McpManage/`，采用 app.js 独立加载模式（可 iframe 嵌入）。

### 智能体编排 (AgentOrchestration)

管理 AI 智能体的角色配置、工具挂载、提示词编辑。

```
views/nurseStation/McpManage/AgentOrchestration.html
views/js/McpManage/AgentOrchestration.js
views/css/McpManage/AgentOrchestration.css / .less
```

**功能：**
- **卡片列表视图**：展示所有智能体，显示名称、角色、工具数、状态
- **编辑视图**（4 个区块）：
  1. 基本信息：名称、角色多选（从 HIS 字典 `b_staff_prop` 动态加载）、业务描述
  2. 系统提示词：可编辑 Prompt，提供医学专用模板
  3. MCP 工具挂载：
     - MCP 数据源多选（从 `/api/mcp-services` 加载已注册服务）
     - 选中数据源后实时连接发现外部工具（支持飞书代理认证）
     - 内部工具按角色过滤（`/api/tool-catalog?roleCodes=A,B`）
     - 内部+外部工具合并去重，支持卡片/KendoGrid 双视图切换
     - 内部工具可编辑描述（弹窗修改 → PUT `/api/tool-descriptions`）
     - KendoGrid 支持排序、列宽拖动、全选
  4. 常用问题管理：添加/删除/排序快捷问题

**数据存储**：`agents.json`

```json
{
  "id": "agent-001",
  "name": "护士交班助手",
  "roleCodes": ["B"],
  "mcpServiceIds": ["svc-feishu01"],
  "tools": ["query_in_hospital_patient_list", "fetch-doc"],
  "systemPrompt": "...",
  "quickQuestions": ["当前病区在院患者有哪些？"]
}
```

### MCP 服务注册 (McpServiceRegistry)

连接任意 MCP Server，发现其工具能力（类似 MCP Inspector）。

```
views/nurseStation/McpManage/McpServiceRegistry.html
views/js/McpManage/McpServiceRegistry.js
views/css/McpManage/McpServiceRegistry.css / .less
```

**功能：**
- **卡片列表视图**：展示已注册的 MCP 服务，显示名称、URL、协议、状态
- **编辑视图**（左表单 + 右深色预览面板）：
  - 基础配置：服务名、传输协议（Streamable HTTP / SSE）、端点 URL、描述
  - 身份验证：无鉴权 / App Token / 飞书 MCP（App ID + Secret + Allowed Tools）
  - 测试连接：实时执行 MCP 握手（initialize → initialized → tools/list）
  - 能力发现预览：深色面板展示服务器信息 + 发现的工具列表及参数

**支持的连接协议：**
- **Streamable HTTP**：POST 到单一端点，响应为 JSON 或 SSE
- **Legacy SSE**：GET 建立 EventSource → 接收 endpoint 事件 → POST JSON-RPC
- **飞书 MCP**：通过后端代理换取 TAT + 转发请求（绕过浏览器 CORS）

**数据存储**：`mcp-services.json`（只存连接配置，不存工具）

## 数据文件说明

| 文件 | 用途 | 读写方 |
|------|------|--------|
| `agents.json` | 智能体配置（角色、工具、提示词等） | 前端 AgentOrchestration ↔ REST API |
| `mcp-services.json` | 外部 MCP 服务连接配置 | 前端 McpServiceRegistry ↔ REST API |
| `tool-descriptions.json` | 内部工具描述覆盖（支持热重载） | 前端编辑弹窗 ↔ REST API → FileSystemWatcher |
| `appsettings.json` | 应用配置（端口、HIS 认证信息） | 手动配置 |

## 快速开始

### 1. 配置 HIS 连接

编辑 `src/McpWebApi/appsettings.json`：

```json
{
  "ExternalApi": {
    "BaseUrl": "http://10.10.1.193:8081",
    "Account": "admin",
    "Password": "your_password",
    "DeptId": "your_dept_id"
  }
}
```

### 2. 启动后端

```bash
cd src/McpWebApi
dotnet run
```

服务启动后监听 `http://localhost:10011`

### 3. 测试 API

```bash
# 智能体列表
curl http://localhost:10011/api/agents

# 工具目录（按角色过滤）
curl http://localhost:10011/api/tool-catalog?roleCodes=A,B

# MCP 服务列表
curl http://localhost:10011/api/mcp-services

# MCP 连接测试
curl -X POST http://localhost:10011/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream, application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
```

### 4. 前端页面

在 `hf.inpnurse.ui` 项目中：

- 智能体编排：`views/nurseStation/McpManage/AgentOrchestration.html`
- MCP 服务注册：`views/nurseStation/McpManage/McpServiceRegistry.html`

两个页面通过 `cict.sdp.app.js` 独立加载，可 iframe 嵌入到护士站 SPA。
