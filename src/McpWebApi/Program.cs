using McpWebApi.Modules.InpNurse;
using McpWebApi.Modules.InpDoctor;
using McpWebApi.Modules.Shared;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. 注册公共服务
// ============================================================
builder.Services.Configure<ExternalApiOptions>(
    builder.Configuration.GetSection("ExternalApi"));

// Token 自动获取与刷新
var baseUrl = builder.Configuration["ExternalApi:BaseUrl"]!;
builder.Services.AddHttpClient("TokenLogin", client =>
{
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddSingleton<TokenProvider>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("TokenLogin");
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ExternalApiOptions>>();
    var logger = sp.GetRequiredService<ILogger<TokenProvider>>();
    return new TokenProvider(httpClient, options, logger);
});
builder.Services.AddTransient<TokenAuthHandler>();

// ============================================================
// 2. 注册 HTTP API 相关服务
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// 3. 注册 MCP Server（HTTP 传输）
// ============================================================
var mcpBuilder = builder.Services
    .AddMcpServer()
    .WithHttpTransport();

// ============================================================
// 4. 注册各业务模块
// ============================================================
builder.AddInpNurseModule(mcpBuilder);    // 住院护士站
builder.AddInpDoctorModule(mcpBuilder);   // 住院医生站

// ============================================================
// 5. 注册基于角色的 MCP Tool 过滤器
// ============================================================
mcpBuilder.WithRoleBasedToolFilter(
    builder.Services,
    typeof(McpWebApi.Modules.InpNurse.Tools.NurseTools).Assembly,
    typeof(McpWebApi.Modules.InpDoctor.Tools.CrisisValueTools).Assembly);

// ============================================================
// 6. 注册 Tool Description 热重载（从外部 JSON 配置覆盖描述）
// ============================================================
mcpBuilder.WithToolDescriptionOverride("tool-descriptions.json");

var app = builder.Build();

// ============================================================
// 中间件管道
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapMcp("/mcp");

app.Run();
