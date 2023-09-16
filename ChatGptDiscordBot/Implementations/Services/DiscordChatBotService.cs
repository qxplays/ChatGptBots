using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using OpenAI_API.Chat;
using OpenAI_API.Models;

namespace ChatGptDiscordBot.Implementations.Services;

public class DiscordChatBotService : IHostedService
{
    private readonly OpenAIChatService _chatService;
    private readonly ILogger<DiscordChatBotService> _logger;
    string? DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
    public DiscordChatBotService(OpenAIChatService chatService, ILogger<DiscordChatBotService> logger)
    {
        _chatService = chatService;
        _logger = logger;
        _client = new DiscordSocketClient(new DiscordSocketConfig() { GatewayIntents = GatewayIntents.All });
    }
    private DiscordSocketClient _client;
    private ConcurrentDictionary<string, Conversation> conversations = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {

        
        _client.Log += LogAsync;
        _client.MessageReceived += MessageReceivedAsync;

        await _client.LoginAsync(TokenType.Bot, DiscordToken);
        await _client.StartAsync();
    }

    async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.Id == _client.CurrentUser.Id)
            return;
        if (!message.Content.StartsWith("/gpt"))
            return;
        Conversation conversation;
        switch (message.Content.Substring(0, 5).ToLower())
        {
            case "/gpt4":
            {
                if (!conversations.TryGetValue(message.Author.Id + Model.GPT4, out conversation))
                {
                    conversation = _chatService.CreateConversation(Model.GPT4);
                    conversations.TryAdd(message.Author.Id + Model.GPT4, conversation);
                }

                break;
            }
            default:
            {
                if (!conversations.TryGetValue(message.Author.Id.ToString(), out conversation))
                {
                    conversation = _chatService.CreateConversation(Model.ChatGPTTurbo);
                    conversations.TryAdd(message.Author.Id.ToString(), conversation);
                }

                break;
            }
        }

        string? rsString = String.Empty;
        bool end = false;
        bool sendDone = false;
        var content = message.Content.Replace("/gpt4", "").Replace("/gpt", "");
        conversation.AppendUserInput(content);
        ResponseToString(conversation.StreamResponseEnumerableFromChatbotAsync(), (s) => rsString += s, finished => end = finished);

        var msg = await message.Channel.SendMessageAsync("Думаю...",
            messageReference: new MessageReference(message.Id));
        while (!end && !sendDone)
        {
            await Task.Delay(300);
            if (string.IsNullOrWhiteSpace(rsString))
                continue;
            if (end)
                sendDone = true;
            await msg.ModifyAsync(properties => properties.Content = rsString);

        }

        await Task.Delay(1000);
        await msg.ModifyAsync(properties => properties.Content = rsString);




    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    async void ResponseToString(IAsyncEnumerable<string> chatResult, Action<string> updator, Action<bool> finisher)
    {
        await foreach (var result in chatResult) updator(result);
        finisher(true);
    }
}