using System.Runtime.Serialization;

namespace ChatGptDiscordBot.Model;

[DataContract]
public class User
{
    [DataMember]public Guid Id { get; set; }
    [DataMember]public bool Premium { get; set; }
    [DataMember]public DateTime PremiumEndDate { get; set; }
    [DataMember]public string? UserSource { get; set; }
    [DataMember]public string? UserIdentifier { get; set; }
    [DataMember]public int GPT35_TOKENS { get; set; }
    [DataMember]public int GPT4_TOKENS { get; set; }
    [DataMember]public string? Name { get; set; }

    public bool IsPremium()
    {
        return GPT4_TOKENS+GPT35_TOKENS>0;
    }
}

[Flags]
public enum ModelPermissions
{
    FREE = 0,
    GPT3_5 = 1,
    GPT4 = 2,
}