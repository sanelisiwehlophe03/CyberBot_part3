using System.Text;
using CyberBot.Models;
using CyberBot.Utils;

namespace CyberBot.Services
{
    /// <summary>
    /// Part 3 / POE additions to the chatbot: task assistant with reminders (MySQL),
    /// NLP command handling, quiz launching and the activity log.
    /// (The Part 2 features live in Chatbot.cs - this is the same class, split across two files.)
    /// </summary>
    public partial class Chatbot
    {
        private const int LogPageSize = 5;

        /// <summary>Where the bot is in a multi-step conversation (e.g. "Would you like a reminder?").</summary>
        private enum PendingAction
        {
            None,
            ReminderChoice,   // waiting for yes / no / a time
            ReminderTime      // waiting for a time such as "in 3 days"
        }

        private readonly TaskRepository _repo;
        private readonly ActivityLog _log;
        private readonly NlpInterpreter _nlp = new();

        private NlpResult _nlpResult = new();
        private PendingAction _pending = PendingAction.None;
        private int _pendingTaskId;

        private bool _lastWasLog;
        private bool _previousWasLog;
        private int _logOffset;

        /// <summary>Raised when tasks change in the database so the GUI list can refresh.</summary>
        public event Action? TasksChanged;

        /// <summary>Raised when the user asks for the quiz in the chat.</summary>
        public event Action? QuizRequested;

        /// <summary>Generates a helpful description for a task from keywords in its title.</summary>
        public string DescribeTask(string title)
        {
            string input = TextMatcher.Normalise(title);

            foreach ((string[] keywords, string description) in _knowledge.TaskTemplates)
            {
                if (TextMatcher.ContainsAny(input, keywords))
                {
                    return description;
                }
            }

            return $"Cybersecurity task: {title}. Completing it will improve your online safety.";
        }

        // =========================================================
        // NLP COMMAND ROUTING
        // =========================================================

        /// <summary>Intent matcher: does the message contain a task / quiz / log command?</summary>
        private bool IsNlpCommand(string input)
        {
            _nlpResult = _nlp.Interpret(input, DateTime.Now);

            return _nlpResult.Intent switch
            {
                NlpIntent.None => false,
                NlpIntent.ShowMoreLog => _previousWasLog,   // "show more" only means "more log" right after the log
                _ => true
            };
        }

        private string HandleNlpCommand(string input)
        {
            return _nlpResult.Intent switch
            {
                NlpIntent.AddTask => HandleAddTask(isReminder: false),
                NlpIntent.SetReminder => HandleAddTask(isReminder: true),
                NlpIntent.ViewTasks => HandleViewTasks(),
                NlpIntent.CompleteTask => HandleCompleteOrDelete(input, complete: true),
                NlpIntent.DeleteTask => HandleCompleteOrDelete(input, complete: false),
                NlpIntent.StartQuiz => HandleStartQuiz(),
                NlpIntent.ShowLog => HandleShowLog(more: false),
                NlpIntent.ShowMoreLog => HandleShowLog(more: true),
                _ => HandleDefault()
            };
        }

        // =========================================================
        // TASKS
        // =========================================================

