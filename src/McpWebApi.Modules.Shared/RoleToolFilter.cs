using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpWebApi.Modules.Shared;

/// <summary>
/// 基于角色的 MCP Tool 过滤器，根据 TokenProvider.UserRoles 过滤工具列表和调用权限。
/// 依赖 ToolRoleMapProvider 获取 tool → roles 映射。
/// </summary>
public static class RoleToolFilter
{
    /// <summary>
    /// 注册基于角色的工具过滤器。使用 ToolRoleMapProvider（需提前注册为单例）
    /// 通过 SDK 过滤器拦截列表和调用请求。
    /// </summary>
    public static IMcpServerBuilder WithRoleBasedToolFilter(
        this IMcpServerBuilder mcpBuilder,
        IServiceCollection services)
    {
        ILogger? _logger = null;
        TokenProvider? _tokenProvider = null;
        ToolRoleMapProvider? _mapProvider = null;

        void EnsureResolved(IServiceProvider? contextServices)
        {
            if (_tokenProvider is not null)
                return;

            var sp = contextServices;
            if (sp is null)
                return;

            _tokenProvider = sp.GetService<TokenProvider>();
            _mapProvider = sp.GetService<ToolRoleMapProvider>();
            _logger = sp.GetService<ILoggerFactory>()?.CreateLogger("McpWebApi.Modules.Shared.RoleToolFilter");

            if (_tokenProvider is null)
                _logger?.LogWarning("[RoleToolFilter] 无法从 DI 容器解析 TokenProvider，角色过滤将不生效");
            if (_mapProvider is null)
                _logger?.LogWarning("[RoleToolFilter] 无法从 DI 容器解析 ToolRoleMapProvider");
        }

        async Task EnsureLoginAsync(CancellationToken cancellationToken)
        {
            if (_tokenProvider is null)
                return;
            await _tokenProvider.GetTokenAsync(cancellationToken);
        }

        mcpBuilder.WithRequestFilters(filters =>
        {
            // 过滤工具列表：移除用户无权限的工具
            filters.AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                var result = await next(context, cancellationToken);

                EnsureResolved(context.Services);

                if (_tokenProvider is null || _mapProvider is null)
                {
                    _logger?.LogWarning("[RoleToolFilter] TokenProvider 或 ToolRoleMapProvider 未就绪，返回空工具列表");
                    result.Tools = [];
                    return result;
                }

                await EnsureLoginAsync(cancellationToken);

                var userRoles = _tokenProvider.UserRoles;
                if (userRoles.Count == 0)
                {
                    _logger?.LogWarning("[RoleToolFilter] 用户角色为空，返回空工具列表");
                    result.Tools = [];
                    return result;
                }

                var toolRoleMap = _mapProvider.ToolRoleMap;
                var before = result.Tools.Count;

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
                var toolRoleMap = _mapProvider?.ToolRoleMap;

                if (context.Params is not null && toolRoleMap is not null)
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
