using CyberBot.Models;

namespace CyberBot.Services
{
    /// <summary>
    /// All of the chatbot's content, organised in lists and dictionaries so the
    /// Chatbot class contains logic only. Adding a topic = adding one Build method.
    /// </summary>
    public class KnowledgeBase
    {
        private readonly Dictionary<string, Topic> _byKey = new();

        public KnowledgeBase()
        {
            // Order matters: if two topics match equally, the earlier one wins.
            Register(BuildPhishing());
            Register(BuildPassword());
            Register(BuildMalware());
            Register(BuildFirewall());
            Register(BuildVpn());
            Register(BuildTwoFactor());
            Register(BuildScam());
            Register(BuildPrivacy());
            Register(BuildBrowsing());
        }

        public List<Topic> Topics { get; } = new();

        // ---------- General conversation (random selection) ----------

        public string[] Greetings { get; } = new[]
        {
            "Hello, {name}! I'm CyberBot. What cybersecurity question can I help you with?",
            "Hi {name}! Ready to learn something new about staying safe online?",
            "Hey {name}! Ask me about phishing, passwords, scams, privacy and more."
        };

        public string[] HowAreYou { get; } = new[]
        {
            "I'm doing great, thanks for asking! I'm ready to help you learn about cybersecurity.",
            "All systems running smoothly! What would you like to know about staying safe online?",
            "Feeling secure and ready to help! How are you doing, {name}?"
        };

        public string[] ThanksReplies { get; } = new[]
        {
            "You're welcome! Feel free to ask me another cybersecurity question.",
            "Happy to help, {name}! Stay safe out there.",
            "Anytime! Want another tip?"
        };

        public string[] DefaultReplies { get; } = new[]
        {
            "I'm not sure I understand. Can you try rephrasing?",
            "I didn't quite understand that. Could you rephrase?",
            "Hmm, I didn't catch that. Could you say it a different way?"
        };

        public string[] GeneralTips { get; } = new[]
        {
            "Keep your devices and apps updated. Updates often fix security holes attackers rely on.",
            "Use a different password for every account, and turn on two-factor authentication where you can.",
            "Pause before you click. If a message creates urgency or fear, it's worth double-checking.",
            "Back up important files regularly so you can recover from ransomware or a lost device.",
            "Only share personal details with people and organisations you have verified."
        };

        public string IdentityReply =>
            "I'm CyberBot, a cybersecurity awareness assistant. I can explain common threats and help you build safer online habits.";

        public string PurposeReply =>
            "My purpose is to help you understand phishing, passwords, malware, firewalls, VPNs, two-factor authentication, scams, privacy and safe browsing.";

        /// <summary>Descriptions for tasks, chosen by keywords in the task title (first match wins).</summary>
        public List<(string[] Keywords, string Description)> TaskTemplates { get; } = new()
        {
            (new[] { "privacy" }, "Review account privacy settings to ensure your data is protected."),
            (new[] { "2fa", "two factor", "multi factor", "mfa", "authenticator" }, "Enable two-factor authentication to add an extra layer of protection to your account."),
            (new[] { "password", "passphrase" }, "Create a strong, unique password and avoid reusing it on other accounts."),
            (new[] { "phishing", "suspicious email" }, "Learn to spot phishing emails and report anything suspicious."),
            (new[] { "backup", "back up" }, "Back up your important files so you can recover them after a lost device or ransomware attack."),
            (new[] { "update", "software", "patch" }, "Install the latest updates to fix security holes that attackers could use."),
            (new[] { "vpn" }, "Set up a trusted VPN to protect your connection on public Wi-Fi."),
            (new[] { "firewall" }, "Check that your firewall is switched on and configured correctly."),
            (new[] { "antivirus", "malware", "virus", "scan" }, "Run a security scan to check your device for malware.")
        };

        public Topic? GetTopic(string key) => _byKey.GetValueOrDefault(key);

        private void Register(Topic topic)
        {
            Topics.Add(topic);
            _byKey[topic.Key] = topic;
        }

        private static SubQuestion Sub(string answer, params string[] triggers) => new(triggers, answer);

        // ---------- Topics ----------

