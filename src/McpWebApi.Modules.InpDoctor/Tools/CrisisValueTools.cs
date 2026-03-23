using McpWebApi.Modules.InpDoctor.Services;
using McpWebApi.Modules.Shared;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
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

    [McpServerTool]
    public async Task<string> GetCrisisValueList(
        string startTime,
        string endTime,
        string status = "2")
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

    [McpServerTool]
    public async Task<string> GetCrisisValueDetail(
        string id)
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
