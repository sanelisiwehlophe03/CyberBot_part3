using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CyberBot.Models;
using CyberBot.Utils;

namespace CyberBot.Services
{
    /// <summary>
    /// The chatbot "brain". It takes text in and returns text out, with no UI code,
    /// so the WPF window only has to display what it returns.
    ///
    /// Part 2 features:
    ///  - Keyword recognition (topics + sub-questions)
    ///  - Random, non-repeating tips
    ///  - Conversation flow (follow-ups continue the current topic)
    ///  - Memory and recall (name, favourite topic, topics discussed)
    ///  - Sentiment detection (worried / frustrated / overwhelmed / curious / positive)
    ///  - Error handling and default responses
    /// </summary>
    public partial class Chatbot
    {
        private const int MaxInputLength = 300;

        // Phrases used to recognise follow-up requests.
        private static readonly string[] AnotherTipPhrases = { "another", "one more", "more tips", "next tip" };

        private static readonly string[] MorePhrases =
        {
            "tell me more", "explain more", "more detail", "more info", "more about", "go on",
            "elaborate", "continue", "what else", "expand", "explain further", "explain it more", "explain that"
        };

        private static readonly string[] ConfusedPhrases =
        {
            "dont understand", "dont get it", "confused", "what do you mean", "unclear",
            "simpler", "simplify", "clarify", "not sure what you mean", "makes no sense", "feel lost"
        };

        private static readonly Regex NameRegex =
            new(@"^(?:my name is|call me|you can call me)\s+([a-z][a-z ]{0,29})$", RegexOptions.Compiled);

        private static readonly Regex InterestRegex =
            new(@"(?:interested in|favourite topic is|favorite topic is|love learning about|like learning about|want to learn about|care about)\s+(.+)$",
                RegexOptions.Compiled);

