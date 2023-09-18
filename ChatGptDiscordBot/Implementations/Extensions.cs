using OpenAI_API.Chat;

namespace ChatGptDiscordBot.Implementations;

public static class Extensions
{
    public static bool HasFlag<TEnum>(this Enum enumeration, TEnum flag) where TEnum : Enum
    {
        return (Convert.ToInt32(enumeration) & Convert.ToInt32(flag)) == 1;
    }

    public static bool CanUsePremiumModel(this ChatGptDiscordBot.Model.User user, OpenAI_API.Models.Model model)
    {
        if (model.ModelID == OpenAI_API.Models.Model.GPT4.ModelID)
        {
            return user.GPT4_TOKENS > 0;
        }
        if (model.ModelID == OpenAI_API.Models.Model.ChatGPTTurbo.ModelID)
        {
            return user.GPT35_TOKENS > 0;
        }

        return false;
    }
}