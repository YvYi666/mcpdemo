using System.Text;
using System.Text.Json;

namespace McpWebApi.Modules.InpDoctor.Services;

/// <summary>
/// 危急值预警外部 API 代理服务（住院医生站）
/// </summary>
public class CrisisValueService : ICrisisValueService
{
    private readonly HttpClient _httpClient;

    public CrisisValueService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetCrisisValueListAsync(string startTime, string endTime, string status = "2")
    {
        var body = JsonSerializer.Serialize(new { startTime, endTime, status });
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("api/inp/CrisisValue/GetCrisisValueList", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetCrisisValueDetailAsync(string id)
    {
        var body = JsonSerializer.Serialize(new { id });
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("api/inp/CrisisValue/GetCrisisValueDetail", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
