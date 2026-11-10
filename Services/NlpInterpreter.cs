using System.Text.RegularExpressions;
using CyberBot.Utils;

namespace CyberBot.Services
{
    /// <summary>The kinds of command the NLP simulation can recognise.</summary>
    public enum NlpIntent
    {
        None,
        AddTask,
        SetReminder,
        ViewTasks,
        CompleteTask,
        DeleteTask,
        StartQuiz,
        ShowLog,
        ShowMoreLog
    }

    /// <summary>What the interpreter understood from a message.</summary>
    public class NlpResult
    {
        public NlpIntent Intent { get; init; } = NlpIntent.None;

        /// <summary>Cleaned-up task title (for AddTask / SetReminder).</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Reminder date/time found in the message, if any.</summary>
        public DateTime? When { get; init; }

        /// <summary>Task number found in the message (e.g. "complete task 3").</summary>
        public int? TaskNumber { get; init; }
    }

    /// <summary>
    /// Simulated Natural Language Processing using keyword detection and regular expressions.
    /// It understands many different phrasings, e.g. "add a task to enable 2FA",
    /// "can you remind me to update my password tomorrow" or "what have you done for me?".
    /// </summary>
    public class NlpInterpreter
    {
        // ----- Command patterns (run on text that was already normalised: lower-case, no punctuation) -----

        private static readonly Regex AddTaskRegex = new(
            @"\b(?:add|create|make|new|set up)\s+(?:an?\s+|another\s+|my\s+|the\s+)?(?:new\s+)?task\b\s*(?:to\s+|for\s+|called\s+|named\s+|about\s+)?(.*)$",
            RegexOptions.Compiled);

        private static readonly Regex ReminderRegex = new(
            @"\b(?:remind me|reminder)\s*(?:to\s+|about\s+|for\s+|that\s+)?(.*)$",
            RegexOptions.Compiled);

        private static readonly Regex TaskNumberRegex = new(
            @"\btask\s*(?:number\s+|no\s+)?(\d+)\b", RegexOptions.Compiled);

        // ----- Date / time patterns -----

        private static readonly Regex RelativeRegex = new(
            @"\b(?:in|after|within)\s+(\d+|a|an|one|two|three|four|five|six|seven|eight|nine|ten)\s+(minute|hour|day|week|month)s?\b",
            RegexOptions.Compiled);

        private static readonly Regex TomorrowRegex = new(@"\btomorrow\b", RegexOptions.Compiled);
        private static readonly Regex TodayRegex = new(@"\b(today|tonight)\b", RegexOptions.Compiled);
        private static readonly Regex NextWeekRegex = new(@"\bnext week\b", RegexOptions.Compiled);
        private static readonly Regex NextMonthRegex = new(@"\bnext month\b", RegexOptions.Compiled);

        private static readonly Regex WeekdayRegex = new(
            @"\b(?:on\s+|next\s+|this\s+)?(sunday|monday|tuesday|wednesday|thursday|friday|saturday)\b",
            RegexOptions.Compiled);

        private static readonly Regex IsoDateRegex = new(@"\b(\d{4})\s(\d{1,2})\s(\d{1,2})\b", RegexOptions.Compiled);

        private static readonly Regex DayMonthRegex = new(
            @"\b(?:on\s+)?(\d{1,2})(?:st|nd|rd|th)?\s+(january|february|march|april|may|june|july|august|september|october|november|december)(?:\s+(\d{4}))?\b",
            RegexOptions.Compiled);

        private static readonly string[] Days =
            { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };

        private static readonly string[] Months =
        {
            "january", "february", "march", "april", "may", "june",
            "july", "august", "september", "october", "november", "december"
        };

        private static readonly Dictionary<string, int> NumberWords = new()
        {
            ["a"] = 1, ["an"] = 1, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
            ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10
        };