        private static Topic BuildPhishing() => new()
        {
            Key = "phishing",
            DisplayName = "phishing",
            Keywords = new[] { "phishing", "phish", "fake email", "suspicious email", "spoof", "smishing", "vishing", "clicked a link", "click a link" },
            Overview = new[]
            {
                "Phishing is a cyberattack where criminals pretend to be a trusted person or organisation to trick you into revealing information, clicking malicious links, opening attachments or sending money.",
                "Phishing means fake messages (email, SMS or social media) designed to trick you into giving away passwords, banking details or other sensitive information."
            },
            Simple = "Think of phishing as a stranger dressed up as your bank, asking you to hand over your keys. The message looks real, but it's a trap.",
            Tips = new()
            {
                "Be cautious of emails asking for personal information. Scammers often disguise themselves as trusted organisations.",
                "Hover over links before clicking. If the address doesn't match the organisation it claims to be from, don't click.",
                "Urgent threats like 'your account will be closed in 24 hours' are a classic phishing tactic. Pause and verify through the official website or app.",
                "Check the sender's address carefully. Attackers often swap or add a letter, such as 'paypa1' instead of 'paypal'.",
                "Never open unexpected attachments, even if they seem to come from someone you know. Confirm with the sender first."
            },
            MoreInfo = new()
            {
                "Phishing comes in several forms: email phishing, SMS phishing ('smishing'), voice phishing ('vishing') and targeted 'spear phishing' that uses details about you.",
                "Common red flags: generic greetings, spelling mistakes, mismatched links, requests for passwords or payments, and pressure to act immediately.",
                "If you suspect a phishing message, don't reply or click. Report it to your email provider or the organisation being impersonated, then delete it."
            },
            MemoryTip = "try checking the sender address and hovering over links before you click anything in an email.",
            SubQuestions = new()
            {
                Sub("To protect yourself from phishing, don't click unexpected links or attachments, check the sender carefully, avoid entering passwords through suspicious links, and verify urgent requests using a trusted method.",
                    "how can i prevent", "how do i prevent", "how can i avoid", "how do i avoid", "protect myself", "stay safe", "stop phishing"),
                Sub("Common phishing signs include unexpected messages, urgent requests, suspicious links, requests for passwords or payment information, unusual sender addresses and unexpected attachments.",
                    "example", "look like", "signs", "warning signs", "red flag"),
                Sub("If you clicked a suspicious link, don't enter any information. Close the page, run a security scan, change any password you may have entered, enable two-factor authentication and report the message.",
                    "clicked", "click a link", "opened a phishing")
            }
        };

        private static Topic BuildPassword() => new()
        {
            Key = "password",
            DisplayName = "password safety",
            Keywords = new[] { "password", "passphrase", "passcode", "credentials" },
            Overview = new[]
            {
                "A strong password is long, unique and difficult to guess. It should not contain easily available personal information and should be different for every account.",
                "Password safety means using long, unique passwords or passphrases, never sharing them, and storing them securely, ideally in a password manager."
            },
            Simple = "A password is like the key to your front door. If it's short or reused, it's easy to copy and use everywhere. Make each key long, different and hard to guess.",
            Tips = new()
            {
                "Make sure to use strong, unique passwords for each account. Avoid using personal details in your passwords.",
                "Try a passphrase: four or five random words strung together are long, memorable and hard to crack.",
                "Use a password manager to generate and store unique passwords so you don't have to remember them all.",
                "Never share your password with anyone, not even someone claiming to be from IT support or your bank.",
                "If you suspect a password has been exposed in a breach, change it straight away and turn on two-factor authentication."
            },
            MoreInfo = new()
            {
                "Length matters most. A long passphrase is far harder to crack than a short 'complex' password like 'P@ssw0rd1'.",
                "Reusing passwords is risky because of 'credential stuffing': attackers take leaked passwords from one site and try them on others.",
                "You can check whether your email has appeared in a data breach using a trusted service such as Have I Been Pwned, then change any affected passwords."
            },
            MemoryTip = "check that each of your important accounts has its own unique password.",
            SubQuestions = new()
            {
                Sub("Create a long, unique password or passphrase for each account. Avoid names, birthdays and common words. A password manager can help you generate and store them.",
                    "how can i make", "how do i make", "create a strong", "strong password", "secure password", "what makes"),
                Sub("A password manager securely stores your passwords and can generate strong, unique ones for every account, so you never need to reuse them.",
                    "password manager", "manage passwords"),
                Sub("Avoid reusing passwords. If one account is compromised, attackers will try the same password on your other accounts.",
                    "reuse", "same password", "different accounts")
            }
        };

