using McpWebApi.Modules.InpNurse.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpWebApi.Modules.InpNurse.Tools;

[McpServerToolType]
public class NurseTools
{
    private readonly INurseService _nurseService;
    private readonly ILogger<NurseTools> _logger;

    public NurseTools(INurseService nurseService, ILogger<NurseTools> logger)
    {
        _nurseService = nurseService;
        _logger = logger;
    }

    [McpServerTool, Description("查询住院患者列表，返回当前病区的在院患者信息")]
    public async Task<string> QueryInHospitalPatientList(
        [Description("搜索关键词，可按患者姓名、床号等搜索（可选）")] string? keyword = null,
        [Description("科室ID（可选）")] string? deptId = null,
        [Description("患者状态：in=在院，out=出院（默认 in）")] string status = "in",
        [Description("查询范围：ward=病区，dept=科室（默认 ward）")] string scope = "ward",
        [Description("是否包含空床位（默认 true）")] bool emptyBed = true)
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
