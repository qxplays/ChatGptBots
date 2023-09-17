using System.Collections.Concurrent;
using ChatGptDiscordBot.Model;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Models;
using VkNet;
using VkNet.Enums.StringEnums;
using VkNet.Model;
using Conversation = OpenAI_API.Chat.Conversation;
using Message = VkNet.Model.Message;
using User = ChatGptDiscordBot.Model.User;

namespace ChatGptDiscordBot.Implementations.Services;

public class VkChatBotService
{
    private readonly OpenAIChatService _service;
    private readonly ILogger<VKChatBot> _logger;
    private readonly VkApi _vkApi;
    private readonly IServiceScopeFactory _factory;
    private static ConcurrentDictionary<string, (Conversation, DateTime)> conversations = new();
    private static ConcurrentDictionary<string, (Conversation, DateTime)> premiumConversations = new();
    public VkChatBotService(OpenAIChatService service, ILogger<VKChatBot> logger, VkApi vkApi, IServiceScopeFactory factory)
    {
        _service = service;
        _logger = logger;
        _vkApi = vkApi;
        _factory = factory;
    }

    User GetUser(ChatbotDbContext _dbContext, string? userIdentifier, string? name)
    {
        
        if (_dbContext.Users.SingleOrDefault(x => x.UserIdentifier == userIdentifier) is { } user)
        {
            _dbContext.Users.Entry(user).Reload();
            return user;
        }
        user = new User()
        {
            Id = Guid.NewGuid(),
            UserIdentifier = userIdentifier,
            PremiumEndDate = DateTime.UtcNow.AddDays(3),
            UserSource = "VK",
            GPT4_TOKENS = 5,
            GPT35_TOKENS = 100,
            Name = name
        };
        _dbContext.Add(user);
        _dbContext.SaveChanges();
        return user;
    }
    public async void HandleAsync(VkIncomingRequest message)
    {
        try
        {
            var dbContext = _factory.CreateScope().ServiceProvider.GetService<ChatbotDbContext>();
            var model = OpenAI_API.Models.Model.ChatGPTTurbo;
            var msgObject = message.Object?.Object;
            if (msgObject == null || msgObject.FromId == 222570306)
                return;
            if (msgObject.Text.StartsWith("/me"))
            {
                HandleMeCommand(msgObject, dbContext);
                return;
            }
            if (msgObject.Text.StartsWith("/help") || msgObject.Text.StartsWith("/?"))
            {
                HandleHelpCommand(msgObject);
                return;
            }
            if (!msgObject.Text.StartsWith("/gpt"))
                return;
            string key;
            var vkUser = _vkApi.Users.Get(new[] { (long)msgObject.FromId })?.FirstOrDefault();
            var currentUser = GetUser( dbContext, msgObject.FromId.ToString(), $"{vkUser.FirstName} {vkUser.Nickname} {vkUser.LastName}");
            (Conversation conversation, DateTime lastUsage) conversation;
            switch (msgObject.Text.Substring(0, 5).ToLower())
            {
                case "/gpt4":
                {
                    //if (!currentUser.Premium || currentUser.PremiumEndDate <= DateTime.UtcNow)
                    //{
                    //    
                    //    await _vkApi.Messages.SendAsync(new MessagesSendParams()
                    //    {
                    //        Message =
                    //            "Кажется премиум куда-то испарился. Доступен только ChatGPT версии 3.5 с ограничением 3 зпроса в минуту (/gpt)",
                    //        RandomId = new Random().Next(100, 100000000),
                    //        ForwardMessages = msgObject.Id == null
                    //            ? null
                    //            : new List<long>(new[] { (long)msgObject.Id }.AsEnumerable()),
                    //        DontParseLinks = true, PeerId = msgObject.PeerId
                    //    });
                    //    return;
                    //}

                    //if (!currentUser.ModelPermissions.HasFlag(ModelPermissions.GPT4))
                    //{
                    //    await _vkApi.Messages.SendAsync(new MessagesSendParams()
                    //    {
                    //        Message =
                    //            "Недостаточно прав для использования GPT4. Нужна подписка получше.",
                    //        RandomId = new Random().Next(100, 100000000),
                    //        ForwardMessages = msgObject.Id == null
                    //            ? null
                    //            : new List<long>(new[] { (long)msgObject.Id }.AsEnumerable()),
                    //        DontParseLinks = true, PeerId = msgObject.PeerId
                    //    });
                    //    return;
                    //}
                    if (currentUser.GPT4_TOKENS < 1)
                    {
                        await _vkApi.Messages.SendAsync(new MessagesSendParams()
                        {
                            Message =
                                "Доступне запросы к GPT4 закончились.",
                            RandomId = new Random().Next(100, 100000000),
                            ForwardMessages = msgObject.Id == null
                                ? null
                                : new List<long>(new[] { (long)msgObject.Id }.AsEnumerable()),
                            DontParseLinks = true, PeerId = msgObject.PeerId
                        });
                        return;
                    }

                    model = OpenAI_API.Models.Model.GPT4;
                    key = msgObject.FromId + model;
                    if (!premiumConversations.TryGetValue(key, out conversation))
                    {

                        conversation = (
                            _service.CreatePremiumConversation(model),
                            DateTime.MinValue);

                        premiumConversations.TryAdd(msgObject.FromId + model, conversation);
                    }

                    break;
                }
                default:
                {
                    key = msgObject.FromId.ToString();
                    if (currentUser.GPT35_TOKENS < 1)
                    {
                        if (!premiumConversations.TryGetValue(key, out conversation))
                        {

                            conversation = (_service.CreatePremiumConversation(model),
                                DateTime.MinValue);
                            premiumConversations.TryAdd(msgObject.FromId.ToString(), conversation);
                        }
                    }
                    
                    else
                    {
                        if (!conversations.TryGetValue(key, out conversation))
                        {
                            conversation = (
                                _service.CreateConversation(model),
                                DateTime.MinValue);
                            conversations.TryAdd(msgObject.FromId.ToString(), conversation);
                        }
                    }

                    break;
                }
            }

            if (DateTime.UtcNow - conversation.lastUsage >=
                TimeSpan.FromSeconds(60 / int.Parse(Environment.GetEnvironmentVariable("RPM_MAX") ?? "3")) || (currentUser.GPT4_TOKENS+currentUser.GPT35_TOKENS)>0)
            {
                conversation.lastUsage = DateTime.UtcNow;
                conversations.AddOrUpdate(key, conversation, (k, old) => conversation);
            }
            else
            {
                await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = "Слишком много запросов. Пододждите " + (TimeSpan.FromSeconds(60 / int.Parse(Environment.GetEnvironmentVariable("RPM_MAX") ?? "3")) - (DateTime.UtcNow - conversation.lastUsage)).Seconds + "с. Или купите пакет запросов без лимитов", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
                return;
            }
            var content = msgObject.Text.Replace("/gpt4", "").Replace("/gpt", "");
            conversation.conversation.AppendUserInput(content);
            var rsString = string.Empty;
            var end = false;
            bool sendDone = false;
            var replyMsg = await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = $"Генерирую ответ для {vkUser?.FirstName} {vkUser?.LastName}... {(currentUser.GPT35_TOKENS>1?"":"Бесплатная версия (3 RPM)")}", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});

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

