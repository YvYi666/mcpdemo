using McpWebApi.Modules.InpNurse.Services;
using McpWebApi.Modules.Shared;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Diagnostics;

namespace McpWebApi.Modules.InpNurse.Tools;

[McpServerToolType]
[AllowedRoles("B")]
public class NurseTools
{
    private readonly INurseService _nurseService;
    private readonly ILogger<NurseTools> _logger;

    public NurseTools(INurseService nurseService, ILogger<NurseTools> logger)
    {
        _nurseService = nurseService;
        _logger = logger;
    }

    [McpServerTool]
    public async Task<string> QueryInHospitalPatientList(
        string? keyword = null,
        string? deptId = null,
        string status = "in",
        string scope = "ward",
        bool emptyBed = true)
    {
        _logger.LogInformation("[MCP调用] QueryInHospitalPatientList 入参: keyword={Keyword}, deptId={DeptId}, status={Status}, scope={Scope}, emptyBed={EmptyBed}",
            keyword, deptId, status, scope, emptyBed);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _nurseService.QueryInHospitalPatientListAsync(
                keyword, deptId, status, scope, emptyBed);
            sw.Stop();
            _logger.LogInformation("[MCP调用] QueryInHospitalPatientList 成功 | 耗时={Elapsed}ms | 出参长度={Length}",
                sw.ElapsedMilliseconds, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[MCP调用] QueryInHospitalPatientList 失败 | 耗时={Elapsed}ms", sw.ElapsedMilliseconds);
            return $"调用失败: {ex.Message}";
        }
    }
}