        private string HandleAddTask(bool isReminder)
        {
            string title = _nlpResult.Title;

            if (string.IsNullOrWhiteSpace(title))
            {
                return isReminder
                    ? "Sure! What should I remind you about? For example: 'Remind me to update my password tomorrow'."
                    : "Sure! What should the task be? For example: 'Add task - Review privacy settings'.";
            }

            if (!_repo.IsAvailable)
            {
                return DatabaseOfflineMessage();
            }

            var task = new TaskItem
            {
                Title = title,
                Description = DescribeTask(title),
                ReminderDate = _nlpResult.When,
                CreatedAt = DateTime.Now
            };

            int id = _repo.Add(task);

            if (id <= 0)
            {
                return DatabaseErrorMessage();
            }

            _pendingTaskId = id;
            TasksChanged?.Invoke();

            // A reminder time was included in the same sentence - we're done.
            if (task.ReminderDate is DateTime when)
            {
                string formatted = TaskItem.FormatDate(when);

                if (isReminder)
                {
                    _log.Add($"Reminder set: '{title}' for {formatted} [NLP]");
                    return $"Reminder set for '{title}' on {formatted}.";
                }

                _log.Add($"Task added: '{title}' (reminder set for {formatted}) [NLP]");
                return $"Task added: '{title}'. {task.Description}\nI'll remind you on {formatted}.";
            }

            _log.Add($"Task added: '{title}' (no reminder set) [NLP]");

            if (isReminder)
            {
                _pending = PendingAction.ReminderTime;
                return $"Sure! I've added '{title}' to your tasks. When should I remind you? " +
                       "You can say 'in 3 days', 'tomorrow' or 'next Monday'.";
            }

            _pending = PendingAction.ReminderChoice;
            return $"Task added: '{title}'.\nDescription: \"{task.Description}\"\nWould you like to set a reminder for this task?";
        }

        /// <summary>Handles the answer to "Would you like a reminder?" / "When should I remind you?".</summary>
        private string? HandlePending(string input)
        {
            (DateTime? when, _) = _nlp.ExtractTime(input, DateTime.Now);
            string firstWord = input.Split(' ')[0];

            bool yes = firstWord is "yes" or "yeah" or "yep" or "sure" or "ok" or "okay" or "please" or "y";
            bool no = firstWord is "no" or "nope" or "nah" or "n"
                      || TextMatcher.ContainsAny(input, "not now", "skip", "no thanks", "no reminder");

            if (when != null)
            {
                return ApplyReminder(when.Value);
            }

            if (no)
            {
                _pending = PendingAction.None;
                return "No problem! The task is saved without a reminder.";
            }

            if (yes)
            {
                _pending = PendingAction.ReminderTime;
                return "When should I remind you? You can say 'in 3 days', 'tomorrow' or 'next Monday'.";
            }

            // The user moved on to something else - drop the question and handle the message normally.
            bool otherCommand = _nlp.Interpret(input, DateTime.Now).Intent != NlpIntent.None || FindTopic(input) != null;

            if (_pending == PendingAction.ReminderChoice || otherCommand)
            {
                _pending = PendingAction.None;
                return null;
            }

            return "I couldn't work out the date. Try 'in 3 days', 'tomorrow' or 'next Friday', or say 'no' to skip the reminder.";
        }

        private string ApplyReminder(DateTime when)
        {
            if (when <= DateTime.Now)
            {
                return "That time has already passed. Please give me a future date, for example 'in 3 days'.";
            }

            TaskItem? task = _repo.Get(_pendingTaskId);

            if (task == null || !_repo.SetReminder(task.Id, when))
            {
                _pending = PendingAction.None;
                return DatabaseErrorMessage();
            }

            string formatted = TaskItem.FormatDate(when);
            _log.Add($"Task updated: reminder set for '{task.Title}' on {formatted} [NLP]");
            _pending = PendingAction.None;
            TasksChanged?.Invoke();

            return $"Got it! I'll remind you on {formatted}.";
        }

        private string HandleViewTasks()
        {
            if (!_repo.IsAvailable)
            {
                return DatabaseOfflineMessage();
            }

            List<TaskItem> tasks = _repo.GetAll();

            if (_repo.LastError != null)
            {
                return DatabaseErrorMessage();
            }

            _log.Add("Showed the task list [NLP]");

            if (tasks.Count == 0)
            {
                return "You don't have any tasks yet. Try 'Add task - Enable two-factor authentication'.";
            }

            var sb = new StringBuilder("Here are your tasks:");

            foreach (TaskItem task in tasks.Take(12))
            {
                sb.Append($"\n\n#{task.Id} {task.StatusIcon} {task.Title}");
                sb.Append($"\n   {task.Description}");
                sb.Append($"\n   {task.ReminderDisplay}");
            }

            if (tasks.Count > 12)
            {
                sb.Append($"\n\n...and {tasks.Count - 12} more in the Tasks tab.");
            }

            sb.Append("\n\nSay 'complete task 3' or 'delete task 3' to manage them.");
            return sb.ToString();
        }

