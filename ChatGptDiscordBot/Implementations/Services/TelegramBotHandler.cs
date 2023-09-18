using System.Diagnostics;
using ChatGptDiscordBot.Model;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Message = Telegram.Bot.Types.Message;

namespace ChatGptDiscordBot.Implementations.Services;

public class TelegramBotHandler
{
    private readonly ITelegramBotClient _client;
    private readonly ChatbotDbContext _dbContext;
    private readonly IOpenAIChatService _service;
    private readonly IUserService _userService;

    public TelegramBotHandler(ITelegramBotClient client, ChatbotDbContext dbContext, IOpenAIChatService service, IUserService userService)
    {
        _client = client;
        _dbContext = dbContext;
        _service = service;
        _userService = userService;
    }


    public async void HandleAsync(Update request) =>
        await (request switch
        {
            { Message: { } message } => BotOnMessageReceived(message),
            _ => UnknownUpdateHandlerAsync(request)
        });
    

    private Task UnknownUpdateHandlerAsync(object update)
    {
        Console.WriteLine($"Skipped request {JsonConvert.SerializeObject(update)}");
        return Task.CompletedTask;
        
    }

    private async Task BotOnMessageReceived(Telegram.Bot.Types.Message message)
    {
        if(message.From.IsBot)
            return;
        

        if(!message.EntityValues?.Any()??false)
            return;
        
        OpenAI_API.Models.Model model;
        
        var command = message.EntityValues?.First();
        message.Text = message.Text?.Replace(command!, String.Empty);
        

        if (command!.StartsWith("/gpt4"))
        {
            model = OpenAI_API.Models.Model.GPT4;
        }
        else if (command.StartsWith("/gpt"))
        {
            model = OpenAI_API.Models.Model.ChatGPTTurbo;
        }
        else if (command.StartsWith("/reset"))
        {
            _service.Reset(message.From.Id.ToString());
            return;
        }
        else if (command.StartsWith("/creativity"))
        {
            _service.SetCreativity(message.From.Id.ToString());
            return;
        }
        else if (command.StartsWith("/me"))
        {
            var data = _service.HandleMeCommand(message.From.Id.ToString(), message.From.Username);
            await _client.SendTextMessageAsync(message.Chat.Id,
                $"Осталось запросов:\r\n GPT4: {data.gpt4} \r\nGPT3.5: {data.gpt35}");
            return;
        }
        else
        {
            return;
        }

        if (command.Contains("/gpt"))
        {
            if (message.Text.Length < 2)
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "Нужно б}ольше золота (текста)",
                    replyToMessageId: message.MessageId);
                return;
            }

            var sentMessage = await _client.SendTextMessageAsync(message.Chat.Id, "Генерирую ответ...",
                replyToMessageId: message.MessageId);
            await _service.AskGPT(new AskGPTRequest(message.From.Id.ToString(), message.From.Username, message.Text,
                model,
                async (answer) =>
                {
                    var rsString = string.Empty;
                    var timer = Stopwatch.StartNew();
                    await foreach (var ans in answer)
                    {
                        rsString += ans;
                        if (timer.ElapsedMilliseconds <= 500) 
                            continue;
                        timer.Restart();
                        await _client.EditMessageTextAsync(message.Chat.Id, sentMessage.MessageId, rsString);
                    }

                    await Task.Delay(300);
                    await _client.EditMessageTextAsync(message.Chat.Id, sentMessage.MessageId, rsString);
                },
                async tuple =>
                {
                    switch (tuple.Item1)
                    {
                        case OpenAIErrors.INUSE:
                            await _client.EditMessageTextAsync(message.Chat.Id, sentMessage.MessageId, "Нужно подождать завершения прошлой генерации.");
                            break;
                        case OpenAIErrors.GENERAL:
                            await _client.EditMessageTextAsync(message.Chat.Id, sentMessage.MessageId, "Внутренняя ошибка системы.");
                            break;
                        case OpenAIErrors.TIME_EXCEEDED:
                            await _client.EditMessageTextAsync(message.Chat.Id, sentMessage.MessageId, $"Слишком много запросов. Перед следующим необходимо подождать {tuple.Item2}с.");
                            break;
                        case OpenAIErrors.TOKENS_EXCEEDED:
                            await _client.EditMessageTextAsync(message.Chat.Id, sentMessage.MessageId,"Превышено количество доступных запросов к GPT4");
                            break;
                        default:
                            break;
                    }

                }));
        }
    }
}