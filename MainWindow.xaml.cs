using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberBot.Models;
using CyberBot.Services;
using CyberBot.Utils;

namespace CyberBot
{
    /// <summary>
    /// The main window. It contains only presentation logic: chat bubbles, the Tasks, Quiz and
    /// Activity Log tabs, and the reminder timer. All conversation logic lives in Chatbot,
    /// database access in TaskRepository and the quiz rules in QuizService.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int LogPageSize = 10;

        private readonly ActivityLog _log = new();
        private readonly TaskRepository _repo = new();
        private readonly QuizService _quiz = new();
        private readonly Chatbot _bot;
        private readonly DispatcherTimer _reminderTimer;
        private readonly Button[] _optionButtons;

        private bool _awaitingName = true;
        private bool _busy;
        private bool _quizAnswered;
        private bool _openQuizAfterReply;
        private int _logShown = LogPageSize;

        public MainWindow()
        {
            InitializeComponent();

            _bot = new Chatbot(_repo, _log);
            _optionButtons = new[] { OptionA, OptionB, OptionC, OptionD };

            AsciiLogo.Text = AsciiArt.Logo;
            QuizIntroText.Text = $"Test what you know with {_quiz.QuestionCount} questions on phishing, passwords, " +
                                 "safe browsing and social engineering. You'll get feedback after every answer.";
            ReminderDatePicker.DisplayDateStart = DateTime.Today;

            BuildTopicButtons();

            // Events: the bot tells the window when tasks change or the quiz is requested in chat.
            _bot.TasksChanged += RefreshTasks;
            _bot.QuizRequested += () => _openQuizAfterReply = true;
            _log.Changed += RefreshLog;

            _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _reminderTimer.Tick += async (_, _) => await CheckRemindersAsync();

            ShowView("Chat");
            Loaded += async (_, _) => await StartConversationAsync();
            Closed += (_, _) => _reminderTimer.Stop();
        }

        // =========================================================
        // STARTUP
        // =========================================================

        /// <summary>Voice greeting, database connection, then the text greeting and the name question.</summary>
        private async Task StartConversationAsync()
        {
            SetBusy(true);

            bool played = AudioPlayer.PlayGreeting();
            StatusText.Text = played
                ? "Voice greeting played"
                : "Voice greeting file (greeting.wav) not found - continuing without audio";
            StatusText.Foreground = (Brush)FindResource(played ? "GreenBrush" : "MutedBrush");

            bool databaseReady = await Task.Run(() => _repo.Initialise());
            ShowDatabaseStatus(databaseReady);

            await AddBotMessageAsync("Hello! Welcome to CyberBot. I'm here to help you become safer online.");

            if (!databaseReady)
            {
                await AddBotMessageAsync("Heads up: I couldn't connect to the MySQL task database, so the task features are switched off. " +
                                         "Check appsettings.json and that MySQL is running, then restart me. Everything else still works.");
            }

            await AddBotMessageAsync("First things first - what's your name?");

            SetBusy(false);
        }

        private void ShowDatabaseStatus(bool connected)
        {
            DbStatusText.Text = connected ? "Database: connected" : "Database: offline (task features disabled)";
            DbStatusText.Foreground = (Brush)FindResource(connected ? "GreenBrush" : "RedBrush");
            DbStatusText.ToolTip = connected ? null : _repo.LastError;
        }