        private static Topic BuildMalware() => new()
        {
            Key = "malware",
            DisplayName = "malware",
            Keywords = new[] { "malware", "virus", "antivirus", "ransomware", "spyware", "trojan" },
            Overview = new[]
            {
                "Malware means malicious software. It is designed to damage systems, steal information, disrupt operations or gain unauthorised access.",
                "Malware is any software built to harm you or your device, including viruses, ransomware, spyware and trojans."
            },
            Simple = "Malware is harmful software that sneaks onto your device, like a burglar pretending to be a delivery driver to get inside.",
            Tips = new()
            {
                "Keep your operating system and apps updated. Updates patch the holes malware uses to get in.",
                "Only download software from official websites and app stores, and avoid cracked or pirated programs.",
                "Don't open unexpected attachments or run files you weren't expecting, even from contacts.",
                "Use reputable security software and run regular scans.",
                "Back up important files regularly. If ransomware strikes, you can restore instead of paying."
            },
            MoreInfo = new()
            {
                "Types of malware include viruses (spread by attaching to files), worms (spread on their own), trojans (disguised as useful software), spyware and ransomware.",
                "Malware often arrives through phishing emails, malicious downloads, infected USB drives and compromised websites.",
                "Signs of infection include a sudden slowdown, unexpected pop-ups, programs you didn't install and files that won't open."
            },
            MemoryTip = "keep your devices updated and only install software from trusted sources.",
            SubQuestions = new()
            {
                Sub("To reduce malware risk, keep your operating system and applications updated, use reputable security software, avoid suspicious downloads and attachments, and only install software from trusted sources.",
                    "how can i prevent", "how do i prevent", "protect against", "avoid malware"),
                Sub("Ransomware is malware that encrypts your files and demands payment. Regular backups, software updates and careful handling of suspicious files can reduce the risk.",
                    "ransomware"),
                Sub("A computer virus is malicious software that can reproduce or spread by attaching itself to files or programs.",
                    "virus"),
                Sub("Spyware is malicious software designed to secretly monitor your activity or collect information from your device.",
                    "spyware")
            }
        };

        private static Topic BuildFirewall() => new()
        {
            Key = "firewall",
            DisplayName = "firewalls",
            Keywords = new[] { "firewall" },
            Overview = new[]
            {
                "A firewall is a security system that monitors and controls network traffic based on predefined rules. It allows legitimate connections and blocks unauthorised traffic.",
                "A firewall acts as a barrier between your device or network and the internet, filtering what is allowed in and out."
            },
            Simple = "A firewall is like a security guard at the door of your network, checking who is allowed in and out.",
            Tips = new()
            {
                "Make sure your device's built-in firewall is switched on. Windows and macOS both include one.",
                "Don't turn your firewall off to 'fix' a connection problem. Allow only the specific app that needs access.",
                "Keep your router's firewall enabled and its firmware up to date.",
                "Treat a firewall as one layer of protection. Use it alongside updates, strong passwords and security software."
            },
            MoreInfo = new()
            {
                "Firewalls can be software (on your computer) or hardware (in your router or a dedicated device).",
                "They apply rules based on things like IP addresses, ports and applications to decide whether to allow or block traffic.",
                "A firewall can't stop you from installing malware yourself or from being tricked by phishing, which is why other defences matter too."
            },
            MemoryTip = "check that the firewall on your computer and router is switched on.",
            SubQuestions = new()
            {
                Sub("A firewall monitors network traffic and applies security rules to decide which connections should be allowed or blocked.",
                    "how does", "how do", "work"),
                Sub("A firewall helps prevent unauthorised network connections and reduces exposure to certain network-based threats.",
                    "why do i need", "why use", "important")
            }
        };

