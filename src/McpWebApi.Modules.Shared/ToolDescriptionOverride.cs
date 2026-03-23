using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace McpWebApi.Modules.Shared;

/// <summary>
/// 单个 Tool 的描述覆盖配置
/// </summary>
public class ToolDescriptionEntry
{
    public string? Description { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

/// <summary>
/// Tool Description 热重载扩展
/// </summary>
public static class ToolDescriptionOverrideExtensions
{
    /// <summary>
    /// 注册 Tool Description 热重载过滤器，从外部 JSON 文件读取描述覆盖，支持文件变更自动重载
    /// </summary>
    public static IMcpServerBuilder WithToolDescriptionOverride(
        this IMcpServerBuilder mcpBuilder, string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        var overrides = LoadConfig(fullPath);

        // 监听文件变更，原子替换内存字典
        var dir = Path.GetDirectoryName(fullPath)!;
        var fileName = Path.GetFileName(fullPath);
        var watcher = new FileSystemWatcher(dir, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        watcher.Changed += (_, _) => OnFileChanged(fullPath, ref overrides);
        watcher.Created += (_, _) => OnFileChanged(fullPath, ref overrides);

        // 注册 ListToolsFilter，每次 tools/list 请求时用最新配置覆盖描述
        mcpBuilder.WithRequestFilters(filters =>
        {
            filters.AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                var result = await next(context, cancellationToken);

                var currentOverrides = Volatile.Read(ref overrides);
                if (currentOverrides == null || currentOverrides.Count == 0)
                    return result;

                foreach (var tool in result.Tools)
                {
                    if (!currentOverrides.TryGetValue(tool.Name, out var entry))
                        continue;

                    // 覆盖 Tool 级描述
                    if (!string.IsNullOrEmpty(entry.Description))
                    {
                        tool.Description = entry.Description;
                    }

                    // 覆盖参数级描述
                    if (entry.Parameters != null && entry.Parameters.Count > 0)
                    {
                        tool.InputSchema = OverrideParameterDescriptions(
                            tool.InputSchema, entry.Parameters);
                    }
                }

                return result;
            });
        });

        return mcpBuilder;
    }

    private static void OnFileChanged(string fullPath,
        ref Dictionary<string, ToolDescriptionEntry>? overrides)
    {
        // 延迟少许时间，避免文件写入未完成就读取
        Thread.Sleep(100);
        var newOverrides = LoadConfig(fullPath);
        Volatile.Write(ref overrides, newOverrides);
    }

    private static Dictionary<string, ToolDescriptionEntry>? LoadConfig(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, ToolDescriptionEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            // 配置文件不存在或解析失败时不影响服务
            return null;
        }
    }

    /// <summary>
    /// 修改 InputSchema (JsonElement) 中各参数的 description 字段
    /// </summary>
    private static JsonElement OverrideParameterDescriptions(
        JsonElement schema, Dictionary<string, string> paramOverrides)
    {
        try
        {
            var node = JsonNode.Parse(schema.GetRawText());
            if (node == null) return schema;

            var properties = node["properties"]?.AsObject();
            if (properties == null) return schema;

            foreach (var (paramName, newDesc) in paramOverrides)
            {
                if (properties.TryGetPropertyValue(paramName, out var paramNode)
                    && paramNode is JsonObject paramObj)
                {
                    paramObj["description"] = newDesc;
                }
            }

            return JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
        }
        catch
        {
            // 解析失败时保持原始 schema
            return schema;
        }
    }
}
