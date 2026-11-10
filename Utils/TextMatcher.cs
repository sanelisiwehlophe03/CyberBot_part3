using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CyberBot.Utils
{
    /// <summary>
    /// Text helpers for keyword recognition. Matching is word-boundary aware, so
    /// "scam" matches "scammers" but "data" would not match inside "database".
    /// </summary>
    public static class TextMatcher
    {
        private static readonly ConcurrentDictionary<string, Regex> Cache = new();
        private static readonly Regex NonWordRegex = new(@"[^\p{L}\p{N}\s]", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Lower-cases, strips apostrophes, replaces punctuation/hyphens with spaces and collapses spaces.</summary>
        public static string Normalise(string input)
        {
            string text = input.Trim().ToLowerInvariant()
                               .Replace("'", string.Empty)
                               .Replace("\u2019", string.Empty);

            text = NonWordRegex.Replace(text, " ");
            return WhitespaceRegex.Replace(text, " ").Trim();
        }

        /// <summary>True if the input contains the phrase at the start of a word.</summary>
        public static bool Contains(string input, string phrase)
        {
            Regex regex = Cache.GetOrAdd(
                phrase,
                p => new Regex($@"\b{Regex.Escape(p)}", RegexOptions.IgnoreCase));

            return regex.IsMatch(input);
        }

        /// <summary>True if the input contains at least one of the phrases.</summary>
        public static bool ContainsAny(string input, params string[] phrases)
        {
            foreach (string phrase in phrases)
            {
                if (Contains(input, phrase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Counts how many of the phrases appear in the input.</summary>
        public static int CountMatches(string input, IEnumerable<string> phrases)
        {
            int count = 0;

            foreach (string phrase in phrases)
            {
                if (Contains(input, phrase))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