        private string HandleCompleteOrDelete(string input, bool complete)
        {
            if (!_repo.IsAvailable)
            {
                return DatabaseOfflineMessage();
            }

            TaskItem? task;

            if (_nlpResult.TaskNumber is int number)
            {
                task = _repo.Get(number);

                if (task == null)
                {
                    return $"I couldn't find task #{number}. Say 'show my tasks' to see the task numbers.";
                }
            }
            else
            {
                task = FindTaskByText(input, _repo.GetAll());

                if (task == null)
                {
                    return "Which task do you mean? Say 'show my tasks' to see them, then 'complete task 3' or 'delete task 3'.";
                }
            }

            if (complete)
            {
                if (task.IsCompleted)
                {
                    return $"'{task.Title}' is already marked as completed.";
                }

                if (!_repo.MarkCompleted(task.Id))
                {
                    return DatabaseErrorMessage();
                }

                _log.Add($"Task marked as completed: '{task.Title}' [NLP]");
                TasksChanged?.Invoke();
                return $"Well done! '{task.Title}' is now marked as completed. ✔";
            }

            if (!_repo.Delete(task.Id))
            {
                return DatabaseErrorMessage();
            }

            _log.Add($"Task deleted: '{task.Title}' [NLP]");
            TasksChanged?.Invoke();
            return $"I've deleted '{task.Title}' from your tasks.";
        }

        /// <summary>Finds the task whose title shares the most words with the message (for "I finished the 2FA task").</summary>
        private static TaskItem? FindTaskByText(string input, IEnumerable<TaskItem> tasks)
        {
            TaskItem? best = null;
            int bestScore = 0;

            foreach (TaskItem task in tasks)
            {
                IEnumerable<string> words = TextMatcher.Normalise(task.Title).Split(' ').Where(w => w.Length > 3);
                int score = TextMatcher.CountMatches(input, words);

                if (score > bestScore)
                {
                    best = task;
                    bestScore = score;
                }
            }

            return best;
        }

        // =========================================================
        // QUIZ & ACTIVITY LOG
        // =========================================================

        private string HandleStartQuiz()
        {
            QuizRequested?.Invoke();
            return "Great idea! Let's test your cybersecurity knowledge. I'm opening the Quiz tab for you.";
        }

        /// <summary>Shows the log 5 entries at a time; "show more" continues from where it stopped.</summary>
        private string HandleShowLog(bool more)
        {
            if (_log.Count == 0)
            {
                return "I haven't done anything yet. Try adding a task or taking the quiz!";
            }

            if (!more)
            {
                _logOffset = 0;
            }

            List<ActivityEntry> entries = _log.GetRecent(LogPageSize, _logOffset);

            if (entries.Count == 0)
            {
                return "That's the full activity history.";
            }

            var sb = new StringBuilder(_logOffset == 0 ? "Here's a summary of recent actions:" : "Earlier actions:");

            for (int i = 0; i < entries.Count; i++)
            {
                sb.Append($"\n {_logOffset + i + 1}. [{entries[i].Timestamp:HH:mm}] {entries[i].Description}");
            }

            _logOffset += entries.Count;
            _lastWasLog = _logOffset < _log.Count;

            if (_lastWasLog)
            {
                sb.Append("\n\nSay 'show more' to see older actions.");
            }

            return sb.ToString();
        }

        // =========================================================
        // ERROR MESSAGES
        // =========================================================

        private string DatabaseOfflineMessage() =>
            "I can't reach the task database right now. Please check that MySQL is running and that " +
            "appsettings.json has the right connection details, then restart the app.";

        private string DatabaseErrorMessage() =>
            "Sorry, I couldn't save that to the database. " + (_repo.LastError ?? "Please try again.");
    }
}