        private static readonly Regex NamePrefixRegex =
            new(@"^(?:my name is|call me|i am|i'm|im|its|it's)\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ValidNameRegex =
            new(@"^[\p{L}][\p{L}\s'\-]*$", RegexOptions.Compiled);

        /// <summary>
        /// A recogniser/handler pair. Both are delegates, so the list of intents
        /// below is checked in order and the first match decides the reply.
        /// </summary>
        private record Intent(Func<string, bool> Matches, Func<string, string> Respond);

        private readonly KnowledgeBase _knowledge = new();
        private readonly SentimentAnalyser _sentimentAnalyser = new();
        private readonly Random _random = new();
        private readonly List<Intent> _intents;

        // Tracks which tips / "more info" items have been used, to avoid repeats.
        private readonly Dictionary<string, HashSet<int>> _usedTips = new();
        private readonly Dictionary<string, int> _moreInfoIndex = new();

        private Topic? _currentTopic;
        private Sentiment _sentiment = Sentiment.Neutral;
        private int _replyCount;

        public Chatbot(TaskRepository repository, ActivityLog log)
        {
            _repo = repository;
            _log = log;

            // Order matters: the first intent whose Matches delegate returns true wins.
            _intents = new List<Intent>
            {
                new Intent(IsGoodbye, HandleGoodbye),
                new Intent(IsHelp, _ => GetHelpText()),
                new Intent(IsMemoryQuestion, _ => HandleMemoryRecall()),
                new Intent(IsNameStatement, HandleNameStatement),
                new Intent(IsInterestStatement, HandleInterestStatement),
                new Intent(IsGreeting, _ => Fill(PickRandom(_knowledge.Greetings))),
                new Intent(input => TextMatcher.ContainsAny(input, "how are you", "how are u", "how do you feel", "hows it going"),
                           _ => Fill(PickRandom(_knowledge.HowAreYou))),
                new Intent(input => TextMatcher.ContainsAny(input, "thank") && FindTopic(input) == null,
                           _ => Fill(PickRandom(_knowledge.ThanksReplies))),
                new Intent(input => TextMatcher.ContainsAny(input, "your name", "who are you", "what are you"),
                           _ => _knowledge.IdentityReply),
                new Intent(input => TextMatcher.ContainsAny(input, "your purpose", "what can you do", "what do you do"),
                           _ => _knowledge.PurposeReply),
                new Intent(IsNlpCommand, HandleNlpCommand),   // Part 3: tasks, reminders, quiz, activity log
                new Intent(IsFollowUp, HandleFollowUp),
                new Intent(input => FindTopic(input) != null, HandleTopic),
                new Intent(_ => _sentiment != Sentiment.Neutral, HandleSentimentOnly)
            };
        }

        /// <summary>The current user (null until a name has been registered).</summary>
        public User? User { get; private set; }

        /// <summary>True once the user has said goodbye.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>All topics - used by the GUI to build the topic buttons.</summary>
        public IReadOnlyList<Topic> Topics => _knowledge.Topics;

        // =========================================================
        // PUBLIC API
        // =========================================================

        /// <summary>
        /// Validates and stores the user's name. Returns false (with a helpful reply)
        /// if the name is empty or contains unsupported characters.
        /// </summary>
        public bool TryRegisterUser(string? rawName, out string reply)
        {
            string name = NamePrefixRegex.Replace((rawName ?? string.Empty).Trim(), string.Empty).Trim();

            if (name.Length == 0)
            {
                reply = "I didn't catch that. Please type your name so we can get started.";
                return false;
            }

            if (name.Length > 30)
            {
                reply = "That name is a bit long for me. Could you give me a shorter version (30 characters or less)?";
                return false;
            }

            if (!ValidNameRegex.IsMatch(name))
            {
                reply = "Names can only contain letters, spaces, hyphens and apostrophes. Please try again.";
                return false;
            }

            User = new User(ToTitleCase(name));
            reply = $"Nice to meet you, {User.Name}! How can I help you today?";
            return true;
        }

        /// <summary>Returns the bot's reply to a message. Never throws.</summary>
        public string GetReply(string? rawInput)
        {
            try
            {
                if (User == null)
                {
                    return "Please tell me your name first so we can get started.";
                }

                if (string.IsNullOrWhiteSpace(rawInput))
                {
                    return "I didn't receive anything. Please type a question, or say 'help' to see what I can do.";
                }

                if (rawInput.Length > MaxInputLength)
                {
                    return "That message is a bit long for me. Could you shorten it to a question or two?";
                }

                string input = TextMatcher.Normalise(rawInput);

                if (input.Length == 0)
                {
                    return "I couldn't find any words in that. Try a question like 'What is phishing?'";
                }

                _sentiment = _sentimentAnalyser.Detect(input);
                User.LastSentiment = _sentiment;

                // Part 3: remember whether the last reply was the activity log (for "show more")
                _previousWasLog = _lastWasLog;
                _lastWasLog = false;

                // Part 3: answer to "Would you like a reminder?" takes priority
                if (_pending != PendingAction.None)
                {
                    string? pendingReply = HandlePending(input);

                    if (pendingReply != null)
                    {
                        return pendingReply;
                    }
                }

                foreach (Intent intent in _intents)
                {
                    if (intent.Matches(input))
                    {
                        return intent.Respond(input);
                    }
                }

                return HandleDefault();
            }
            catch (Exception)
            {
                return "Sorry, something went wrong on my side. Please try asking again.";
            }
        }

        /// <summary>Help text listing topics, example questions and commands.</summary>
        public string GetHelpText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Here's what I can help with:");
            sb.AppendLine();
            sb.AppendLine($"Topics: {TopicListText()}.");
            sb.AppendLine();
            sb.AppendLine("Try asking:");
            sb.AppendLine("• What is phishing?");
            sb.AppendLine("• Give me a phishing tip");
            sb.AppendLine("• How do I make a strong password?");
            sb.AppendLine("• I'm worried about online scams");
            sb.AppendLine();
            sb.AppendLine("You can also say:");
            sb.AppendLine("• 'Another tip' or 'Tell me more' to continue a topic");
            sb.AppendLine("• 'I'm interested in privacy' and I'll remember it");
            sb.AppendLine("• 'What do you remember about me?'");
            sb.AppendLine();
            sb.AppendLine("Tasks, quiz and log:");
            sb.AppendLine("• 'Add task - Review privacy settings'");
            sb.AppendLine("• 'Remind me to update my password tomorrow'");
            sb.AppendLine("• 'Show my tasks', 'complete task 1', 'delete task 1'");
            sb.AppendLine("• 'Start the quiz'");
            sb.AppendLine("• 'Show activity log' or 'What have you done for me?'");
            sb.AppendLine();
            sb.Append("• 'bye' to finish");
            return sb.ToString();
        }

        /// <summary>Display name of the user's favourite topic, if they have told us one.</summary>
        public string? GetFavouriteTopicName()
        {
            return User?.FavouriteTopic == null ? null : _knowledge.GetTopic(User.FavouriteTopic)?.DisplayName;
        }

        // =========================================================
        // INTENT MATCHERS (used as delegates)
        // =========================================================

        private static bool IsGoodbye(string input) =>
            input is "exit" or "quit" or "bye" or "goodbye" or "good bye";

        private static bool IsHelp(string input) =>
            input == "help" || TextMatcher.ContainsAny(input, "what can i ask", "what topics", "show topics", "list topics", "commands");

