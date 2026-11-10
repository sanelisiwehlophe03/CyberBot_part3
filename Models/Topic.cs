namespace CyberBot.Models
{
    /// <summary>A specific question pattern inside a topic, e.g. "how do I avoid phishing?".</summary>
    public class SubQuestion
    {
        public SubQuestion(string[] triggers, string answer)
        {
            Triggers = triggers;
            Answer = answer;
        }

        public string[] Triggers { get; }
        public string Answer { get; }
    }

    /// <summary>
    /// One cybersecurity topic and all the content the bot can use for it.
    /// Lists of tips allow random, non-repeating responses.
    /// </summary>
    public class Topic
    {
        /// <summary>Short internal key (e.g. "password").</summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>Friendly name shown to the user (e.g. "password safety").</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Words/phrases that identify this topic in user input.</summary>
        public string[] Keywords { get; init; } = Array.Empty<string>();

        /// <summary>Several definitions - one is picked at random.</summary>
        public string[] Overview { get; init; } = Array.Empty<string>();

        /// <summary>A plain-language explanation used when the user is confused.</summary>
        public string Simple { get; init; } = string.Empty;

        /// <summary>Random tips ("give me a phishing tip").</summary>
        public List<string> Tips { get; init; } = new();

        /// <summary>Ordered deeper explanations used for "tell me more".</summary>
        public List<string> MoreInfo { get; init; } = new();

        /// <summary>Sentence fragment used to personalise advice for users interested in this topic.</summary>
        public string MemoryTip { get; init; } = string.Empty;

        /// <summary>Specific question patterns carried over from Part 1.</summary>
        public List<SubQuestion> SubQuestions { get; init; } = new();
    }
}
