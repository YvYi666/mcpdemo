namespace McpWebApi.Modules.InpDoctor.Services;

public interface ICrisisValueService
{
    /// <summary>
    /// 查询危急值预警列表
    /// </summary>
    Task<string> GetCrisisValueListAsync(string startTime, string endTime, string status = "2");

    /// <summary>
    /// 查询危急值详情
    /// </summary>
    Task<string> GetCrisisValueDetailAsync(string id);
}
