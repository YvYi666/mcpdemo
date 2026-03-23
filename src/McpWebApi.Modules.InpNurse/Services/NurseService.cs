namespace McpWebApi.Modules.InpNurse.Services;

/// <summary>
/// 护理站外部 API 代理服务
/// 通过 HttpClient 调用外部系统接口
/// </summary>
public class NurseService : INurseService
{
    private readonly HttpClient _httpClient;

    public NurseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> QueryInHospitalPatientListAsync(
        string? keyword = null,
        string? deptId = null,
        string status = "in",
        string scope = "ward",
        bool emptyBed = true)
    {
        var query = $"api/inp/NurseVisit/QueryInHospitaPtList?1=1" +
                    $"&careOperateType=0" +
                    $"&cndlvl=" +
                    $"&moreCondition=0" +
                    $"&moreConditionName={Uri.EscapeDataString("无")}" +
                    $"&emptyBed={emptyBed.ToString().ToLower()}" +
                    $"&keyword={Uri.EscapeDataString(keyword ?? "")}" +
                    $"&deptId={Uri.EscapeDataString(deptId ?? "")}" +
                    $"&status={Uri.EscapeDataString(status)}" +
                    $"&scope={Uri.EscapeDataString(scope)}" +
                    $"&ptType=" +
                    $"&ptSign=" +
                    $"&hasToday=false" +
                    $"&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        var response = await _httpClient.GetAsync(query);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
