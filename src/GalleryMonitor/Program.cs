using GalleryMonitor.Clients;
using GalleryMonitor.Configuration;
using GalleryMonitor.Notifications;
using GalleryMonitor.Persistence;
using GalleryMonitor.Services;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddOptions<GalleryOptions>().BindConfiguration(GalleryOptions.SectionName).Validate(static options => options.IsValid(), "Configure ao menos uma galeria; cada item precisa de Name único e Url absoluta.").ValidateOnStart();
builder.Services.AddOptions<DiscordOptions>().BindConfiguration(DiscordOptions.SectionName).PostConfigure(static options => options.WebhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL") ?? options.WebhookUrl);
builder.Services.AddHttpClient<IGalleryClient, PicTimeGalleryClient>(static client => client.DefaultRequestHeaders.UserAgent.ParseAdd("GalleryMonitor/1.0"));
builder.Services.AddHttpClient<INotificationService, DiscordNotificationService>();
builder.Services.AddSingleton<IStateRepository, JsonStateRepository>();
builder.Services.AddSingleton<IGalleryMonitorService, GalleryMonitorService>();
builder.Services.AddHostedService<GalleryWorker>();

await builder.Build().RunAsync();