            try
            {
                await _vkApi.Messages.DeleteAsync(new List<ulong>(){(ulong)replyMsg}, deleteForAll: true);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            replyMsg = await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = rsString, RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});

            var entry = dbContext.Users.Entry(currentUser);
            if (conversation.conversation.Model.ModelID == OpenAI_API.Models.Model.GPT4.ModelID)
                entry.Entity.GPT4_TOKENS -= 1;
            if (model.ModelID == OpenAI_API.Models.Model.ChatGPTTurbo.ModelID && currentUser.GPT35_TOKENS > 0)
                entry.Entity.GPT35_TOKENS -= 1;
            
            entry.State = EntityState.Modified;
            await dbContext.SaveChangesAsync();

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

    private async void HandleHelpCommand(VkNet.Model.Message msgObject)
    {
        var replyMsg = await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = $"Доступные команды: \r\n /help \r\n /gpt \r\n /gpt4 \r\n /me", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});

    }

    private async void HandleMeCommand(VkNet.Model.Message msgObject, ChatbotDbContext dbcontext)
    {
        var vkUser = _vkApi.Users.Get(new[] { (long)msgObject.FromId })?.FirstOrDefault();
        var currentUser = GetUser ( dbcontext, msgObject.FromId.ToString(), $"{vkUser.FirstName} {vkUser.Nickname} {vkUser.LastName}");
        var replyMsg = await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = $"Осталось запросов к ChatGPT в рамках оплаченного пакета для {currentUser.Name}\r\n GPT3.5: {currentUser.GPT35_TOKENS}\r\n GPT4: {currentUser.GPT4_TOKENS}", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});

    }
}