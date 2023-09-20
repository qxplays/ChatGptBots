using Microsoft.EntityFrameworkCore;

namespace ChatGptDiscordBot.Model;

public class ChatbotDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserToken> UserTokens { get; set; }
    public ChatbotDbContext(DbContextOptions options) : base(options)
    {
        Database.EnsureCreated();
        Database.Migrate();
    }
    
    
}