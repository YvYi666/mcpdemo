using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpWebApi.Modules.Shared;

/// <summary>
/// 基于角色的 MCP Tool 过滤器，根据 TokenProvider.UserRoles 过滤工具列表和调用权限。
/// </summary>
public static class RoleToolFilter
{
    /// <summary>
    /// 将 PascalCase 方法名转为 snake_case（与 MCP SDK 的默认命名策略一致）。
    /// 例如 GetCrisisValueList → get_crisis_value_list
    /// </summary>
    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 注册基于角色的工具过滤器。扫描指定程序集中标记了 [AllowedRoles] 的 Tool 类，
    /// 建立 tool name → 所需角色的映射，然后通过 SDK 过滤器拦截列表和调用请求。
    /// </summary>
    public static IMcpServerBuilder WithRoleBasedToolFilter(
        this IMcpServerBuilder mcpBuilder,
        IServiceCollection services,
        params Assembly[] assemblies)
    {
        // 扫描程序集，建立 tool name（snake_case）→ 所需角色 映射
        var toolRoleMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var rolesAttr = type.GetCustomAttribute<AllowedRolesAttribute>();
                if (rolesAttr is null)
                    continue;

                // 查找该类中所有标记了 [McpServerTool] 的方法
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                    if (toolAttr is null)
                        continue;

                    // SDK 默认将方法名转为 snake_case 作为 tool name
                    var toolName = toolAttr.Name ?? ToSnakeCase(method.Name);
                    toolRoleMap[toolName] = rolesAttr.Roles;
                }
            }
        }

        // 启动时打印扫描结果，便于排查
        Console.WriteLine($"[RoleToolFilter] 扫描到 {toolRoleMap.Count} 个角色映射:");
        foreach (var (name, roles) in toolRoleMap)
            Console.WriteLine($"  工具={name} → 需要角色=[{string.Join(",", roles)}]");

        ILogger? _logger = null;
        TokenProvider? _tokenProvider = null;

        void EnsureResolved(IServiceProvider? contextServices)
        {
            if (_tokenProvider is not null)
                return;

            var sp = contextServices;
            if (sp is null)
                return;

            _tokenProvider = sp.GetService<TokenProvider>();
            _logger = sp.GetService<ILoggerFactory>()?.CreateLogger("McpWebApi.Modules.Shared.RoleToolFilter");

            if (_tokenProvider is null)
                _logger?.LogWarning("[RoleToolFilter] 无法从 DI 容器解析 TokenProvider，角色过滤将不生效");
        }

        /// <summary>
        /// 确保登录已完成、UserRoles 已提取。
        /// 首次调用会触发 TokenProvider.GetTokenAsync() 完成登录。
        /// </summary>
        async Task EnsureLoginAsync(CancellationToken cancellationToken)
        {
            if (_tokenProvider is null)
                return;

            // GetTokenAsync 内部有缓存，不会重复登录
            await _tokenProvider.GetTokenAsync(cancellationToken);
        }

        mcpBuilder.WithRequestFilters(filters =>
        {
            // 过滤工具列表：移除用户无权限的工具
            filters.AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                var result = await next(context, cancellationToken);

                EnsureResolved(context.Services);

                if (_tokenProvider is null)
                {
                    _logger?.LogWarning("[RoleToolFilter] TokenProvider 未就绪，返回空工具列表");
                    result.Tools = [];
                    return result;
                }

                // 确保登录已完成，UserRoles 已填充
                await EnsureLoginAsync(cancellationToken);

                var userRoles = _tokenProvider.UserRoles;
                if (userRoles.Count == 0)
                {
                    _logger?.LogWarning("[RoleToolFilter] 用户角色为空，返回空工具列表");
                    result.Tools = [];
                    return result;
                }

                var before = result.Tools.Count;
                _logger?.LogInformation("[RoleToolFilter] SDK 工具列表: {Tools}",
                    string.Join(", ", result.Tools.Select(t => t.Name)));
                result.Tools = result.Tools
                    .Where(tool =>
                    {
                        if (!toolRoleMap.TryGetValue(tool.Name, out var requiredRoles))
                            return true; // 未标记角色的工具，所有人可见

                        var allowed = requiredRoles.Any(r => userRoles.Contains(r));
                        if (!allowed)
                            _logger?.LogInformation("[RoleToolFilter] 工具 {ToolName} 被过滤，需要角色: {Required}，用户角色: {UserRoles}",
                                tool.Name, string.Join(",", requiredRoles), string.Join(",", userRoles));
                        return allowed;
                    })
                    .ToList();

                _logger?.LogInformation("[RoleToolFilter] 工具过滤完成: {Before} → {After}，用户角色: {Roles}",
                    before, result.Tools.Count, string.Join(",", userRoles));

                return result;
            });

            // 拦截未授权的工具调用
            filters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                EnsureResolved(context.Services);
                await EnsureLoginAsync(cancellationToken);

                var userRoles = _tokenProvider?.UserRoles;

                // 无角色时拒绝所有需要角色的工具调用
                if (context.Params is not null)
                {
                    var toolName = context.Params.Name;
                    if (toolRoleMap.TryGetValue(toolName, out var requiredRoles))
                    {
                        if (userRoles is null || userRoles.Count == 0 || !requiredRoles.Any(r => userRoles.Contains(r)))
                        {
                            var rolesDisplay = userRoles is not null ? string.Join(",", userRoles) : "无";
                            _logger?.LogWarning("[RoleToolFilter] 未授权调用工具 {ToolName}，需要角色: {Required}，用户角色: {UserRoles}",
                                toolName, string.Join(",", requiredRoles), rolesDisplay);

                            return new CallToolResult
                            {
                                IsError = true,
                                Content = [new TextContentBlock { Text = $"权限不足：工具 {toolName} 需要角色 [{string.Join(",", requiredRoles)}]，当前用户角色 [{rolesDisplay}]" }]
                            };
                        }
                    }
                }

                return await next(context, cancellationToken);
            });
        });

        return mcpBuilder;
    }
}
