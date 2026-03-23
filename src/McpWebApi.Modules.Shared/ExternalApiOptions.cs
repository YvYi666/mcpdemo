namespace McpWebApi.Modules.Shared;

/// <summary>
/// 外部 API 配置模型
/// </summary>
public class ExternalApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Account { get; set; } = "admin";
    public string Password { get; set; } = "Cict#S80dp";
    public string AppId { get; set; } = "201912181131469";
    public string DeviceId { get; set; } = "127.321664-61561_zhszjqhtml";
    public string LoginProductId { get; set; } = "fyylxxxt";
    public string OrgId { get; set; } = "1";

    /// <summary>
    /// 科室ID，配置后会在登录后自动切换到该科室获取带科室信息的 Token。留空则不切换。
    /// </summary>
    public string DeptId { get; set; } = string.Empty;
}
