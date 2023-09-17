using OpenAI_API.Chat;

namespace ChatGptDiscordBot.Implementations;

public static class Extensions
{
    public static bool HasFlag<TEnum>(this Enum enumeration, TEnum flag) where TEnum : Enum
    {
        return (Convert.ToInt32(enumeration) & Convert.ToInt32(flag)) == 1;
    }
    
}