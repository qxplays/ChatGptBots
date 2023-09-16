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
}