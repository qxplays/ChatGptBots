

using System.Security.Cryptography.X509Certificates;
using System.Text;
using ChatGptDiscordBot;
using ChatGptDiscordBot.Implementations;
using ChatGptDiscordBot.Implementations.Services;
using ChatGptDiscordBot.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using VkNet;
using VkNet.Model;
using Conversation = OpenAI_API.Chat.Conversation;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddHostedService<DiscordChatBotService>();
builder.Services.AddTransient<OpenAIChatService>();
builder.Services.AddTransient<IOpenAIChatService, OpenAIChatServiceImpl>();
builder.Services.AddDbContextPool<ChatbotDbContext>(optionsBuilder => optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("CONNECTION_STRING") ??"Host=192.168.1.207;Database=chatbotdb_root;Username=postgres;Password=56840756716"));
builder.Services.AddSingleton<VkApi>((provider) =>
{
    var vkToken = Environment.GetEnvironmentVariable("VK_TOKEN") ?? 
                  "";
    var vkapi = new VkApi();
    vkapi.Authorize(new ApiAuthParams
    {
        AccessToken = vkToken
    });
    return vkapi;
});
builder.Services.Configure<KestrelServerOptions>(options =>
{
    //options.ConfigureHttpsDefaults(adapterOptions =>
    //    adapterOptions.UseLettuceEncrypt(options.ApplicationServices));
    //options.ListenAnyIP(443, listenOptions => listenOptions.UseHttps());
    options.ListenAnyIP(80);
});
//builder.Services.AddLettuceEncrypt();
builder.Services.AddTransient<ITelegramBotClient>((ctx) => new TelegramBotClient(Environment.GetEnvironmentVariable("TELEGRAM_TOKEN") ??
    "6119724546:AAGWe7H8Z_Lx7VBX_y7z0vdq4tpymhxYDwA")
);
builder.Services.AddSingleton<VkChatBotService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<TelegramBotHandler>();
builder.Services.AddHostedService<TelegramChatBot>();

var app = builder.Build();

app.MapPost("/", async (VkChatBotService service, HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
    var requestBody = await reader.ReadToEndAsync();
    Console.WriteLine(requestBody);
    var request = JsonConvert.DeserializeObject<VkIncomingRequest>(requestBody);
    try
    {

        service.HandleAsync(request);
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
    return "OK";
});

app.MapGet("/.well-known/acme-challenge/q45TpmX8KE67N4H4Cp0iEtAyMyNZAPMUE14v4lz60ZA/", () => "q45TpmX8KE67N4H4Cp0iEtAyMyNZAPMUE14v4lz60ZA.uWBTespC7Uw84d0pOhrI7dJObM4ELKmP4CHoADg1C2I");

app.MapPost("/tghook/", async (TelegramBotHandler handler, HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
    var requestBody = await reader.ReadToEndAsync();
    Console.WriteLine(requestBody);
    var request = JsonConvert.DeserializeObject<Update>(requestBody);
    try
    {

        handler.HandleAsync(request);
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
    return "OK";
});

app.MapGet("/", (ChatbotDbContext ctx) =>
{
    Console.WriteLine(ctx.Users.Any());
    return "OK";
});

app.Run();

public class VkIncomingRequest
{
    [JsonProperty("group_id")]
    public long? GroupId { get; set; }
    [JsonProperty("type")]
    public string? Type { get; set; }
    [JsonProperty("event_id")]
    public string? EventId { get; set; }
    [JsonProperty("object")]
    public Message? Object { get; set; }
}

public class Message
{
    
    [JsonProperty("message")]
    public VkNet.Model.Message? Object { get; set; }
}