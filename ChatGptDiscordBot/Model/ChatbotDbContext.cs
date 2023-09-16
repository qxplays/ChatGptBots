using Microsoft.EntityFrameworkCore;

namespace ChatGptDiscordBot.Model;

public class ChatbotDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public ChatbotDbContext(DbContextOptions options) : base(options)
    {
        Database.EnsureCreated();
    }
}