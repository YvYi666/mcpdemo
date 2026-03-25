using System.Text.Json.Serialization;

namespace McpWebApi.Modules.Shared.Models;

public class AgentConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>角色代码数组，如 ["A","B"]，支持多角色</summary>
    [JsonPropertyName("roleCodes")]
    public List<string> RoleCodes { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "debug";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>选中的 MCP 服务 ID 列表</summary>
    [JsonPropertyName("mcpServiceIds")]
    public List<string> McpServiceIds { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();

    [JsonPropertyName("quickQuestions")]
    public List<string> QuickQuestions { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AgentsStore
{
    [JsonPropertyName("agents")]
    public List<AgentConfig> Agents { get; set; } = new();
}
