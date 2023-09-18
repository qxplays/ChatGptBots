using System.Collections.Concurrent;
using Microsoft.Win32;
using OpenAI_API.Chat;
using OpenQA.Selenium.DevTools.V85.CSS;
using VkNet.Model;
using Conversation = OpenAI_API.Chat.Conversation;
using User = ChatGptDiscordBot.Model.User;

namespace ChatGptDiscordBot.Implementations.Services;

public interface IOpenAIChatService
{
    Task AskGPT(AskGPTRequest request);
    void Reset(string fromId);
    void SetCreativity(string fromId);
    (int gpt4, int gpt35) HandleMeCommand(string s, string toString);
}

public class OpenAIChatServiceImpl : IOpenAIChatService
{
    private readonly IUserService _userService;
    private readonly OpenAIChatService _service;

    private static readonly ConcurrentDictionary<string, (Conversation conversation, DateTime lastUsage)> Conversations = new();
    private static readonly ConcurrentDictionary<string, (Conversation conversation, DateTime lastUsage)> PremiumConversations = new();
    private static readonly ConcurrentDictionary<string, bool> Generating = new();

    public OpenAIChatServiceImpl(IUserService userService, OpenAIChatService service)
    {
        _userService = userService;
        _service = service;
    }
    public async Task AskGPT(AskGPTRequest request)
    {
        if (Generating.ContainsKey(request.UserIdentifier))
        {
            await request.ErrorHandler((OpenAIErrors.INUSE, string.Empty));
            return;
        }

        try
        {
            Generating.TryAdd(request.UserIdentifier, true);
            var user = _userService.GetUser(request.UserIdentifier, request.UserName);
            if (request.Model.ModelID == OpenAI_API.Models.Model.GPT4.ModelID && !user.CanUsePremiumModel(request.Model))
            {
                await request.ErrorHandler((OpenAIErrors.TOKENS_EXCEEDED, string.Empty));
                return;
            }

            var conversation = GetConversation(request, user);
            conversation.AppendUserInput(request.Message);
            await request.AnswerHandler(conversation.StreamResponseEnumerableFromChatbotAsync());
            if (request.Model.ModelID == OpenAI_API.Models.Model.GPT4 && user.GPT4_TOKENS > 0)
                user.GPT4_TOKENS -= 1;
            if (request.Model.ModelID == OpenAI_API.Models.Model.ChatGPTTurbo && user.GPT35_TOKENS > 0)
                user.GPT35_TOKENS -= 1;
            await _userService.SaveUser(user);


        }
        
        catch (Exception e)
        {
            Console.WriteLine(e);
            await request.ErrorHandler((OpenAIErrors.GENERAL, e.Message));
        }
        finally
        {
            Generating.TryRemove(request.UserIdentifier, out _);
        }

    }

    public void Reset(string fromId)
    {
        throw new NotImplementedException();
    }

    public void SetCreativity(string fromId)
    {
        throw new NotImplementedException();
    }

    public (int gpt4, int gpt35) HandleMeCommand(string id, string nick)
    {
        var user = _userService.GetUser(id, nick);
        return (user.GPT4_TOKENS, user.GPT35_TOKENS);
    }

    private static TimeSpan RPM_DELAY =
        TimeSpan.FromSeconds(60 / int.Parse(Environment.GetEnvironmentVariable("RPM_MAX") ?? "3"));
    Conversation GetConversation(AskGPTRequest request, User user)
    {
        var conversation = GetOrGenerateConversation(request, user, out var isPremium);
        var timeDelta = RPM_DELAY - (DateTime.UtcNow - conversation.lastUsage);
        if (!isPremium && timeDelta.Seconds > 0)
        {
            request.ErrorHandler((OpenAIErrors.TIME_EXCEEDED, $"{RPM_DELAY-timeDelta}"));
            throw new InvalidOperationException("Limit Exceeded");
        }
        conversation.lastUsage = DateTime.UtcNow;
        if (isPremium)
            PremiumConversations.AddOrUpdate(request.UserIdentifier + request.Model.ModelID, conversation,
                (k, old) => conversation);
        else
            Conversations.AddOrUpdate(request.UserIdentifier + request.Model.ModelID, conversation,
                (k, old) => conversation);
        
        return conversation.conversation;
    }
    
    (Conversation conversation, DateTime lastUsage) GetOrGenerateConversation(AskGPTRequest request, User user, out bool isPremium)
    {
        isPremium = false;
        if (request.Model.ModelID == OpenAI_API.Models.Model.GPT4.ModelID)
        {
            isPremium = true;
            if(PremiumConversations.TryGetValue(request.UserIdentifier + request.Model.ModelID, out var conversation))
                return conversation;
            conversation = (
                _service.CreatePremiumConversation(request.Model),
                DateTime.MinValue);

            PremiumConversations.TryAdd(request.UserIdentifier + request.Model.ModelID, conversation);
            return conversation;
        }
        
        if (request.Model.ModelID == OpenAI_API.Models.Model.ChatGPTTurbo.ModelID)
        {
            if(user.CanUsePremiumModel(request.Model))
            {
                isPremium = true;
                if (PremiumConversations.TryGetValue(request.UserIdentifier + request.Model.ModelID,
                        out var premiumConversation))
                    return premiumConversation;
                
                premiumConversation = (
                    _service.CreatePremiumConversation(request.Model),
                    DateTime.MinValue);

                PremiumConversations.TryAdd(request.UserIdentifier + request.Model.ModelID, premiumConversation);
                return premiumConversation;
                
            }
            
            if (Conversations.TryGetValue(request.UserIdentifier + request.Model.ModelID, out var conversation))
                return conversation;
            
            conversation = (
                _service.CreateConversation(request.Model),
                DateTime.MinValue);
            Conversations.TryAdd(request.UserIdentifier + request.Model.ModelID, conversation);
            return conversation;

        }

        throw  new NotImplementedException("Unsupported model");
    }
}

public class AskGPTRequest
{
    public AskGPTRequest(string userIdentifier, string userName, string message, OpenAI_API.Models.Model model, Func<IAsyncEnumerable<string>, Task> answerHandler, Func<(OpenAIErrors, string), Task> errorHandler)
    {
        UserIdentifier = userIdentifier;
        UserName = userName;
        Message = message;
        Model = model;
        AnswerHandler = answerHandler;
        ErrorHandler = errorHandler;
    }
    public string UserIdentifier { get; }
    public string UserName { get; }
    public string Message { get; }
    public OpenAI_API.Models.Model Model { get; }
    public Func<IAsyncEnumerable<string>, Task> AnswerHandler{ get; }
    public Func<(OpenAIErrors, string), Task> ErrorHandler{ get; }
}

public enum OpenAIErrors
{
    NONE,
    TOKENS_EXCEEDED,
    INUSE,
    TIME_EXCEEDED,
    GENERAL

}