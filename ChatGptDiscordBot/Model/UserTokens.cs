namespace ChatGptDiscordBot.Model;

public class UserToken
{
    public Guid Id { get; set; }
    public int GPT4_TOKENS { get; set; }
    public int GPT35_TOKENS { get; set; }
    public Guid UserId { get; set; }
}