        private static readonly Dictionary<string, string> Acronyms = new()
        {
            ["2fa"] = "2FA", ["mfa"] = "MFA", ["vpn"] = "VPN", ["otp"] = "OTP",
            ["sms"] = "SMS", ["wifi"] = "Wi-Fi", ["pin"] = "PIN"
        };

        // =========================================================
        // INTENT RECOGNITION
        // =========================================================

        /// <summary>Works out what the user wants from a normalised message.</summary>
        public NlpResult Interpret(string input, DateTime now)
        {
            // Activity log
            if (TextMatcher.ContainsAny(input, "activity log", "what have you done", "what did you do",
                                        "recent actions", "show log", "action log"))
            {
                return new NlpResult { Intent = NlpIntent.ShowLog };
            }

            if (input is "show more" or "more log" or "show older" or "show more actions")
            {
                return new NlpResult { Intent = NlpIntent.ShowMoreLog };
            }

            // Quiz
            if (TextMatcher.ContainsAny(input, "quiz", "mini game", "play a game", "start the game", "start game",
                                        "test my knowledge", "test me"))
            {
                return new NlpResult { Intent = NlpIntent.StartQuiz };
            }

            bool hasTask = TextMatcher.Contains(input, "task");
            Match numbered = TaskNumberRegex.Match(input);
            int? number = numbered.Success ? int.Parse(numbered.Groups[1].Value) : null;

            // "complete task 3" / "delete task 3" (explicit number wins over everything else)
            if (number != null && TextMatcher.ContainsAny(input, "complete", "finish", "done", "mark"))
            {
                return new NlpResult { Intent = NlpIntent.CompleteTask, TaskNumber = number };
            }

            if (number != null && TextMatcher.ContainsAny(input, "delete", "remove", "cancel", "discard"))
            {
                return new NlpResult { Intent = NlpIntent.DeleteTask, TaskNumber = number };
            }

            // "add a task to enable 2FA"
            Match add = AddTaskRegex.Match(input);

            if (add.Success)
            {
                (DateTime? when, string remaining) = ExtractTime(add.Groups[1].Value, now);
                return new NlpResult { Intent = NlpIntent.AddTask, Title = CleanTitle(remaining), When = when };
            }

            // "remind me to update my password tomorrow" / "set a reminder to check my privacy settings"
            Match reminder = ReminderRegex.Match(input);

            if (reminder.Success)
            {
                (DateTime? when, string remaining) = ExtractTime(reminder.Groups[1].Value, now);
                return new NlpResult { Intent = NlpIntent.SetReminder, Title = CleanTitle(remaining), When = when };
            }

            // Complete / delete without a number ("I finished the task about 2FA")
            if (hasTask && TextMatcher.ContainsAny(input, "complete", "finish", "done", "mark"))
            {
                return new NlpResult { Intent = NlpIntent.CompleteTask };
            }

            if (hasTask && TextMatcher.ContainsAny(input, "delete", "remove", "cancel", "discard"))
            {
                return new NlpResult { Intent = NlpIntent.DeleteTask };
            }

            // View tasks
            if (TextMatcher.ContainsAny(input, "my tasks", "show tasks", "view tasks", "list tasks", "task list",
                                        "to do list", "todo list", "what tasks", "all tasks", "display tasks") ||
                (hasTask && TextMatcher.ContainsAny(input, "show", "view", "list", "display", "see")))
            {
                return new NlpResult { Intent = NlpIntent.ViewTasks };
            }

            return new NlpResult();
        }

        // =========================================================
        // DATE / TIME EXTRACTION
        // =========================================================

