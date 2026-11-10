using CyberBot.Models;

namespace CyberBot.Services
{
    /// <summary>
    /// Records every significant action the chatbot takes (tasks, reminders, quiz, NLP commands).
    /// Stored in a List and read back newest-first.
    /// </summary>
    public class ActivityLog
    {
        private readonly List<ActivityEntry> _entries = new();

        /// <summary>Raised whenever a new entry is added so the GUI can refresh.</summary>
        public event Action? Changed;

        public int Count => _entries.Count;

        public void Add(string description)
        {
            _entries.Add(new ActivityEntry(DateTime.Now, description));
            Changed?.Invoke();
        }

        /// <summary>Returns entries newest-first, skipping <paramref name="skip"/> and taking <paramref name="count"/>.</summary>
        public List<ActivityEntry> GetRecent(int count, int skip = 0)
        {
            return Enumerable.Reverse(_entries).Skip(skip).Take(count).ToList();
        }
    }
}