        private static Topic BuildVpn() => new()
        {
            Key = "vpn",
            DisplayName = "VPNs",
            Keywords = new[] { "vpn", "virtual private network" },
            Overview = new[]
            {
                "A VPN, or Virtual Private Network, creates an encrypted connection between your device and a VPN server. It can help protect your traffic from local network observers.",
                "A VPN routes your internet traffic through an encrypted tunnel, which improves privacy on networks you don't control."
            },
            Simple = "A VPN is like a private tunnel for your internet traffic, so other people on the same network can't peek inside.",
            Tips = new()
            {
                "Use a reputable VPN on public Wi-Fi such as cafes, airports and hotels.",
                "Be careful with free VPNs. Some log or sell your browsing data.",
                "A VPN doesn't replace good habits. You still need strong passwords and caution with links.",
                "Choose a provider with a clear no-logs policy and independent audits."
            },
            MoreInfo = new()
            {
                "A VPN encrypts the traffic between your device and the VPN server, and hides your real IP address from the sites you visit.",
                "Your VPN provider can still see your traffic, so it matters that you choose one you trust.",
                "A VPN does not protect you from malware or phishing. It protects the connection, not your choices."
            },
            MemoryTip = "consider using a trusted VPN whenever you connect to public Wi-Fi.",
            SubQuestions = new()
            {
                Sub("A VPN, or Virtual Private Network, creates an encrypted connection between your device and a VPN server. It can help protect your traffic from local network observers.",
                    "what is", "define", "meaning", "explain"),
                Sub("A reputable VPN adds protection on public Wi-Fi by encrypting traffic between your device and the VPN server.",
                    "public wifi", "public wi fi", "public network"),
                Sub("A VPN can improve privacy, but it does not make you completely anonymous online.",
                    "anonymous", "completely private", "hide me")
            }
        };

        private static Topic BuildTwoFactor() => new()
        {
            Key = "2fa",
            DisplayName = "two-factor authentication",
            Keywords = new[] { "2fa", "2 factor", "two factor", "multi factor", "multifactor", "mfa", "authenticator" },
            Overview = new[]
            {
                "Two-factor authentication, or 2FA, is a security method that requires an additional verification step beyond your password.",
                "2FA means proving who you are in two different ways, such as a password plus a code from an app on your phone."
            },
            Simple = "2FA is like needing both a key and a fingerprint to open a door. A stolen password alone isn't enough.",
            Tips = new()
            {
                "Turn on two-factor authentication for your email, banking and social media accounts first.",
                "Prefer an authenticator app or security key over SMS codes where possible. They're harder to intercept.",
                "Save your backup codes somewhere safe in case you lose your phone.",
                "Never share verification codes with anyone, even if they claim to be from the company."
            },
            MoreInfo = new()
            {
                "The three kinds of factor are something you know (a password), something you have (a phone or key) and something you are (a fingerprint).",
                "SIM-swap attacks can intercept SMS codes, which is why app-based codes are stronger.",
                "Even with 2FA, fake login pages can trick you into typing your code, so always check the web address first."
            },
            MemoryTip = "make sure two-factor authentication is switched on for your email account.",
            SubQuestions = new()
            {
                Sub("Two-factor authentication adds a verification step after your password, making it much harder for someone to access your account with a stolen password alone.",
                    "why", "important", "should i use", "should i enable"),
                Sub("Two-factor authentication requires two different types of verification, such as a password and a code from an authenticator app.",
                    "how does", "how do", "work")
            }
        };

