namespace CyberBot.Models
{
    /// <summary>
    /// Holds everything the chatbot "remembers" about the person it is talking to.
    /// Uses automatic properties (Part 1 learning outcome).
    /// </summary>
    public class User
    {
        public User(string name)
        {
            Name = name;
        }

        /// <summary>The user's name, used to personalise responses.</summary>
        public string Name { get; set; }

        /// <summary>Key of the topic the user said they are interested in (e.g. "privacy").</summary>
        public string? FavouriteTopic { get; set; }

        /// <summary>The most recently detected sentiment.</summary>
        public Sentiment LastSentiment { get; set; } = Sentiment.Neutral;

        /// <summary>Display names of every topic discussed so far (no duplicates).</summary>
        public List<string> TopicsDiscussed { get; } = new();
    }
}
