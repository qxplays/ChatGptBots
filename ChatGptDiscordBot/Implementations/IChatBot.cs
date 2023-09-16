using Discord;

namespace ChatGptDiscordBot.Implementations;

public interface IChatBot : IDisposable
{
    IChatBot Init();
}