using System.Globalization;

namespace CyberBot.Models
{
    /// <summary>
    /// One cybersecurity task stored in the MySQL "Tasks" table.
    /// The read-only properties at the bottom are used by the GUI to display the task.
    /// </summary>
    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>Optional reminder date/time (null = no reminder).</summary>
        public DateTime? ReminderDate { get; set; }

        public bool IsCompleted { get; set; }

        /// <summary>True once the "reminder is due" message has been shown, so it only fires once.</summary>
        public bool ReminderNotified { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ----- Display helpers (data-bound in MainWindow.xaml) -----

        public string StatusIcon => IsCompleted ? "✔" : "○";

        public string HeadingText => $"#{Id}   {Title}";

        public string ReminderDisplay
        {
            get
            {
                if (ReminderDate is null)
                {
                    return "No reminder set";
                }

                string text = $"⏰ Reminder: {FormatDate(ReminderDate.Value)}";

                if (!IsCompleted && ReminderDate < DateTime.Now)
                {
                    text += "  (due)";
                }

                return text;
            }
        }

        /// <summary>Consistent date format used in the GUI, chat replies and activity log.</summary>
        public static string FormatDate(DateTime when) =>
            when.ToString("ddd, dd MMM yyyy 'at' HH:mm", CultureInfo.CurrentCulture);
    }
}