        private static bool IsMemoryQuestion(string input) =>
            TextMatcher.ContainsAny(input, "what do you remember", "do you remember", "what do you know about me",
                                    "who am i", "what is my name", "whats my name");

        private static bool IsNameStatement(string input) => NameRegex.IsMatch(input);

        private bool IsInterestStatement(string input)
        {
            Match match = InterestRegex.Match(input);
            return match.Success && FindTopic(match.Groups[1].Value) != null;
        }

        private bool IsGreeting(string input)
        {
            if (input is "hello" or "hi" or "hey" or "good morning" or "good afternoon" or "good evening")
            {
                return true;
            }

            bool startsWithGreeting = input.StartsWith("hello ") || input.StartsWith("hi ") || input.StartsWith("hey ");
            return startsWithGreeting && FindTopic(input) == null;
        }

        private static bool IsFollowUp(string input) =>
            input is "more" or "next"
            || TextMatcher.ContainsAny(input, AnotherTipPhrases)
            || TextMatcher.ContainsAny(input, MorePhrases)
            || TextMatcher.ContainsAny(input, ConfusedPhrases);

        // =========================================================
        // INTENT HANDLERS
        // =========================================================

        private string HandleGoodbye(string input)
        {
            IsFinished = true;
            return $"Goodbye, {User?.Name}! Remember: think before you click.";
        }

        private string HandleNameStatement(string input)
        {
            string name = ToTitleCase(NameRegex.Match(input).Groups[1].Value.Trim());
            User!.Name = name;
            return $"Got it! I'll call you {name} from now on.";
        }

        private string HandleInterestStatement(string input)
        {
            Topic topic = FindTopic(InterestRegex.Match(input).Groups[1].Value)!;

            User!.FavouriteTopic = topic.Key;
            SetCurrentTopic(topic);

            return $"Great! I'll remember that you're interested in {topic.DisplayName}, {User.Name}. " +
                   $"It's a crucial part of staying safe online.\n\nHere's a tip to start: {NextTip(topic)}";
        }

        private string HandleMemoryRecall()
        {
            var sb = new StringBuilder();
            sb.Append($"Here's what I remember, {User!.Name}:");
            sb.Append($"\n• Your name is {User.Name}.");

            string? favourite = GetFavouriteTopicName();
            sb.Append(favourite != null
                ? $"\n• You're interested in {favourite}."
                : "\n• You haven't told me a favourite topic yet. Try saying 'I'm interested in privacy'.");

            if (User.TopicsDiscussed.Count > 0)
            {
                sb.Append($"\n• We've talked about: {string.Join(", ", User.TopicsDiscussed)}.");
            }

            return sb.ToString();
        }

        /// <summary>Handles "another tip", "tell me more" and "I don't understand" without losing the topic.</summary>
        private string HandleFollowUp(string input)
        {
            Topic? mentioned = FindTopic(input);

            if (mentioned != null)
            {
                SetCurrentTopic(mentioned);
            }

            Topic? topic = mentioned ?? _currentTopic;

            if (topic == null)
            {
                return Empathise() + $"Happy to continue! Which topic would you like to explore? I can help with {TopicListText()}.";
            }

            string core;

            if (TextMatcher.ContainsAny(input, ConfusedPhrases))
            {
                core = $"Let me put it more simply. {topic.Simple}";
            }
            else if (input == "more" || TextMatcher.ContainsAny(input, MorePhrases))
            {
                core = NextMoreInfo(topic);
            }
            else
            {
                core = $"Here's another tip on {topic.DisplayName}: {NextTip(topic)}";
            }

            return Personalise(Empathise() + core);
        }

        /// <summary>Handles any message containing a recognised cybersecurity keyword.</summary>
        private string HandleTopic(string input)
        {
            Topic topic = FindTopic(input)!;
            bool topicChanged = SetCurrentTopic(topic);

            SubQuestion? specific = topic.SubQuestions.FirstOrDefault(s => TextMatcher.ContainsAny(input, s.Triggers));
            string core;

            if (specific != null)
            {
                core = specific.Answer;
            }
            else if (_sentiment == Sentiment.Worried)
            {
                // Worried users get reassurance AND a tip straight away.
                core = $"Here's a tip: {NextTip(topic)}";
            }
            else if (_sentiment is Sentiment.Frustrated or Sentiment.Overwhelmed)
            {
                core = $"Let's keep it simple. {topic.Simple} Here's one easy tip: {NextTip(topic)}";
            }
            else if (TextMatcher.ContainsAny(input, "tip", "advice", "suggest", "recommend"))
            {
                core = NextTip(topic);
            }
            else if (TextMatcher.ContainsAny(input, "what is", "what are", "whats", "define", "meaning", "explain", "describe"))
            {
                core = PickRandom(topic.Overview);
            }
            else
            {
                core = $"{PickRandom(topic.Overview)}\n\nHere's a tip: {NextTip(topic)}";
            }

            if (topicChanged)
            {
                core += "\n\nSay 'another tip' or 'tell me more' if you'd like to go deeper.";
            }

            return Personalise(Empathise() + core);
        }

