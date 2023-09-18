using ChatGptDiscordBot.Model;
using Microsoft.EntityFrameworkCore;

namespace ChatGptDiscordBot.Implementations.Services;

public interface IUserService
{
    User GetUser(string? userIdentifier, string? name);
    Task SaveUser(User user);
}

public class UserService : IUserService
{
    private readonly ChatbotDbContext _dbContext;

    public UserService(ChatbotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public User GetUser(string? userIdentifier, string? name)
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

    public async Task SaveUser(User user)
    {
        _dbContext.Entry(user).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public User? GetUser(Guid userId)
    {
        try
        {
            var user = _dbContext.Users.SingleOrDefault(x => x.Id == userId);
            _dbContext.Entry(user!).Reload();
            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
    

}