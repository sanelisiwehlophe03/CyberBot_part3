namespace CyberBot.Models
{
    /// <summary>One line in the activity log: when it happened and what the bot did.</summary>
    public record ActivityEntry(DateTime Timestamp, string Description)
    {
        public string Display => $"[{Timestamp:dd MMM HH:mm:ss}]  {Description}";
    }
}
