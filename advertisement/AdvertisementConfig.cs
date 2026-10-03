using System.Text.Json.Serialization;

namespace MatchZy.Advertisement;

public class AdvertisementConfig
{
    public List<Advertisement> Ads { get; set; } = new();
}

public class Advertisement
{
    public float Interval { get; set; }

    public List<Dictionary<string, string>> Messages { get; set; } = new();

    [JsonIgnore]
    private int CurrentMessageIndex { get; set; }

    [JsonIgnore]
    public Dictionary<string, string> NextMessages
    {
        get
        {
            if (Messages.Count == 0)
                return new Dictionary<string, string>();

            var message = Messages[
                CurrentMessageIndex % Messages.Count
            ];

            CurrentMessageIndex++;

            return message;
        }
    }
}