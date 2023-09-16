

using System.Text;
using ChatGptDiscordBot;
using ChatGptDiscordBot.Implementations.Services;
using ChatGptDiscordBot.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using VkNet;
using VkNet.Model;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddHostedService<DiscordChatBotService>();
builder.Services.AddTransient<OpenAIChatService>();
builder.Services.AddDbContextPool<ChatbotDbContext>(optionsBuilder => optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("CONNECTION_STRING") ??"Host=tower;Database=chatbotdb_test;Username=postgres;Password=56840756716"));
builder.Services.AddSingleton<VkApi>((provider) =>
{
    var vkToken = Environment.GetEnvironmentVariable("VK_TOKEN") ?? 
                  "vk1.a.hkLwVrJoAN8maJnKt5fEiw4zSme6yCEHHu5BO6jgsHtCkelMEtrgniI2KyCGd9UAONnaenJWOcdMfxBiUEwuaLqj596a-fzpVTseKer9rPajku4gYMF3ufcGNEuSJaVYkAlGZPrrQ7Kh0TtNwp1DaC7GhTjHaofyM5bXv0oKgIwVihhmN1IVFqb4FucjFPjMq_RyBFhXyuUE6cWGrfc8yg";
    var vkapi = new VkApi();
    vkapi.Authorize(new ApiAuthParams
    {
        AccessToken = vkToken
    });
    return vkapi;
});
builder.Services.AddSingleton<VkChatBotService>();
var app = builder.Build();

app.MapPost("/", async (VkChatBotService service, HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
    var requestBody = await reader.ReadToEndAsync();
    Console.WriteLine(requestBody);
    var request = JsonConvert.DeserializeObject<VkIncomingRequest>(requestBody);
    service.HandleAsync(request);
    return "OK";
});

app.MapGet("/", (ChatbotDbContext ctx) =>
{
    Console.WriteLine(ctx.Users.Any());
    return "OK";
});

app.Urls.Add("http://0.0.0.0:80");

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