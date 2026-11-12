# CyberBot - Cybersecurity Awareness Chatbot (Part 3 / POE)

A Windows desktop chatbot (WPF, .NET 8) that teaches South African users about cybersecurity.
Part 3 adds a MySQL task assistant with reminders, a quiz mini-game, NLP command recognition and an activity log.

![CI](https://github.com/<your-username>/<your-repo>/actions/workflows/dotnet.yml/badge.svg)

Video presentation:https://advtechonline-my.sharepoint.com/:v:/r/personal/st10500376_rcconnect_edu_za/_layouts/15/stream.aspx?id=%2Fpersonal%2Fst10500376%5Frcconnect%5Fedu%5Fza%2FDocuments%2FLabVM2334547%20%2D%20labvmdnsm4okohpypfuo4%2Esouthafricanorth%2Ecloudapp%2Eazure%2Ecom%5F3389%20%2D%20Remote%20Desktop%20Connection%202026%2D11%2D12%2008%2D51%2D56%2Emp4&share=cQqhrs1S1VhyR4mbAROxtmBtEgUC1Y0gZzSoju2cipCSdLfUZg

## How to run
1. Install the **.NET 8 SDK** and **MySQL Server** (or XAMPP) on Windows. Make sure MySQL is running.
2. Open `appsettings.json` and replace `YOUR_MYSQL_PASSWORD` with your MySQL root password.
   (The app creates the `cyberbot_db` database and `Tasks` table automatically on first run. `database.sql` is provided if you prefer to do it by hand.)
3. Put your own `greeting.wav` next to `CyberBot.csproj`.
4. Open `CyberBot.sln` in Visual Studio 2022 and press **F5** (or run `dotnet run`).

If MySQL is unavailable the app still starts; only the task features are disabled and a clear message is shown.

## Features
| Feature | Where |
|---|---|
| Part 1: voice greeting, ASCII logo, name, validation | `Utils/AudioPlayer.cs`, `Utils/AsciiArt.cs`, `MainWindow` |
| Part 2: keywords, random tips, follow-ups, memory, sentiment | `Services/Chatbot.cs`, `KnowledgeBase.cs`, `SentimentAnalyser.cs` |
| Task assistant: add / view / complete / delete with reminders (MySQL) | `Services/TaskRepository.cs`, `Services/Chatbot.Tasks.cs`, Tasks tab |
| Reminder system: due reminders announced in chat (checked every 20 s) | `MainWindow.CheckRemindersAsync` |
| Quiz: 12 questions, multiple choice + true/false, feedback, score | `Services/QuizService.cs`, Quiz tab |
| NLP simulation: keyword detection + regular expressions + date parsing | `Services/NlpInterpreter.cs` |
| Activity log: timestamps, last 5 in chat ("show more"), 10 at a time in the tab | `Services/ActivityLog.cs`, Activity Log tab |

## Try these in the chat
- `Add task - Review privacy settings` then `Yes, remind me in 3 days`
- `Can you remind me to update my password tomorrow?`
- `Add a task to enable 2FA`
- `Show my tasks` / `Complete task 1` / `Delete task 1`
- `Start the quiz`
- `Show activity log` / `What have you done for me?` then `Show more`
- `I'm worried about online scams` / `Give me a phishing tip` / `Another tip`

## Project structure
```
App.xaml, MainWindow.xaml(.cs)       GUI: chat, Tasks, Quiz, Activity Log tabs
Models/                              User, Topic, TaskItem, ActivityEntry, QuizQuestion, Sentiment
Services/                            Chatbot (+ Chatbot.Tasks), KnowledgeBase, SentimentAnalyser,
                                     NlpInterpreter, TaskRepository (MySQL), QuizService, ActivityLog
Utils/                               AudioPlayer, AsciiArt, TextMatcher
appsettings.json, database.sql       MySQL connection settings and schema
.github/workflows/dotnet.yml         CI build on every push
```

## Releases (POE needs at least 3 tags)
```
git tag -a v1.0.0 -m "Part 1: console chatbot"   <commit of Part 1>
git tag -a v2.0.0 -m "Part 2: WPF GUI, keywords, memory, sentiment"   <commit of Part 2>
git tag -a v3.0.0 -m "POE: tasks + MySQL, quiz, NLP, activity log"
git push origin --tags
```
Create a GitHub Release from each tag with short release notes.