        /// <summary>
        /// Finds a date/time phrase in the text ("in 3 days", "tomorrow", "next Friday", "5 november")
        /// and returns it together with the text that is left over.
        /// </summary>
        public (DateTime? When, string Remaining) ExtractTime(string text, DateTime now)
        {
            Match m = RelativeRegex.Match(text);

            if (m.Success)
            {
                int amount = ParseNumber(m.Groups[1].Value);

                DateTime when = m.Groups[2].Value switch
                {
                    "minute" => now.AddMinutes(amount),
                    "hour" => now.AddHours(amount),
                    "day" => AtNine(now.AddDays(amount)),
                    "week" => AtNine(now.AddDays(7 * amount)),
                    _ => AtNine(now.AddMonths(amount))
                };

                return (when, Strip(text, m));
            }

            m = DayMonthRegex.Match(text);

            if (m.Success)
            {
                int day = int.Parse(m.Groups[1].Value);
                int month = Array.IndexOf(Months, m.Groups[2].Value) + 1;
                bool hasYear = m.Groups[3].Success;
                int year = hasYear ? int.Parse(m.Groups[3].Value) : now.Year;

                if (TryDate(year, month, day, out DateTime date))
                {
                    if (!hasYear && date <= now)
                    {
                        date = date.AddYears(1);
                    }

                    return (date, Strip(text, m));
                }
            }

            m = IsoDateRegex.Match(text);

            if (m.Success && TryDate(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                                     int.Parse(m.Groups[3].Value), out DateTime isoDate))
            {
                return (isoDate, Strip(text, m));
            }

            m = TomorrowRegex.Match(text);
            if (m.Success)
            {
                return (AtNine(now.AddDays(1)), Strip(text, m));
            }

            m = NextWeekRegex.Match(text);
            if (m.Success)
            {
                return (AtNine(now.AddDays(7)), Strip(text, m));
            }

            m = NextMonthRegex.Match(text);
            if (m.Success)
            {
                return (AtNine(now.AddMonths(1)), Strip(text, m));
            }

            m = TodayRegex.Match(text);
            if (m.Success)
            {
                DateTime when = now.Date.AddHours(m.Groups[1].Value == "tonight" ? 20 : 17);

                if (when <= now)
                {
                    when = now.AddHours(1);
                }

                return (when, Strip(text, m));
            }

            m = WeekdayRegex.Match(text);
            if (m.Success)
            {
                int target = Array.IndexOf(Days, m.Groups[1].Value);
                int difference = (target - (int)now.DayOfWeek + 7) % 7;

                if (difference == 0)
                {
                    difference = 7;
                }

                return (AtNine(now.AddDays(difference)), Strip(text, m));
            }

            return (null, text);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static DateTime AtNine(DateTime date) => date.Date.AddHours(9);

        private static int ParseNumber(string text) =>
            int.TryParse(text, out int number) ? number : NumberWords.GetValueOrDefault(text, 1);

        private static bool TryDate(int year, int month, int day, out DateTime date)
        {
            try
            {
                date = new DateTime(year, month, day, 9, 0, 0);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                date = default;
                return false;
            }
        }

        /// <summary>Removes the matched date phrase and any dangling words like "by" or "on".</summary>
        private static string Strip(string text, Match match)
        {
            string result = text.Remove(match.Index, match.Length);
            result = Regex.Replace(result, @"\s+", " ").Trim();
            return Regex.Replace(result, @"\s*\b(?:on|by|for|at|in|due|before)\s*$", string.Empty).Trim();
        }

        /// <summary>Turns "enable 2fa" into "Enable 2FA".</summary>
        private static string CleanTitle(string raw)
        {
            string title = Regex.Replace(raw.Trim(), @"^(?:to|that|about|for)\s+", string.Empty);
            title = Regex.Replace(title, @"\s+", " ").Trim();

            if (title.Length == 0)
            {
                return string.Empty;
            }

            if (title.Length > 100)
            {
                title = title[..100].TrimEnd();
            }

            string[] words = title.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                if (Acronyms.TryGetValue(words[i], out string? acronym))
                {
                    words[i] = acronym;
                }
            }

            title = string.Join(' ', words);
            return char.ToUpper(title[0]) + title[1..];
        }
    }
}