        private static Topic BuildScam() => new()
        {
            Key = "scam",
            DisplayName = "scams",
            Keywords = new[] { "scam", "fraud", "fake website", "too good to be true" },
            Overview = new[]
            {
                "A scam is a deliberate trick to get your money or personal details, usually by pretending to be someone trustworthy and rushing you into acting.",
                "Online scams include fake prizes, romance scams, investment schemes, fake job offers and criminals impersonating banks or delivery companies."
            },
            Simple = "A scam is a trick to get your money or details by pretending to be something trustworthy, usually while rushing you.",
            Tips = new()
            {
                "If an offer sounds too good to be true, it almost always is. Pause and research before you pay or share details.",
                "Scammers create urgency. Take your time and talk to someone you trust before acting.",
                "Never pay using gift cards, crypto or wire transfers because a stranger asked. Legitimate organisations don't demand these.",
                "Verify by contacting the organisation using the number on its official website, not the one in the message.",
                "Be wary of anyone who moves the conversation off an official platform, for example to WhatsApp."
            },
            MoreInfo = new()
            {
                "Common scams: fake prizes and lotteries, romance scams, investment scams, fake job offers and impersonation of banks or delivery companies.",
                "Scammers often ask you to keep things secret or act immediately, so they can stop you checking the facts.",
                "If you've been scammed, contact your bank immediately, keep evidence such as screenshots, change affected passwords and report it to the police."
            },
            MemoryTip = "take a moment to verify any unexpected request for money or personal details.",
            SubQuestions = new()
            {
                Sub("If you've been scammed, contact your bank straight away, keep all evidence, change any affected passwords and report it to the police. Acting quickly gives you the best chance of limiting the damage.",
                    "been scammed", "got scammed", "lost money", "fell for")
            }
        };

        private static Topic BuildPrivacy() => new()
        {
            Key = "privacy",
            DisplayName = "privacy",
            Keywords = new[] { "privacy", "personal information", "personal data", "personal details", "oversharing", "tracking", "social media" },
            Overview = new[]
            {
                "Online privacy is about controlling who can see your personal information and how it is used.",
                "Privacy means deciding what you share, with whom, and making sure apps and websites only collect what they genuinely need."
            },
            Simple = "Online privacy is deciding who gets to see your personal information, and making sure it's only the people you choose.",
            Tips = new()
            {
                "Review the privacy settings on your social media and email accounts so only people you trust can see your details.",
                "Think before you share. Details like your birthday, address and ID number can be used for identity theft.",
                "Check app permissions regularly and remove access to location, camera and contacts where it isn't needed.",
                "Avoid logging in to sensitive accounts on public Wi-Fi unless you are using a VPN.",
                "In South Africa, the POPIA law gives you rights over how organisations collect and use your personal information."
            },
            MoreInfo = new()
            {
                "Apps and advertisers often track your activity across websites. Browser tracking protection and limiting app permissions reduce this.",
                "Oversharing on social media can give scammers the answers to security questions or material for convincing impersonation.",
                "Using separate email addresses for important accounts and for newsletters limits how much a single leak can expose."
            },
            MemoryTip = "you might want to review the security settings on your accounts.",
            SubQuestions = new()
        };

        private static Topic BuildBrowsing() => new()
        {
            Key = "browsing",
            DisplayName = "safe browsing",
            Keywords = new[] { "safe browsing", "browsing", "browser", "https", "suspicious link", "unsafe link", "suspicious website", "unsafe website", "public wifi", "public wi fi", "padlock", "pop up" },
            Overview = new[]
            {
                "Safe browsing means making sure a website is genuine and secure before you trust it with any information.",
                "Safe browsing is the habit of checking links, website addresses and downloads before you click, type or install anything."
            },
            Simple = "Safe browsing means checking that a website is genuine and secure before you trust it with anything.",
            Tips = new()
            {
                "Look for 'https' and a padlock before entering personal details, but remember scammers can use them too. Always check the domain name.",
                "Ignore pop-ups and 'your device is infected' warnings. Close the tab instead of clicking.",
                "Keep your browser and extensions updated, and only install extensions from official stores.",
                "For banking, type the address yourself or use a bookmark rather than following links in messages.",
                "Download software only from the official website or app store."
            },
            MoreInfo = new()
            {
                "HTTPS encrypts the data between your browser and a site, but it does not prove that the site is trustworthy.",
                "Lookalike domains such as 'amaz0n.com' are used to imitate real sites. Read the address carefully.",
                "Private or incognito mode doesn't hide your activity from your network, your employer or the websites you visit."
            },
            MemoryTip = "make sure your browser is up to date and double-check website addresses before logging in.",
            SubQuestions = new()
            {
                Sub("On public Wi-Fi, avoid banking or other sensitive logins unless you use a VPN, and forget the network when you're done.",
                    "public wifi", "public wi fi")
            }
        };
    }
}
