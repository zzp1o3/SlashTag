using CircleLight.Data;
using CircleLight.Mcp;
using CircleLight.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdio 是协议通道，日志必须全部走 stderr。
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new TaskStore(AppPaths.DatabasePath));
builder.Services.AddSingleton<TodoTools>();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TodoTools>();

await builder.Build().RunAsync();
