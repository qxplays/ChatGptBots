using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ChatGptDiscordBot.GPTContracts;

public class ChatGptApiRequest
{
    public ChatGptApiRequest(string model, params string[] messages)
    {
        Model = model;
        Messages = new GptMessage[messages.Length];
        for (var i = 0; i < messages.Length; i++)
        {
            Messages[i] = new GptMessage("user", messages[i]);
        }
    }
    [JsonProperty("model")]
    public string Model { get; set; }
    [JsonProperty("messages")]
    public GptMessage[] Messages { get; set; }
}

public class GptMessage
{
    public GptMessage(string role, string message)
    {
        Role = role;
        Content = message;
    }
    [JsonProperty("role")]
    public string Role { get; set; }
    [JsonProperty("content")]
    public string Content { get; set; }
}