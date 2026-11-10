using CyberBot.Models;
using CyberBot.Utils;

namespace CyberBot.Services
{
    /// <summary>
    /// Simple keyword-based sentiment detection plus empathetic response templates.
    /// </summary>
    public class SentimentAnalyser
    {
        // Each sentiment has a list of trigger words (matched at the start of words).
        private static readonly (Sentiment Sentiment, string[] Keywords)[] Lexicon =
        {
            (Sentiment.Worried, new[]
            {
                "worried", "worry", "scared", "afraid", "anxious", "nervous", "concerned",
                "fear", "panic", "terrified", "paranoid", "unsafe", "hacked", "compromised"
            }),
            (Sentiment.Frustrated, new[]
            {
                "frustrated", "frustrating", "annoyed", "annoying", "angry", "fed up",
                "irritated", "sick of", "hate", "useless", "stupid"
            }),
            (Sentiment.Overwhelmed, new[]
            {
                "overwhelmed", "confused", "confusing", "too much", "complicated",
                "unsure", "not sure", "no idea", "feel lost", "dont understand", "dont get it"
            }),
            (Sentiment.Curious, new[]
            {
                "curious", "wondering", "wonder", "interested", "fascinated",
                "want to know", "want to learn"
            }),
            (Sentiment.Positive, new[]
            {
                "happy", "glad", "awesome", "amazing", "great", "excited", "love"
            })
        };

        // {name} is replaced with the user's name.
        private static readonly Dictionary<Sentiment, string[]> Responses = new()
        {
            [Sentiment.Worried] = new[]
            {
                "It's completely understandable to feel that way, {name}. Cyber threats can be very convincing, but a few good habits go a long way.",
                "I hear you, {name}. Feeling worried is a sensible reaction, and the good news is there is plenty you can do to protect yourself.",
                "Don't worry, {name}, you're already taking a great first step by asking. Let's make you safer together."
            },
            [Sentiment.Frustrated] = new[]
            {
                "I'm sorry this feels frustrating, {name}. Cybersecurity can be confusing, so let's take it one small step at a time.",
                "That's a fair reaction, {name}. Security advice can feel like a lot, but I'll keep it simple.",
                "Thanks for being honest, {name}. Let's slow down and tackle it together."
            },
            [Sentiment.Overwhelmed] = new[]
            {
                "No need to feel overwhelmed, {name}. You don't have to learn everything at once.",
                "It's okay to feel unsure, {name}. Everyone starts somewhere, and we'll go at your pace.",
                "Let's break this down into something manageable, {name}."
            },
            [Sentiment.Curious] = new[]
            {
                "Great curiosity, {name}! Asking questions is one of the best security habits.",
                "I love that you want to learn more, {name}!"
            },
            [Sentiment.Positive] = new[]
            {
                "Love the enthusiasm, {name}!",
                "Great to hear, {name}! A positive attitude makes learning security easier."
            }
        };

        /// <summary>Returns the sentiment with the most keyword hits, or Neutral if none match.</summary>
        public Sentiment Detect(string normalisedInput)
        {
            Sentiment best = Sentiment.Neutral;
            int bestScore = 0;

            foreach ((Sentiment sentiment, string[] keywords) in Lexicon)
            {
                int score = TextMatcher.CountMatches(normalisedInput, keywords);

                if (score > bestScore)
                {
                    best = sentiment;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>Picks a random empathetic response for the sentiment.</summary>
        public string GetResponse(Sentiment sentiment, string name, Random random)
        {
            if (!Responses.TryGetValue(sentiment, out string[]? options))
            {
                return string.Empty;
            }

            return options[random.Next(options.Length)].Replace("{name}", name);
        }

        /// <summary>Short label (with emoji) for display in the GUI.</summary>
        public static string Describe(Sentiment sentiment) => sentiment switch
        {
            Sentiment.Worried => "😟 Worried",
            Sentiment.Frustrated => "😤 Frustrated",
            Sentiment.Overwhelmed => "😵 Overwhelmed",
            Sentiment.Curious => "🤔 Curious",
            Sentiment.Positive => "😊 Positive",
            _ => "😐 Neutral"
        };
    }
}
