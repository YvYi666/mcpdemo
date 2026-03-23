using McpWebApi.Modules.InpNurse.Services;
using McpWebApi.Modules.InpNurse.Tools;
using McpWebApi.Modules.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace McpWebApi.Modules.InpNurse;

public static class InpNurseModule
{
    public static void AddInpNurseModule(
        this WebApplicationBuilder builder, IMcpServerBuilder mcpBuilder)
    {
        var baseUrl = builder.Configuration["ExternalApi:BaseUrl"]!;

        builder.Services.AddHttpClient<INurseService, NurseService>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        })
        .AddHttpMessageHandler<TokenAuthHandler>();

        mcpBuilder.WithTools<NurseTools>();
    }
}
