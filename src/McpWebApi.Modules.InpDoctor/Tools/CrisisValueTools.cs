using McpWebApi.Modules.InpDoctor.Services;
using McpWebApi.Modules.Shared;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpWebApi.Modules.InpDoctor.Tools;

[McpServerToolType]
[AllowedRoles("A")]
public class CrisisValueTools
{
    private readonly ICrisisValueService _crisisValueService;
    private readonly ILogger<CrisisValueTools> _logger;

    public CrisisValueTools(ICrisisValueService crisisValueService, ILogger<CrisisValueTools> logger)
    {
        _crisisValueService = crisisValueService;
        _logger = logger;
    }

    [McpServerTool, Description("查询危急值预警列表，检索指定时间范围内出现危急值的报告")]
    public async Task<string> GetCrisisValueList(
        [Description("开始时间，格式：yyyy-MM-dd HH:mm:ss，例如 2026-03-17 00:00:00")] string startTime,
        [Description("结束时间，格式：yyyy-MM-dd HH:mm:ss，例如 2026-03-20 23:59:59")] string endTime,
        [Description("状态筛选：0=未处理，1=已处理，2=全部（默认）")] string status = "2")
    {
        _logger.LogInformation("[MCP调用] GetCrisisValueList 入参: startTime={StartTime}, endTime={EndTime}, status={Status}",
            startTime, endTime, status);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _crisisValueService.GetCrisisValueListAsync(startTime, endTime, status);
            sw.Stop();
            _logger.LogInformation("[MCP调用] GetCrisisValueList 成功 | 耗时={Elapsed}ms | 出参长度={Length}",
                sw.ElapsedMilliseconds, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[MCP调用] GetCrisisValueList 失败 | 耗时={Elapsed}ms", sw.ElapsedMilliseconds);
            return $"调用失败: {ex.Message}";
        }
    }

    [McpServerTool, Description("查询危急值详情，根据危急值记录ID获取详细信息")]
    public async Task<string> GetCrisisValueDetail(
        [Description("危急值记录ID")] string id)
    {
        _logger.LogInformation("[MCP调用] GetCrisisValueDetail 入参: id={Id}", id);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _crisisValueService.GetCrisisValueDetailAsync(id);
            sw.Stop();
            _logger.LogInformation("[MCP调用] GetCrisisValueDetail 成功 | 耗时={Elapsed}ms | 出参长度={Length}",
                sw.ElapsedMilliseconds, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[MCP调用] GetCrisisValueDetail 失败 | 耗时={Elapsed}ms", sw.ElapsedMilliseconds);
            return $"调用失败: {ex.Message}";
        }
    }
}
