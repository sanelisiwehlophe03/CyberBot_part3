using CyberBot.Models;

namespace CyberBot.Services
{
    /// <summary>
    /// The cybersecurity mini-game. Holds the question bank (a List), shuffles it for each game,
    /// tracks the score and gives final feedback. The GUI shows one question at a time.
    /// </summary>
    public class QuizService
    {
        private readonly Random _random = new();
        private readonly List<QuizQuestion> _bank = BuildQuestions();
        private List<QuizQuestion> _questions = new();

        public int Index { get; private set; }
        public int Score { get; private set; }
        public int Total => _questions.Count;
        public int QuestionCount => _bank.Count;
        public QuizQuestion Current => _questions[Index];
        public bool IsLast => Index >= _questions.Count - 1;

        /// <summary>Starts a new game with the questions in a random order.</summary>
        public void Start()
        {
            _questions = _bank.OrderBy(_ => _random.Next()).ToList();
            Index = 0;
            Score = 0;
        }

        /// <summary>Checks the chosen option for the current question and updates the score.</summary>
        public bool Answer(int choice)
        {
            bool correct = choice == Current.CorrectIndex;

            if (correct)
            {
                Score++;
            }

            return correct;
        }

        /// <summary>Moves to the next question. Returns false when the quiz is finished.</summary>
        public bool Advance()
        {
            if (IsLast)
            {
                return false;
            }

            Index++;
            return true;
        }

        public string GetFinalFeedback()
        {
            double percentage = Total == 0 ? 0 : (double)Score / Total;

            if (percentage >= 0.8)
            {
                return "Great job! You're a cybersecurity pro!";
            }

            if (percentage >= 0.5)
            {
                return "Good effort! A little more practice and you'll be a pro. Try the topics in the chat to learn more.";
            }

            return "Keep learning to stay safe online! Ask me about phishing, passwords and scams, then try the quiz again.";
        }

        private static List<QuizQuestion> BuildQuestions()
        {
            string[] trueFalse = { "True", "False" };

            return new List<QuizQuestion>
            {
                new("What should you do if you receive an email asking for your password?",
                    new[] { "Reply with your password", "Delete the email", "Report the email as phishing", "Ignore it" },
                    2, "Reporting phishing emails helps prevent scams. Legitimate companies never ask for your password by email."),

                new("It is safe to use the same password on all of your accounts.",
                    trueFalse, 1, "If one site is breached, attackers try the same password everywhere else. Use a unique password for each account."),

                new("Which of these is the strongest password?",
                    new[] { "password123", "Summer2024", "Tiger-Lamp-Rain-Boat-47", "your birthday" },
                    2, "Long passphrases made of random words are much harder to crack than short, predictable passwords."),

                new("Public Wi-Fi is always safe for online banking.",
                    trueFalse, 1, "Public networks can be monitored or faked. Avoid banking on them, or use a trusted VPN."),

                new("What does the padlock and 'https' in your browser's address bar tell you?",
                    new[] { "The website is guaranteed to be trustworthy", "Your connection to the site is encrypted", "The site is approved by the government", "The site has no adverts" },
                    1, "HTTPS encrypts the connection, but scam websites can use it too. Always check the domain name as well."),

                new("Someone phones claiming to be your bank and asks for the OTP just sent to your phone. What do you do?",
                    new[] { "Read out the OTP", "Hang up and call the bank on its official number", "Ask them to email you instead", "Give them only half of the code" },
                    1, "This is social engineering. Banks never ask for your OTP. Hang up and call the number on your card."),

                new("Two-factor authentication still protects your account if your password is stolen.",
                    trueFalse, 0, "The attacker also needs your second factor, such as a code from your phone."),

                new("What is ransomware?",
                    new[] { "Software that speeds up your computer", "Malware that locks your files and demands payment", "A type of firewall", "A password manager" },
                    1, "Regular backups and software updates are your best defence against ransomware."),

                new("Which of these is a common sign of a phishing email?",
                    new[] { "A greeting using your full name", "Urgent threats and a link that doesn't match the sender", "A message from a colleague you were expecting", "A plain-text message" },
                    1, "Urgency and mismatched links are classic phishing tactics. Pause and verify before you click."),

                new("Software updates are not important if you have antivirus installed.",
                    trueFalse, 1, "Updates fix security holes that attackers actively exploit. Antivirus is only one layer of protection."),

                new("What is social engineering?",
                    new[] { "Building secure networks", "Manipulating people into giving up information or access", "Encrypting your data", "Writing secure code" },
                    1, "Attackers target people rather than technology. Be wary of anyone creating urgency or pressure."),

                new("You find a USB drive in the parking lot. What is the safest action?",
                    new[] { "Plug it in to find the owner", "Hand it to IT or security without plugging it in", "Format it and use it", "Plug it in on an old computer" },
                    1, "Attackers deliberately leave infected USB drives. Never plug in a device you don't trust."),

                new("A VPN makes you completely anonymous online.",
                    trueFalse, 1, "A VPN protects your connection, but your VPN provider and the sites you log in to can still identify you.")
            };
        }
    }
}
