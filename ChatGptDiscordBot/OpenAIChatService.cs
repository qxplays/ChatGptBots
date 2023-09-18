using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace ChatGptDiscordBot;

public class OpenAIChatService
{
    private OpenAIAPI? _api;
    private  static OpenAIAPI? _apiGpt4;
    private static Queue<string> ApiTokens = new ();
    
    static OpenAIChatService()
    {
        var tokens = Environment.GetEnvironmentVariable("OPENAI_TOKENS") ?? "";
        foreach (var token in tokens.Split(";"))
        {
            if (!string.IsNullOrWhiteSpace(token))
                ApiTokens.Enqueue(token);
        }

        _apiGpt4 = new OpenAIAPI(new APIAuthentication(Environment.GetEnvironmentVariable("OPENAI_PAYED_TOKEN")??""));
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

        if (Environment.GetEnvironmentVariable("TOKEN_PER_USER")?.ToLower() == "true")
        {
            if (ApiTokens.TryDequeue(out var token))
            {
                var api = new OpenAIAPI(new APIAuthentication(token));
                return api.Chat.CreateConversation(new ChatRequest() { Model = model,  });
            }
        }

        return _api.Chat.CreateConversation(new ChatRequest() { Model = model, MaxTokens =  int.TryParse(Environment.GetEnvironmentVariable("MAX_TOKENS"), out var i)?i:2000});
    }
    
    public Conversation CreatePremiumConversation(string model)
    {
        Console.WriteLine("Premium chatgpt requested");
            return _apiGpt4.Chat.CreateConversation(new ChatRequest() { Model = model });
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
            var conversation = CreateConversation(OpenAI_API.Models.Model.ChatGPTTurbo);
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