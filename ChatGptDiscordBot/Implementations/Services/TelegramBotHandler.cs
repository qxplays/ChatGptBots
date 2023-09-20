using System.Diagnostics;
using ChatGptDiscordBot.Model;
using Newtonsoft.Json;
using OpenAI_API.Images;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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
        try
        {
            if(message.From.IsBot)
                return;
        
            OpenAI_API.Models.Model model;
        
            var command = message.EntityValues?.First();
            if(string.IsNullOrWhiteSpace(command) && message.Chat.Type != ChatType.Private)
                return;
            message.Text = message.Text?.Replace(command??"/gpt", String.Empty);
        

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
                    $"Осталось запросов:\r\n GPT4: {data.gpt4} \r\nGPT3.5: {data.gpt35}", replyToMessageId: message.MessageId);
                return;
            }
            else if (command.StartsWith("/imagine"))
            {
                try
                {
                    var sentMessage = await _client.SendPhotoAsync(message.Chat.Id, InputFile.FromUri("https://cs6.pikabu.ru/avatars/1884/x1884116-138679135.png"),  caption: "Генерирую ответ...",
                        replyToMessageId: message.MessageId);
                    var data = await _service.GenerateImage(message.From.Id.ToString(), message.From.Username, new ImageGenerationRequest(message.Text, 1, ImageSize._1024){ResponseFormat = ImageResponseFormat.B64_json});
                    using var stream = new MemoryStream(Convert.FromBase64String(data?.Data.First().Base64Data));
                    await _client.EditMessageMediaAsync(message.Chat.Id, sentMessage.MessageId,
                        new InputMediaPhoto(InputFile.FromStream(stream, data.CreatedUnixTime.ToString()+".png")));
                    //await _client.SendPhotoAsync(message.Chat.Id, , replyToMessageId: message.MessageId);
                    return;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine(e);
                    var jsonRs = JsonConvert.DeserializeObject<RequestError>(e.Message);
                    var sentMessage = await _client.SendTextMessageAsync(message.Chat.Id, "Ошибка при генерации ответа: "+jsonRs?.Error?.Message,
                        replyToMessageId: message.MessageId);
                    return;
                }
            }
            else if (message.Chat.Type == ChatType.Private)
            {
                model = OpenAI_API.Models.Model.ChatGPTTurbo;
            }
            else
            {
                return;
            }

            if (command.Contains("/gpt") || message.Chat.Type == ChatType.Private)
            {
                if (message.Text.Length < 2)
                {
                    await _client.SendTextMessageAsync(message.Chat.Id, "Использование команды - /gpt текст_запроса. Без текста не рокатит. Извините",
                        replyToMessageId: message.MessageId);
                    return;
                }

                var sentMessage = await _client.SendTextMessageAsync(message.Chat.Id, "Генерирую ответ...",
                    replyToMessageId: message.MessageId);
                await _service.AskGPT(new AskGPTRequest(message.From.Id.ToString(), message.From.Username, message.Text,
                    model,
                    async (answer) =>
                    {
                        try
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
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    },
                    async tuple =>
                    {
                        try
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
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }

                    }));
            }
        }
        catch (NotImplementedException e)
        {
            Console.WriteLine(e);
            await _client.SendTextMessageAsync(message.Chat.Id, "Эта команда еще не реализована",
                replyToMessageId: message.MessageId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await _client.SendTextMessageAsync(message.Chat.Id, e.Message,
                replyToMessageId: message.MessageId);
        }
    }
}