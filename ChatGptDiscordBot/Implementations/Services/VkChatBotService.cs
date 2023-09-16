using System.Collections.Concurrent;
using OpenAI_API.Models;
using VkNet;
using VkNet.Enums.StringEnums;
using VkNet.Model;
using Conversation = OpenAI_API.Chat.Conversation;

namespace ChatGptDiscordBot.Implementations.Services;

public class VkChatBotService
{
    private readonly OpenAIChatService _service;
    private readonly ILogger<VKChatBot> _logger;
    private readonly VkApi _vkApi;
    private static DateTime lastUsage;
    private ConcurrentDictionary<string, Conversation> conversations = new();
    public VkChatBotService(OpenAIChatService service, ILogger<VKChatBot> logger, VkApi vkApi)
    {
        _service = service;
        _logger = logger;
        _vkApi = vkApi;
    }
    public async void HandleAsync(VkIncomingRequest message)
    {
        var msgObject = message.Object?.Object;
        if (msgObject == null || msgObject.FromId == 222570306)
            return;
        if (!msgObject.Text.StartsWith("/gpt"))
            return;
        if (DateTime.UtcNow - lastUsage >=
            TimeSpan.FromSeconds(60 / int.Parse(Environment.GetEnvironmentVariable("RPM_MAX") ?? "3")))
            lastUsage = DateTime.UtcNow;
        else
        {
            await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = "Слишком много запросов. Пододждите " + (TimeSpan.FromSeconds(60 / int.Parse(Environment.GetEnvironmentVariable("RPM_MAX") ?? "3")) - (DateTime.UtcNow - lastUsage)).Seconds + "с.", RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
            return;
        }
        Conversation conversation;
        switch (msgObject.Text.Substring(0, 5).ToLower())
        {
            case "/gpt4":
            {
                if (!conversations.TryGetValue(msgObject.FromId + Model.GPT4, out conversation))
                {
                    conversation = _service.CreateConversation(Model.GPT4);
                    conversations.TryAdd(msgObject.FromId + Model.GPT4, conversation);
                }

                break;
            }
            default:
            {
                if (!conversations.TryGetValue(msgObject.FromId.ToString(), out conversation))
                {
                    conversation = _service.CreateConversation(Model.ChatGPTTurbo);
                    conversations.TryAdd(msgObject.FromId.ToString(), conversation);
                }

                break;
            }
        }
        var content = msgObject.Text.Replace("/gpt4", "").Replace("/gpt", "");
        conversation.AppendUserInput(content);
        var rsString = string.Empty;
        var end = false;
        try
        {
            await ResponseToString(conversation.StreamResponseEnumerableFromChatbotAsync(), (s) => rsString += s, finished => end = finished);
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
        await _vkApi.Messages.SendAsync(new MessagesSendParams() { Message = rsString, RandomId = new Random().Next(100,100000000), ForwardMessages = msgObject.Id==null?null:new List<long>(new []{(long)msgObject.Id}.AsEnumerable()), DontParseLinks = true, PeerId = msgObject.PeerId});
        
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
}