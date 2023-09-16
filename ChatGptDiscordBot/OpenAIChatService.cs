using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace ChatGptDiscordBot;

public class OpenAIChatService
{
    private OpenAIAPI? _api;
    private static Queue<string> ApiTokens = new ();
    static OpenAIChatService()
    {
        var tokens = Environment.GetEnvironmentVariable("OPENAI_TOKENS");
        foreach (var token in tokens.Split(";"))
        {
            if (!string.IsNullOrWhiteSpace(token))
                ApiTokens.Enqueue(token);
        }
    }
    public OpenAIChatService()
    {
        Console.WriteLine("getting brand new chat service instance");
        if (ApiTokens.TryDequeue(out var token))
        {
            _api = new OpenAIAPI(new APIAuthentication(token));
            return;
        }
        Console.WriteLine("no free tokens left");
        
    }

    public Conversation CreateConversation(string model)
    {
        return _api.Chat.CreateConversation(new ChatRequest(){Model = model});
    }
}