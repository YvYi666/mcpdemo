namespace McpWebApi.Modules.InpNurse.Services;

public interface INurseService
{
    /// <summary>
    /// 查询住院患者列表
    /// </summary>
    Task<string> QueryInHospitalPatientListAsync(
        string? keyword = null,
        string? deptId = null,
        string status = "in",
        string scope = "ward",
        bool emptyBed = true);
}
