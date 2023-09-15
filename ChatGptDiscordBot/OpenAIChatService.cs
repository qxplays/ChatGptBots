using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace ChatGptDiscordBot;

public class OpenAIChatService
{
    private static OpenAIAPI _api;

    static OpenAIChatService()
    {
        _api = new OpenAIAPI(new APIAuthentication(Environment.GetEnvironmentVariable("OPENAI_TOKEN")));
    }

    static public IAsyncEnumerable<ChatResult> GetGPT35Response(string request)
    {
        return _api.Chat.StreamChatEnumerableAsync(new List<ChatMessage>(new ChatMessage[]
            { new ChatMessage(ChatMessageRole.User, request) }));

    }

    public static Conversation CreateConversation(string model)
    {
        return _api.Chat.CreateConversation(new ChatRequest(){Model = model});
    }
}