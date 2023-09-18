using Telegram.Bot;

namespace ChatGptDiscordBot.Implementations;

public class TelegramChatBot : IHostedService
{
    private readonly ITelegramBotClient _client;
    public TelegramChatBot(ITelegramBotClient client)
    {
        _client = client;
        // token, который вернул BotFather
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        
        await _client.SetWebhookAsync(Environment.GetEnvironmentVariable("TELEGRAM_HOOK_URL")??"https://qxplays.ru/tghook", cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.DeleteWebhookAsync(cancellationToken: cancellationToken);
        
    }
}