        /// <summary>The user expressed a feeling but did not name a topic - respond and still give a tip.</summary>
        private string HandleSentimentOnly(string input)
        {
            string empathy = Empathise();

            if (_sentiment is Sentiment.Curious or Sentiment.Positive)
            {
                return $"{empathy}What would you like to explore? I can help with {TopicListText()}.";
            }

            Topic topic = _currentTopic ?? _knowledge.Topics[_random.Next(_knowledge.Topics.Count)];
            SetCurrentTopic(topic);

            return $"{empathy}Here's a tip about {topic.DisplayName}: {NextTip(topic)}\n\nSay 'another tip' or name a topic and I'll help further.";
        }

        private string HandleDefault()
        {
            string reply = PickRandom(_knowledge.DefaultReplies);

            if (_currentTopic != null)
            {
                return $"{reply} We were talking about {_currentTopic.DisplayName}. Try 'another tip' or 'tell me more'.";
            }

            return $"{reply} I can help with {TopicListText()}.";
        }

        // =========================================================
        // HELPERS
        // =========================================================

        /// <summary>Finds the topic whose keywords match the input best (most keyword hits).</summary>
        private Topic? FindTopic(string input)
        {
            Topic? best = null;
            int bestScore = 0;

            foreach (Topic topic in _knowledge.Topics)
            {
                int score = TextMatcher.CountMatches(input, topic.Keywords);

                if (score > bestScore)
                {
                    best = topic;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>Sets the active topic and records it in memory. Returns true if the topic changed.</summary>
        private bool SetCurrentTopic(Topic topic)
        {
            bool changed = _currentTopic?.Key != topic.Key;
            _currentTopic = topic;

            if (User != null && !User.TopicsDiscussed.Contains(topic.DisplayName))
            {
                User.TopicsDiscussed.Add(topic.DisplayName);
            }

            return changed;
        }

        /// <summary>Random tip that avoids repeating until every tip has been shown.</summary>
        private string NextTip(Topic topic)
        {
            if (!_usedTips.TryGetValue(topic.Key, out HashSet<int>? used) || used.Count >= topic.Tips.Count)
            {
                used = new HashSet<int>();
                _usedTips[topic.Key] = used;
            }

            List<int> available = Enumerable.Range(0, topic.Tips.Count).Where(i => !used.Contains(i)).ToList();
            int chosen = available[_random.Next(available.Count)];
            used.Add(chosen);

            return topic.Tips[chosen];
        }

        /// <summary>Walks through the deeper explanations in order for "tell me more".</summary>
        private string NextMoreInfo(Topic topic)
        {
            int index = _moreInfoIndex.GetValueOrDefault(topic.Key);

            if (index >= topic.MoreInfo.Count)
            {
                _moreInfoIndex[topic.Key] = 0;
                return $"That's everything I have on {topic.DisplayName} for now. I can give you another tip, or we can explore a different topic.";
            }

            _moreInfoIndex[topic.Key] = index + 1;
            return topic.MoreInfo[index];
        }

        /// <summary>Every third reply, recall the user's favourite topic and personalise the advice.</summary>
        private string Personalise(string reply)
        {
            _replyCount++;

            if (User?.FavouriteTopic != null && _replyCount % 3 == 0)
            {
                Topic? favourite = _knowledge.GetTopic(User.FavouriteTopic);

                if (favourite != null)
                {
                    reply += $"\n\nAs someone interested in {favourite.DisplayName}, {favourite.MemoryTip}";
                }
            }

            return reply;
        }

        /// <summary>Empathetic opening for the detected sentiment (empty when neutral).</summary>
        private string Empathise()
        {
            if (_sentiment == Sentiment.Neutral)
            {
                return string.Empty;
            }

            return _sentimentAnalyser.GetResponse(_sentiment, User?.Name ?? "friend", _random) + " ";
        }

        private string TopicListText() => string.Join(", ", _knowledge.Topics.Select(t => t.DisplayName));

        private string Fill(string template) => template.Replace("{name}", User?.Name ?? "friend");

        private T PickRandom<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

        private static string ToTitleCase(string text) =>
            CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower(CultureInfo.CurrentCulture));
    }
}
