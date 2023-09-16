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
        FindWorkingToken().GetAwaiter().GetResult();
    }

    void GenerateApi()
    {
        if (ApiTokens.TryDequeue(out var token))
        {
            _api = new OpenAIAPI(new APIAuthentication(token));
        }
    }
    
    public Conversation CreateConversation(string model)
    {
        return _api.Chat.CreateConversation(new ChatRequest(){Model = model});
    }

    public async Task<bool> FindWorkingToken()
    {
        //while (ApiTokens.Any())
        //{
            GenerateApi();
                //if (await TestConnection())
            //{
            //    Console.WriteLine("Рабочий токен найден");
            //    return true;
            //}
        //}
        //Console.WriteLine("Токен не найден -(");
        return true;
    }

    async Task<bool> TestConnection()
    {
        try
        {
            var conversation = CreateConversation(Model.ChatGPTTurbo);
            conversation.AppendUserInput("Напиши одно слово");
            var response = await conversation.GetResponseFromChatbotAsync();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine("токен мертв, ищу новый.");
            return false;
        }
    }
}