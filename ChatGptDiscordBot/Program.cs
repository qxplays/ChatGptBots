

using System.Text;
using ChatGptDiscordBot;
using ChatGptDiscordBot.Implementations.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using VkNet;
using VkNet.Model;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddHostedService<DiscordChatBotService>();
builder.Services.AddTransient<OpenAIChatService>();
builder.Services.AddSingleton<VkApi>((provider) =>
{
    var vkToken = Environment.GetEnvironmentVariable("VK_TOKEN");
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
    var request = JsonConvert.DeserializeObject<VkIncomingRequest>(requestBody);
    service.HandleAsync(request);
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