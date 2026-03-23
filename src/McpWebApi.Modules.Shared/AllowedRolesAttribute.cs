namespace McpWebApi.Modules.Shared;

/// <summary>
/// 标记 MCP Tool 类所需的角色代码，只有拥有指定角色的用户才能看到和调用该工具。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class AllowedRolesAttribute : Attribute
{
    public HashSet<string> Roles { get; }

    /// <param name="roles">允许的角色代码，多个用逗号分隔，如 "A,B"</param>
    public AllowedRolesAttribute(string roles)
    {
        Roles = new HashSet<string>(
            roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}
