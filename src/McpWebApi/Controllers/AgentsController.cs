using System.Text.Encodings.Web;
using System.Text.Json;
using McpWebApi.Modules.Shared;
using McpWebApi.Modules.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace McpWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly string _agentsFilePath;
    private readonly string _toolDescriptionsFilePath;
    private readonly string _mcpServicesFilePath;
    private readonly ToolRoleMapProvider _toolRoleMapProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AgentsController(IWebHostEnvironment env, ToolRoleMapProvider toolRoleMapProvider)
    {
        _agentsFilePath = Path.Combine(env.ContentRootPath, "agents.json");
        _toolDescriptionsFilePath = Path.Combine(env.ContentRootPath, "tool-descriptions.json");
        _mcpServicesFilePath = Path.Combine(env.ContentRootPath, "mcp-services.json");
        _toolRoleMapProvider = toolRoleMapProvider;
    }

    // ============================================================
    // 智能体 CRUD
    // ============================================================

    /// <summary>获取全部智能体列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var store = await ReadAgentsStore();
        return Ok(store.Agents);
    }

    /// <summary>获取单个智能体</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var store = await ReadAgentsStore();
        var agent = store.Agents.FirstOrDefault(a => a.Id == id);
        if (agent == null) return NotFound(new { message = "智能体不存在" });
        return Ok(agent);
    }

    /// <summary>新建智能体</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AgentConfig agent)
    {
        var store = await ReadAgentsStore();
        agent.Id = $"agent-{Guid.NewGuid():N}"[..16];
        agent.CreatedAt = DateTime.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;
        store.Agents.Add(agent);
        await WriteAgentsStore(store);
        return CreatedAtAction(nameof(Get), new { id = agent.Id }, agent);
    }

    /// <summary>更新智能体</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] AgentConfig updated)
    {
        var store = await ReadAgentsStore();
        var index = store.Agents.FindIndex(a => a.Id == id);
        if (index < 0) return NotFound(new { message = "智能体不存在" });

        updated.Id = id;
        updated.CreatedAt = store.Agents[index].CreatedAt;
        updated.UpdatedAt = DateTime.UtcNow;
        store.Agents[index] = updated;
        await WriteAgentsStore(store);
        return Ok(updated);
    }

    /// <summary>删除智能体</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var store = await ReadAgentsStore();
        var removed = store.Agents.RemoveAll(a => a.Id == id);
        if (removed == 0) return NotFound(new { message = "智能体不存在" });
        await WriteAgentsStore(store);
        return NoContent();
    }

    // ============================================================
    // 统一工具目录
    // ============================================================

    /// <summary>
    /// 获取统一工具目录，合并内部工具（含角色信息）+ 外部发现的工具。
    /// 可选 ?roleCodes=A,B 过滤，返回角色A和B的工具并集。
    /// </summary>
    [HttpGet("/api/tool-catalog")]
    public async Task<IActionResult> GetToolCatalog([FromQuery] string? roleCodes = null)
    {
        // 解析角色过滤参数
        HashSet<string>? roleFilter = null;
        if (!string.IsNullOrWhiteSpace(roleCodes))
        {
            roleFilter = new HashSet<string>(
                roleCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        var catalog = new List<object>();

        // 1. 内部工具：从 ToolRoleMapProvider + tool-descriptions.json
        var toolDescriptions = await ReadToolDescriptions();
        foreach (var (toolName, roles) in _toolRoleMapProvider.ToolRoleMap)
        {
            // 角色过滤：如果指定了 roleCodes，只返回匹配的
            if (roleFilter is not null && !roles.Any(r => roleFilter.Contains(r)))
                continue;

            var desc = "";
            JsonElement? inputSchema = null;
            if (toolDescriptions.TryGetValue(toolName, out var descObj))
            {
                if (descObj.TryGetProperty("description", out var d)) desc = d.GetString() ?? "";
                if (descObj.TryGetProperty("parameters", out var p)) inputSchema = p;
            }

            catalog.Add(new
            {
                name = toolName,
                description = desc,
                source = "internal",
                allowedRoles = roles.ToList(),
                inputSchema
            });
        }

        return Ok(catalog);
    }

    // ============================================================
    // 工具描述（保留原有接口）
    // ============================================================

    /// <summary>获取所有可用 MCP 工具（旧接口，保持兼容）</summary>
    [HttpGet("/api/tools")]
    public async Task<IActionResult> GetTools()
    {
        var tools = await ReadToolDescriptions();
        var result = tools.Select(t => new
        {
            name = t.Key,
            description = t.Value.TryGetProperty("description", out var desc) ? desc.GetString() : "",
            parameters = t.Value.TryGetProperty("parameters", out var p) ? p : default
        });
        return Ok(result);
    }

    /// <summary>获取 tool-descriptions.json 原始内容</summary>
    [HttpGet("/api/tool-descriptions")]
    public async Task<IActionResult> GetToolDescriptions()
    {
        var json = await System.IO.File.ReadAllTextAsync(_toolDescriptionsFilePath);
        return Content(json, "application/json");
    }

    /// <summary>更新 tool-descriptions.json（触发热重载）</summary>
    [HttpPut("/api/tool-descriptions")]
    public async Task<IActionResult> UpdateToolDescriptions([FromBody] JsonElement data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await System.IO.File.WriteAllTextAsync(_toolDescriptionsFilePath, json);
        return Ok(new { message = "工具描述已更新" });
    }

    // ============================================================
    // MCP 服务 CRUD
    // ============================================================

    /// <summary>获取所有已注册的 MCP 服务</summary>
    [HttpGet("/api/mcp-services")]
    public async Task<IActionResult> GetMcpServices()
    {
        var store = await ReadMcpServicesStore();
        return Ok(store.Services);
    }

    /// <summary>新增 MCP 服务</summary>
    [HttpPost("/api/mcp-services")]
    public async Task<IActionResult> CreateMcpService([FromBody] McpServiceConfig service)
    {
        var store = await ReadMcpServicesStore();
        service.Id = $"svc-{Guid.NewGuid():N}"[..12];
        service.CreatedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;
        store.Services.Add(service);
        await WriteMcpServicesStore(store);
        return Ok(service);
    }

    /// <summary>更新 MCP 服务</summary>
    [HttpPut("/api/mcp-services/{id}")]
    public async Task<IActionResult> UpdateMcpService(string id, [FromBody] McpServiceConfig updated)
    {
        var store = await ReadMcpServicesStore();
        var index = store.Services.FindIndex(s => s.Id == id);
        if (index < 0) return NotFound(new { message = "MCP 服务不存在" });

        updated.Id = id;
        updated.CreatedAt = store.Services[index].CreatedAt;
        updated.UpdatedAt = DateTime.UtcNow;
        store.Services[index] = updated;
        await WriteMcpServicesStore(store);
        return Ok(updated);
    }

    /// <summary>代理获取飞书 Tenant Access Token（解决浏览器 CORS 限制）</summary>
    [HttpPost("/api/feishu/token")]
    public async Task<IActionResult> GetFeishuToken([FromBody] FeishuTokenRequest req)
    {
        using var client = new HttpClient();
        var resp = await client.PostAsync(
            "https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal",
            new StringContent(
                JsonSerializer.Serialize(new { app_id = req.AppId, app_secret = req.AppSecret }),
                System.Text.Encoding.UTF8,
                "application/json"));
        var body = await resp.Content.ReadAsStringAsync();
        return Content(body, "application/json");
    }

    /// <summary>代理转发 MCP 请求到飞书（解决浏览器 CORS 限制）</summary>
    [HttpPost("/api/feishu/mcp-proxy")]
    public async Task<IActionResult> FeishuMcpProxy(
        [FromHeader(Name = "X-Proxy-Url")] string proxyUrl,
        [FromHeader(Name = "X-Lark-MCP-TAT")] string? tat,
        [FromHeader(Name = "X-Lark-MCP-Allowed-Tools")] string? allowedTools)
    {
        using var client = new HttpClient();
        var body = await new StreamReader(Request.Body).ReadToEndAsync();

        var req = new HttpRequestMessage(HttpMethod.Post, proxyUrl);
        req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(tat))
            req.Headers.TryAddWithoutValidation("X-Lark-MCP-TAT", tat);
        if (!string.IsNullOrEmpty(allowedTools))
            req.Headers.TryAddWithoutValidation("X-Lark-MCP-Allowed-Tools", allowedTools);

        var resp = await client.SendAsync(req);
        var respBody = await resp.Content.ReadAsStringAsync();
        var contentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json";

        // 透传 Mcp-Session-Id
        if (resp.Headers.TryGetValues("Mcp-Session-Id", out var sessionValues))
            Response.Headers["Mcp-Session-Id"] = sessionValues.First();

        return Content(respBody, contentType);
    }

    /// <summary>删除 MCP 服务</summary>
    [HttpDelete("/api/mcp-services/{id}")]
    public async Task<IActionResult> DeleteMcpService(string id)
    {
        var store = await ReadMcpServicesStore();
        var removed = store.Services.RemoveAll(s => s.Id == id);
        if (removed == 0) return NotFound(new { message = "MCP 服务不存在" });
        await WriteMcpServicesStore(store);
        return NoContent();
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private async Task<AgentsStore> ReadAgentsStore()
    {
        if (!System.IO.File.Exists(_agentsFilePath))
            return new AgentsStore();
        var json = await System.IO.File.ReadAllTextAsync(_agentsFilePath);
        return JsonSerializer.Deserialize<AgentsStore>(json, JsonOptions) ?? new AgentsStore();
    }

    private async Task WriteAgentsStore(AgentsStore store)
    {
        var json = JsonSerializer.Serialize(store, JsonOptions);
        await System.IO.File.WriteAllTextAsync(_agentsFilePath, json);
    }

    private async Task<Dictionary<string, JsonElement>> ReadToolDescriptions()
    {
        if (!System.IO.File.Exists(_toolDescriptionsFilePath))
            return new();
        var json = await System.IO.File.ReadAllTextAsync(_toolDescriptionsFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
    }

    private async Task<McpServicesStore> ReadMcpServicesStore()
    {
        if (!System.IO.File.Exists(_mcpServicesFilePath))
            return new McpServicesStore();
        var json = await System.IO.File.ReadAllTextAsync(_mcpServicesFilePath);
        return JsonSerializer.Deserialize<McpServicesStore>(json, JsonOptions) ?? new McpServicesStore();
    }

    private async Task WriteMcpServicesStore(McpServicesStore store)
    {
        var json = JsonSerializer.Serialize(store, JsonOptions);
        await System.IO.File.WriteAllTextAsync(_mcpServicesFilePath, json);
    }
}

public class FeishuTokenRequest
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
}
