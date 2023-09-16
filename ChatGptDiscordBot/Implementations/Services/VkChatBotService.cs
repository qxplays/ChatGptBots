using System.Collections.Concurrent;
using ChatGptDiscordBot.Model;
using OpenAI_API.Models;
using VkNet;
using VkNet.Enums.StringEnums;
using VkNet.Model;
using Conversation = OpenAI_API.Chat.Conversation;
using User = ChatGptDiscordBot.Model.User;

namespace ChatGptDiscordBot.Implementations.Services;

public class VkChatBotService
{
    private readonly OpenAIChatService _service;
    private readonly ILogger<VKChatBot> _logger;
    private readonly VkApi _vkApi;
    private readonly ChatbotDbContext _dbContext;
    private ConcurrentDictionary<string, (Conversation, DateTime)> conversations = new();
    public VkChatBotService(OpenAIChatService service, ILogger<VKChatBot> logger, VkApi vkApi, ChatbotDbContext dbContext)
    {
        _service = service;
        _logger = logger;
        _vkApi = vkApi;
        _dbContext = dbContext;
    }

    User GetUser(string? userIdentifier)
    {
        if (_dbContext.Users.SingleOrDefault(x => x.UserIdentifier == userIdentifier) is { } user)
            return user;
        user = new User()
        {
            Id = Guid.NewGuid(),
            UserIdentifier = userIdentifier,
            UserSource = "VK",
        };
        _dbContext.Add(user);
        _dbContext.SaveChanges();
        return user;
    }
    public async void HandleAsync(VkIncomingRequest message)
    {
        try
        {
            var msgObject = message.Object?.Object;
            if (msgObject == null || msgObject.FromId == 222570306)
                return;
            if (!msgObject.Text.StartsWith("/gpt"))
                return;
            string key;
            var currentUser = GetUser(msgObject.FromId.ToString());
            (Conversation conversation, DateTime lastUsage) conversation;
                switch (msgObject.Text.Substring(0, 5).ToLower())
                {
                    case "/gpt4":
                    {
                        key = msgObject.FromId + OpenAI_API.Models.Model.GPT4;
                        if (!conversations.TryGetValue(key, out conversation))
                        {
                            if (!currentUser.Premium || currentUser.PremiumEndDate <= DateTime.UtcNow)
                            {
                                await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = "Кажется премиум куда-то испарился. Доступен только ChatGPT версии 3.5 с ограничением 3 зпроса в минуту (/gpt)", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
                                return;
                            }
                            conversation = (
                                    _service.CreatePremiumConversation(OpenAI_API.Models.Model.ChatGPTTurbo),
                                    DateTime.MinValue);

                            conversations.TryAdd(msgObject.FromId + OpenAI_API.Models.Model.GPT4, conversation);
                        }

                        break;
                    }
                    default:
                    {
                        key = msgObject.FromId.ToString();
                        if (!conversations.TryGetValue(key, out conversation))
                        {
                            if (!currentUser.Premium || currentUser.PremiumEndDate <= DateTime.UtcNow)
                                conversation = (_service.CreateConversation(OpenAI_API.Models.Model.ChatGPTTurbo),
                                    DateTime.MinValue);
                            else
                                conversation = (
                                    _service.CreatePremiumConversation(OpenAI_API.Models.Model.ChatGPTTurbo),
                                    DateTime.MinValue);
                            conversations.TryAdd(msgObject.FromId.ToString(), conversation);
                        }

                        break;
                    }
                }
            
            if (DateTime.UtcNow - conversation.lastUsage >=
                TimeSpan.FromSeconds(60 / int.Parse(Environment.GetEnvironmentVariable("RPM_MAX") ?? "3")))
            {
                conversation.lastUsage = DateTime.UtcNow;
                conversations.AddOrUpdate(key, conversation, (k, old) => conversation);
            }
            else
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = "Слишком много запросов. Пододждите " + (TimeSpan.FromSeconds(60 / int.Parse(Environment.GetEnvironmentVariable("RPM_MAX") ?? "3")) - (DateTime.UtcNow - conversation.lastUsage)).Seconds + "с.", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
                return;
            }
            var content = msgObject.Text.Replace("/gpt4", "").Replace("/gpt", "");
            conversation.conversation.AppendUserInput(content);
            var rsString = string.Empty;
            var end = false;
            bool sendDone = false;
            var replyMsg = await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = $"Генерирую ответ для {_vkApi.Users.Get(new []{(long)msgObject.FromId})?.FirstOrDefault()?.FirstName}...", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});

            try
            {
                await ResponseToString(conversation.conversation.StreamResponseEnumerableFromChatbotAsync(), (s) => rsString += s, finished => end = finished);
            }
            catch (HttpRequestException e)
            {
                if (e.ToString().Contains("please check your plan and billing details"))
                {
                    await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = "Лимит сообщений исчерпан. Ищу рабочий токен. К сожалению история запросов будет очищена.", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});

                    if(!await _service.FindWorkingToken())
                        await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = "Рабочих токенов не нашлось... Скоро починим..", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
                    else
                    {
                        await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = "Токен найден, повторите запрос!", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
                    
                    };
                }
                Console.WriteLine(e);
                return;
            }

            await _vkApi.Messages.DeleteAsync(new List<ulong>(){(ulong)replyMsg}, deleteForAll: true);
            replyMsg = await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = rsString, RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});

            //await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = rsString, RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
        
            //while (true)
            //{
            //    var messages = _vkApi.Messages.GetConversations(new GetConversationsParams
            //    {
            //        Filter = GetConversationFilter.Unanswered,
            //        Count = 20,
            //        Extended = true
            //    });
//
            //    foreach (var message in messages.Items)
            //    {
            //        if (message.LastMessage.FromId != groupId) // чтобы не отвечать на собственные сообщения
            //        {
            //            _vkApi.Messages.Send(new MessagesSendParams
            //            {
            //                RandomId = new DateTime().Millisecond,
            //                PeerId = message.LastMessage.PeerId.Value,
            //                Message = "Привет! Это автоматический ответ."
            //            });
            //        }
            //    }
//
            //    System.Threading.Thread.Sleep(5000); // проверка каждые 5 секунд
            //}
            async Task ResponseToString(IAsyncEnumerable<string> chatResult, Action<string> updator, Action<bool> finisher)
            {
                await foreach (var result in chatResult) updator(result);
                finisher(true);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}