        private void BuildTopicButtons()
        {
            foreach (Topic topic in _bot.Topics)
            {
                string displayName = topic.DisplayName;

                var button = new Button
                {
                    Content = char.ToUpper(displayName[0]) + displayName[1..],
                    Style = (Style)FindResource("PillButton"),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                button.Click += async (_, _) => await SubmitAsync($"Tell me about {displayName}");
                TopicList.Children.Add(button);
            }
        }

        // =========================================================
        // NAVIGATION
        // =========================================================

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string view })
            {
                ShowView(view);
            }
        }

        /// <summary>Shows one of the four views (Chat, Tasks, Quiz, Log) and highlights its tab button.</summary>
        private void ShowView(string view)
        {
            ChatView.Visibility = view == "Chat" ? Visibility.Visible : Visibility.Collapsed;
            TasksView.Visibility = view == "Tasks" ? Visibility.Visible : Visibility.Collapsed;
            QuizView.Visibility = view == "Quiz" ? Visibility.Visible : Visibility.Collapsed;
            LogView.Visibility = view == "Log" ? Visibility.Visible : Visibility.Collapsed;
            InputArea.Visibility = view == "Chat" ? Visibility.Visible : Visibility.Collapsed;

            var tabs = new (Button Button, string Name)[]
            {
                (NavChat, "Chat"), (NavTasks, "Tasks"), (NavQuiz, "Quiz"), (NavLog, "Log")
            };

            foreach ((Button button, string name) in tabs)
            {
                button.Style = (Style)FindResource(name == view ? "AccentButton" : "PillButton");
            }

            if (view == "Chat")
            {
                NavChat.Content = "Chat";
                ChatScroll.ScrollToEnd();
            }
            else if (view == "Tasks")
            {
                RefreshTasks();
            }
            else if (view == "Log")
            {
                RefreshLog();
            }
        }

        // =========================================================
        // CHAT
        // =========================================================

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SubmitAsync(InputBox.Text);
        }

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SubmitAsync(InputBox.Text);
            }
        }

        private async void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string message })
            {
                await SubmitAsync(message);
            }
        }

        /// <summary>Shows the user's message, gets the bot's reply and displays it.</summary>
        private async Task SubmitAsync(string? text)
        {
            if (_busy)
            {
                return;
            }

            ShowView("Chat");
            text = text?.Trim() ?? string.Empty;
            SetBusy(true);

            try
            {
                InputBox.Clear();

                if (text.Length > 0)
                {
                    AddUserMessage(text);
                }

                await ShowTypingIndicatorAsync();

                if (_awaitingName)
                {
                    bool accepted = _bot.TryRegisterUser(text, out string nameReply);
                    await AddBotMessageAsync(nameReply);

                    if (accepted)
                    {
                        _awaitingName = false;
                        await AddBotMessageAsync(_bot.GetHelpText());
                        _reminderTimer.Start();
                        RefreshTasks();
                    }
                }
                else
                {
                    string reply = _bot.GetReply(text);
                    await AddBotMessageAsync(reply);

                    if (_bot.IsFinished)
                    {
                        UpdateSidePanel();
                        InputBox.IsEnabled = false;
                        SendButton.IsEnabled = false;
                        QuickPanel.IsEnabled = false;
                        TopicList.IsEnabled = false;

                        await Task.Delay(2200);
                        Close();
                        return;
                    }

                    // The user asked for the quiz in the chat: open the Quiz tab after the reply.
                    if (_openQuizAfterReply)
                    {
                        _openQuizAfterReply = false;
                        ShowView("Quiz");
                        StartQuiz();
                    }
                }

                UpdateSidePanel();
            }
            catch (Exception)
            {
                // The GUI must never crash because of a bad message.
                await AddBotMessageAsync("Sorry, something went wrong. Please try again.");
            }
            finally
            {
                if (!_bot.IsFinished)
                {
                    SetBusy(false);
                }
            }
        }

        /// <summary>Disables input while the bot is "typing" so messages can't overlap.</summary>
        private void SetBusy(bool busy)
        {
            _busy = busy;
            InputBox.IsEnabled = !busy;
            SendButton.IsEnabled = !busy;

            bool chatReady = !busy && !_awaitingName;
            QuickPanel.IsEnabled = chatReady;
            TopicList.IsEnabled = chatReady;

            if (!busy && InputArea.Visibility == Visibility.Visible)
            {
                InputBox.Focus();
            }
        }

        private async Task ShowTypingIndicatorAsync()
        {
            TypingText.Visibility = Visibility.Visible;
            await Task.Delay(400);
            TypingText.Visibility = Visibility.Collapsed;
        }

        private void UpdateSidePanel()
        {
            User? user = _bot.User;

            NameText.Text = user?.Name ?? "-";
            InterestText.Text = _bot.GetFavouriteTopicName() ?? "Not set yet";
            TopicsText.Text = user is { TopicsDiscussed.Count: > 0 }
                ? string.Join(", ", user.TopicsDiscussed)
                : "None yet";
            MoodText.Text = SentimentAnalyser.Describe(user?.LastSentiment ?? Sentiment.Neutral);
        }

        private void AddUserMessage(string text)
        {
            (Border bubble, TextBlock body) = CreateBubble("You", isUser: true);
            body.Text = text;
            ChatPanel.Children.Add(bubble);
            ChatScroll.ScrollToEnd();
        }

        /// <summary>Adds a bot bubble and "types" the message into it (typing effect).</summary>
        private async Task AddBotMessageAsync(string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            (Border bubble, TextBlock body) = CreateBubble("CyberBot", isUser: false);
            ChatPanel.Children.Add(bubble);

            // Reveal the text in chunks so long replies still finish in about two seconds.
            int steps = Math.Min(text.Length, 110);
            int chunk = (int)Math.Ceiling(text.Length / (double)steps);
            var typed = new StringBuilder();

            for (int i = 0; i < text.Length; i += chunk)
            {
                typed.Append(text, i, Math.Min(chunk, text.Length - i));
                body.Text = typed.ToString();
                ChatScroll.ScrollToEnd();
                await Task.Delay(15);
            }
        }

        private (Border Bubble, TextBlock Body) CreateBubble(string speaker, bool isUser)
        {
            var header = new TextBlock
            {
                Text = $"{speaker}  -  {DateTime.Now:HH:mm}",
                FontSize = 11,
                Foreground = (Brush)FindResource("MutedBrush"),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var body = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                LineHeight = 21,
                Foreground = (Brush)FindResource("TextBrush")
            };

            var content = new StackPanel();
            content.Children.Add(header);
            content.Children.Add(body);

            var bubble = new Border
            {
                Child = content,
                Background = (Brush)FindResource(isUser ? "UserBubbleBrush" : "BotBubbleBrush"),
                BorderBrush = (Brush)FindResource(isUser ? "GreenBrush" : "AccentBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10),
                MaxWidth = 640,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            return (bubble, body);
        }

        // =========================================================
        // TASKS TAB (MySQL)
        // =========================================================

        /// <summary>Reloads the task list from the database.</summary>
        private void RefreshTasks()
        {
            if (!_repo.IsAvailable)
            {
                TaskList.ItemsSource = null;
                ShowTaskMessage("The task database is offline. Check appsettings.json and make sure MySQL is running.", isError: true);
                return;
            }

            List<TaskItem> tasks = _repo.GetAll();

            if (_repo.LastError != null)
            {
                ShowTaskMessage("Couldn't load tasks: " + _repo.LastError, isError: true);
                return;
            }

            TaskList.ItemsSource = tasks;
        }

        private void ShowTaskMessage(string message, bool isError = false)
        {
            TaskMessage.Text = message;
            TaskMessage.Foreground = (Brush)FindResource(isError ? "RedBrush" : "MutedBrush");
        }

        /// <summary>Reads the date picker + time box. Returns false (with a message) if the input is invalid.</summary>
        private bool TryReadReminder(out DateTime? reminder)
        {
            reminder = null;

            if (ReminderDatePicker.SelectedDate is not DateTime date)
            {
                return true;   // reminders are optional
            }

            if (!TimeSpan.TryParse(ReminderTimeBox.Text.Trim(), out TimeSpan time) ||
                time < TimeSpan.Zero || time >= TimeSpan.FromHours(24))
            {
                ShowTaskMessage("Please enter the reminder time as HH:mm, for example 09:00.", isError: true);
                return false;
            }

            DateTime when = date.Date + time;

            if (when <= DateTime.Now)
            {
                ShowTaskMessage("The reminder must be in the future.", isError: true);
                return false;
            }

            reminder = when;
            return true;
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_repo.IsAvailable)
            {
                ShowTaskMessage("The task database is offline, so tasks can't be saved.", isError: true);
                return;
            }

            string title = TaskTitleBox.Text.Trim();

            if (title.Length == 0)
            {
                ShowTaskMessage("Please enter a task title.", isError: true);
                return;
            }

            if (!TryReadReminder(out DateTime? reminder))
            {
                return;
            }

            string description = string.IsNullOrWhiteSpace(TaskDescBox.Text)
                ? _bot.DescribeTask(title)
                : TaskDescBox.Text.Trim();

            int id = _repo.Add(new TaskItem
            {
                Title = title,
                Description = description,
                ReminderDate = reminder,
                CreatedAt = DateTime.Now
            });

            if (id <= 0)
            {
                ShowTaskMessage("Couldn't save the task: " + _repo.LastError, isError: true);
                return;
            }

            _log.Add(reminder is DateTime when
                ? $"Task added: '{title}' (reminder set for {TaskItem.FormatDate(when)})"
                : $"Task added: '{title}' (no reminder set)");

            TaskTitleBox.Clear();
            TaskDescBox.Clear();
            ReminderDatePicker.SelectedDate = null;
            ReminderTimeBox.Text = "09:00";

            RefreshTasks();
            ShowTaskMessage($"Task '{title}' added.");
        }

        private void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskList.SelectedItem is not TaskItem task)
            {
                ShowTaskMessage("Select a task first.", isError: true);
                return;
            }

            if (task.IsCompleted)
            {
                ShowTaskMessage("That task is already completed.");
                return;
            }

            if (!_repo.MarkCompleted(task.Id))
            {
                ShowTaskMessage("Couldn't update the task: " + _repo.LastError, isError: true);
                return;
            }

            _log.Add($"Task marked as completed: '{task.Title}'");
            RefreshTasks();
            ShowTaskMessage($"Well done! '{task.Title}' is completed.");
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskList.SelectedItem is not TaskItem task)
            {
                ShowTaskMessage("Select a task first.", isError: true);
                return;
            }

            if (!_repo.Delete(task.Id))
            {
                ShowTaskMessage("Couldn't delete the task: " + _repo.LastError, isError: true);
                return;
            }

            _log.Add($"Task deleted: '{task.Title}'");
            RefreshTasks();
            ShowTaskMessage($"'{task.Title}' deleted.");
        }

        private void SetReminderButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskList.SelectedItem is not TaskItem task)
            {
                ShowTaskMessage("Select a task first.", isError: true);
                return;
            }

            if (ReminderDatePicker.SelectedDate == null)
            {
                ShowTaskMessage("Pick a reminder date (and time) in the form above first.", isError: true);
                return;
            }

            if (!TryReadReminder(out DateTime? reminder) || reminder is not DateTime when)
            {
                return;
            }

            if (!_repo.SetReminder(task.Id, when))
            {
                ShowTaskMessage("Couldn't set the reminder: " + _repo.LastError, isError: true);
                return;
            }

            _log.Add($"Reminder set: '{task.Title}' for {TaskItem.FormatDate(when)}");
            RefreshTasks();
            ShowTaskMessage($"Reminder set for {TaskItem.FormatDate(when)}.");
        }

        private void RefreshTasksButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshTasks();
            ShowTaskMessage("Task list refreshed.");
        }

        // =========================================================
        // REMINDERS (timer)
        // =========================================================

        /// <summary>Every 20 seconds: announce any reminders that have come due.</summary>
        private async Task CheckRemindersAsync()
        {
            if (_busy || _awaitingName || _bot.IsFinished || !_repo.IsAvailable)
            {
                return;
            }

            try
            {
                List<TaskItem> due = _repo.GetDueReminders(DateTime.Now);

                if (due.Count == 0)
                {
                    return;
                }

                SetBusy(true);

                try
                {
                    foreach (TaskItem task in due)
                    {
                        _repo.MarkNotified(task.Id);
                        _log.Add($"Reminder triggered: '{task.Title}'");
                        await AddBotMessageAsync($"⏰ Reminder: '{task.Title}' is due now. {task.Description}");
                    }

                    if (_currentViewIsNotChat())
                    {
                        NavChat.Content = "Chat •";
                    }

                    RefreshTasks();
                }
                finally
                {
                    SetBusy(false);
                }
            }
            catch (Exception)
            {
                // A failed reminder check should never disturb the user; try again on the next tick.
            }
        }

        private bool _currentViewIsNotChat() => ChatView.Visibility != Visibility.Visible;

        // =========================================================
        // QUIZ TAB
        // =========================================================

        private void StartQuizButton_Click(object sender, RoutedEventArgs e)
        {
            StartQuiz();
        }

        private void StartQuiz()
        {
            _quiz.Start();
            _log.Add($"Quiz started ({_quiz.Total} questions)");

            QuizStartPanel.Visibility = Visibility.Collapsed;
            QuizResultPanel.Visibility = Visibility.Collapsed;
            QuizPlayPanel.Visibility = Visibility.Visible;

            ShowQuestion();
        }

        private void ShowQuestion()
        {
            QuizQuestion question = _quiz.Current;

            QuizProgressText.Text = $"Question {_quiz.Index + 1} of {_quiz.Total}";
            QuizScoreText.Text = $"Score: {_quiz.Score}";
            QuizQuestionText.Text = question.Text;
            QuizFeedbackText.Text = string.Empty;
            NextQuestionButton.Visibility = Visibility.Collapsed;
            _quizAnswered = false;

            for (int i = 0; i < _optionButtons.Length; i++)
            {
                Button button = _optionButtons[i];

                if (i < question.Options.Length)
                {
                    button.Visibility = Visibility.Visible;
                    button.Content = new TextBlock
                    {
                        Text = $"{(char)('A' + i)})  {question.Options[i]}",
                        TextWrapping = TextWrapping.Wrap
                    };
                    button.ClearValue(Control.BackgroundProperty);   // remove the green/red from the last question
                    button.ClearValue(Control.BorderBrushProperty);
                }
                else
                {
                    button.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void QuizOption_Click(object sender, RoutedEventArgs e)
        {
            if (_quizAnswered || sender is not Button { Tag: string tag })
            {
                return;
            }

            int choice = int.Parse(tag);
            QuizQuestion question = _quiz.Current;
            bool correct = _quiz.Answer(choice);
            _quizAnswered = true;

            // Immediate visual feedback: correct answer green, wrong choice red.
            _optionButtons[question.CorrectIndex].Background = (Brush)FindResource("GreenBrush");

            if (!correct)
            {
                _optionButtons[choice].Background = (Brush)FindResource("RedBrush");
            }

            QuizFeedbackText.Text = correct
                ? $"✔ Correct! {question.Explanation}"
                : $"✘ Not quite. The correct answer is {(char)('A' + question.CorrectIndex)}) {question.Options[question.CorrectIndex]}.\n{question.Explanation}";

            QuizScoreText.Text = $"Score: {_quiz.Score}";
            NextQuestionButton.Content = _quiz.IsLast ? "See my results" : "Next question";
            NextQuestionButton.Visibility = Visibility.Visible;
        }

        private void NextQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_quiz.Advance())
            {
                ShowQuestion();
                return;
            }

            QuizPlayPanel.Visibility = Visibility.Collapsed;
            QuizResultPanel.Visibility = Visibility.Visible;
            QuizFinalScoreText.Text = $"{_quiz.Score} / {_quiz.Total}";
            QuizFinalMessageText.Text = _quiz.GetFinalFeedback();

            _log.Add($"Quiz completed: scored {_quiz.Score} out of {_quiz.Total}");
        }

        // =========================================================
        // ACTIVITY LOG TAB
        // =========================================================

        private void RefreshLog()
        {
            List<ActivityEntry> entries = _log.GetRecent(_logShown);
            LogList.ItemsSource = entries;

            LogSummaryText.Text = _log.Count == 0
                ? "No activity yet. Add a task, take the quiz or chat with me!"
                : $"Showing the {entries.Count} most recent of {_log.Count} actions (newest first).";

            ShowMoreLogButton.IsEnabled = _log.Count > _logShown;
        }

        private void ShowMoreLogButton_Click(object sender, RoutedEventArgs e)
        {
            _logShown += LogPageSize;
            RefreshLog();
        }

        private void ShowLessLogButton_Click(object sender, RoutedEventArgs e)
        {
            _logShown = LogPageSize;
            RefreshLog();
        }
    }
}
