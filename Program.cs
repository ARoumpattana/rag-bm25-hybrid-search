using RagBm25HybridSearch.Configuration;
using RagBm25HybridSearch.Interface;
using RagBm25HybridSearch.Services;
using log4net.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
ConfigurationManager configuration = builder.Configuration;

var appSettings = new AppSettings();
configuration.Bind("AppSettings", appSettings);
builder.Services.AddSingleton(appSettings);

var appConfig = new AppConfig();
configuration.Bind("AppConfig", appConfig);
builder.Services.AddSingleton(appConfig);

builder.Logging.AddLog4Net();
var exePath = AppDomain.CurrentDomain.BaseDirectory;
XmlConfigurator.Configure(new FileInfo(Path.Combine(exePath, "log4net.config")));

builder.Services.AddTransient<IAppService, AppService>();
builder.Services.AddTransient<OllamaApiService>();
builder.Services.AddTransient<FullTextSearchService>();
builder.Services.AddTransient<HybridSearch>();

var host = builder.Build();
var app = host.Services.GetRequiredService<IAppService>();
await app.RunAsync();
