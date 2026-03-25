using System.Reflection;
using System.Text;
using ModelContextProtocol.Server;

namespace McpWebApi.Modules.Shared;

/// <summary>
/// 共享单例：扫描程序集中的 [AllowedRoles] + [McpServerTool]，
/// 建立 tool name (snake_case) → 所需角色 的映射。
/// RoleToolFilter 和 Controller 都可注入使用。
/// </summary>
public class ToolRoleMapProvider
{
    /// <summary>tool_name → 所需角色集合</summary>
    public Dictionary<string, HashSet<string>> ToolRoleMap { get; }

    public ToolRoleMapProvider(params Assembly[] assemblies)
    {
        ToolRoleMap = ScanAssemblies(assemblies);

        Console.WriteLine($"[ToolRoleMapProvider] 扫描到 {ToolRoleMap.Count} 个角色映射:");
        foreach (var (name, roles) in ToolRoleMap)
            Console.WriteLine($"  工具={name} → 需要角色=[{string.Join(",", roles)}]");
    }

    /// <summary>
    /// 按多个 roleCodes 返回可访问的工具名并集。
    /// 例如 roleCodes=["A","B"] → 返回角色A和角色B的所有工具。
    /// </summary>
    public List<string> GetToolNamesByRoles(IEnumerable<string> roleCodes)
    {
        var roleSet = new HashSet<string>(roleCodes, StringComparer.OrdinalIgnoreCase);
        return ToolRoleMap
            .Where(kv => kv.Value.Any(r => roleSet.Contains(r)))
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>获取所有已知的角色代码（去重）</summary>
    public HashSet<string> GetAllRoleCodes()
    {
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roles in ToolRoleMap.Values)
            foreach (var r in roles)
                all.Add(r);
        return all;
    }

    private static Dictionary<string, HashSet<string>> ScanAssemblies(Assembly[] assemblies)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var rolesAttr = type.GetCustomAttribute<AllowedRolesAttribute>();
                if (rolesAttr is null)
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                    if (toolAttr is null)
                        continue;

                    var toolName = toolAttr.Name ?? ToSnakeCase(method.Name);
                    map[toolName] = rolesAttr.Roles;
                }
            }
        }

        return map;
    }

    /// <summary>PascalCase → snake_case（与 MCP SDK 默认策略一致）</summary>
    internal static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
