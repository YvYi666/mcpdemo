using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpWebApi.Modules.Shared;

/// <summary>
/// 自动登录获取 Token，缓存并在过期前自动刷新。
/// 如果配置了 DeptId，登录后自动切换科室获取带科室信息的 Token。
/// </summary>
public class TokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly ExternalApiOptions _options;
    private readonly ILogger<TokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <summary>
    /// 当前登录用户拥有的角色代码集合，从登录响应的 data.prop 字段提取。
    /// </summary>
    public HashSet<string> UserRoles { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public TokenProvider(HttpClient httpClient, IOptions<ExternalApiOptions> options, ILogger<TokenProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_token is not null && DateTimeOffset.UtcNow.AddMinutes(5) < _expiresAt)
            return _token;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow.AddMinutes(5) < _expiresAt)
                return _token;

            // 第1步：登录
            var (loginToken, expiresAt) = await LoginAsync(cancellationToken);

            // 第2步：如果配置了科室，切换科室获取新 Token
            if (!string.IsNullOrEmpty(_options.DeptId))
            {
                (loginToken, expiresAt) = await ChangeDeptAsync(loginToken, cancellationToken);
            }

            _token = loginToken;
            _expiresAt = expiresAt;

            return _token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TokenProvider] Token 获取失败");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<(string token, DateTimeOffset expiresAt)> LoginAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[TokenProvider] 正在登录获取 Token...");

        var request = CreateRequest("api/up/Login/Login");
        var body = JsonSerializer.Serialize(new
        {
            account_num = _options.Account,
            password = _options.Password,
            orgId = _options.OrgId,
            login_product_id = _options.LoginProductId
        });
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.GetProperty("code").GetInt32();
        if (code != 200)
        {
            var msg = root.GetProperty("msg").GetString();
            throw new InvalidOperationException($"请求失败: {msg}");
        }

        var data = root.GetProperty("data");
        var token = data.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("响应中没有 token");

        var expireTimestamp = data.GetProperty("token_effective_period").GetInt64();
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expireTimestamp);

        // 提取用户角色（prop 字段，逗号分隔的角色代码）
        if (data.TryGetProperty("prop", out var propElement))
        {
            var prop = propElement.GetString();
            if (!string.IsNullOrEmpty(prop))
            {
                UserRoles = new HashSet<string>(
                    prop.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);
                _logger.LogInformation("[TokenProvider] 用户角色: {Roles}", prop);
            }
        }

        _logger.LogInformation("[TokenProvider] 登录成功，有效期至 {ExpiresAt}", expiresAt.ToLocalTime());
        return (token, expiresAt);
    }

    private async Task<(string token, DateTimeOffset expiresAt)> ChangeDeptAsync(
        string loginToken, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[TokenProvider] 正在切换科室 DeptId={DeptId}...", _options.DeptId);

        var request = CreateRequest("api/up/Login/ChangeStaffDepart");
        request.Headers.Add("Authorization", loginToken);

        var body = JsonSerializer.Serialize(new { deptId = _options.DeptId });
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var (token, expiresAt) = await SendAndParseTokenAsync(request, cancellationToken);

        _logger.LogInformation("[TokenProvider] 科室切换成功，有效期至 {ExpiresAt}", expiresAt.ToLocalTime());
        return (token, expiresAt);
    }

    private HttpRequestMessage CreateRequest(string url)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Convert.ToBase64String(Encoding.UTF8.GetBytes(timestamp));

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("appid", _options.AppId);
        request.Headers.Add("deviceid", _options.DeviceId);
        request.Headers.Add("timestamp", timestamp);
        request.Headers.Add("sign", sign);
        request.Headers.Add("inParamEn", "0");
        request.Headers.Add("outParamEn", "0");

        return request;
    }

    private async Task<(string token, DateTimeOffset expiresAt)> SendAndParseTokenAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.GetProperty("code").GetInt32();
        if (code != 200)
        {
            var msg = root.GetProperty("msg").GetString();
            throw new InvalidOperationException($"请求失败: {msg}");
        }

        var data = root.GetProperty("data");
        var token = data.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("响应中没有 token");

        var expireTimestamp = data.GetProperty("token_effective_period").GetInt64();
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expireTimestamp);

        return (token, expiresAt);
    }
}
