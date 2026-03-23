using System.Net.Http.Headers;
using System.Text;

namespace McpWebApi.Modules.Shared;

/// <summary>
/// HttpClient 消息处理器，自动为每个请求附加 Token 和必要请求头
/// </summary>
public class TokenAuthHandler : DelegatingHandler
{
    private readonly TokenProvider _tokenProvider;

    public TokenAuthHandler(TokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 附加 HIS 系统必要的请求头
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(timestamp));

        request.Headers.TryAddWithoutValidation("appid", "201912181131469");
        request.Headers.TryAddWithoutValidation("deviceid", "127.321664-61561_zhszjqhtml");
        request.Headers.TryAddWithoutValidation("timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("sign", sign);
        request.Headers.TryAddWithoutValidation("inParamEn", "0");
        request.Headers.TryAddWithoutValidation("outParamEn", "0");

        return await base.SendAsync(request, cancellationToken);
    }
}
