using System.Text.Json.Serialization;

namespace McpWebApi.Modules.Shared.Models;

public class McpServiceConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("transport")]
    public string Transport { get; set; } = "streamable-http";

    [JsonPropertyName("authType")]
    public string AuthType { get; set; } = "none";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "disconnected";

    // 飞书 MCP 鉴权字段
    [JsonPropertyName("feishuAppId")]
    public string? FeishuAppId { get; set; }

    [JsonPropertyName("feishuAppSecret")]
    public string? FeishuAppSecret { get; set; }

    [JsonPropertyName("feishuAllowedTools")]
    public string? FeishuAllowedTools { get; set; }

    [JsonPropertyName("lastConnectedAt")]
    public DateTime? LastConnectedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class McpServicesStore
{
    [JsonPropertyName("services")]
    public List<McpServiceConfig> Services { get; set; } = new();
}
