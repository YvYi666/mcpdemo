using McpWebApi.Modules.InpDoctor.Services;
using McpWebApi.Modules.InpDoctor.Tools;
using McpWebApi.Modules.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace McpWebApi.Modules.InpDoctor;

public static class InpDoctorModule
{
    public static void AddInpDoctorModule(
        this WebApplicationBuilder builder, IMcpServerBuilder mcpBuilder)
    {
        var baseUrl = builder.Configuration["ExternalApi:BaseUrl"]!;

        builder.Services.AddHttpClient<ICrisisValueService, CrisisValueService>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        })
        .AddHttpMessageHandler<TokenAuthHandler>();

        mcpBuilder.WithTools<CrisisValueTools>();
    }
}
