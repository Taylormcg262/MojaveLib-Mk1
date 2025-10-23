using System;
using System.Threading.Tasks;

namespace MojaveLibMk1
{
 
    // Application entry point and top-level console workflow.
    // Keeps only orchestration here and delegates work to feature classes.
  
    internal class Program
    {
        private const int DefaultWindowWidth = 100;
        private const int DefaultWindowHeight = 30;

        private static string[] GetMenuItems() => new[]
        {
            "Journal",
            "Online Sourcing",
            $"{SettingsManager.Current.Username}'s Profile",
            "Settings",
            "Exit"
        };

        private static readonly string[] Banners = new[]
        {
            "Journal Menu",
            "Online Sourcing",
            "User Profile",
            "Settings"
        };

        static async Task Main(string[] args)
        {
            // Load settings early and set encoding when needed
            SettingsManager.Load();
            if (!SettingsManager.Current.UseAsciiUi)
            {
                try { Console.OutputEncoding = System.Text.Encoding.UTF8; Console.InputEncoding = System.Text.Encoding.UTF8; } catch { }
            }

            InitializeConsole();
            await ShowSplashScreenAndLoginAsync();

            bool running = true;
            while (running)
            {
                Console.Clear();
                ShowIntroduction();

                int choice = ShowMenuAndGetChoice();
                Console.Clear();
                
                switch (choice)
                {
                    case 1:
                        await ShowJournalMenuAsync();
                        break;
                    case 2:
                        await ShowOnlineSourcingMenuAsync();
                        break;
                    case 3:
                        await UserProfileManager.ShowUserProfilePageAsync();
                        break;
                    case 4:
                        await SettingsManager.ShowSettingsMenuAsync();
                        break;
                    case 5:
                        running = false;
                        UIHelper.SetColor(ConsoleColor.Magenta);
                        UIHelper.WriteLineCentered("\nThank you for using Book Entry Journal!");
                        UIHelper.WriteLineCentered("Have a wonderful day!\n");
                        UIHelper.ResetColor();
                        break;
                    case -1: // Dev menu or invalid choice
                        continue;
                }
            }
        }

        private static async Task ShowSplashScreenAndLoginAsync()
        {
            string[] art = new[]
            {
                @"███╗   ███╗ ██████╗       ██╗ █████╗ ██╗   ██╗███████╗   ██╗     ██╗██████╗ ",
                @"████╗ ████║██╔═══██╗      ██║██╔══██╗██║   ██║██╔════╝   ██║     ██║██╔══██╗",
                @"██╔████╔██║██║   ██║      ██║███████║██║   ██║█████╗     ██║     ██║██████╔╝",
                @"██║╚██╔╝██║██║   ██║██║   ██║██╔══██║██║   ██║██╔══╝     ██║     ██║██╔══██╗",
                @"██║ ╚═╝ ██║╚██████╔╝╚██████╔╝██║  ██║╚██████╔╝███████╗   ███████╗██║██████╔╝",
                @"╚═╝     ╚═╝ ╚═════╝  ╚═════╝ ╚═╝  ╚═╝ ╚═════╝ ╚══════╝   ╚══════╝╚═╝╚═════╝ "
            };

            Console.Clear();
            UIHelper.SetColor(ConsoleColor.DarkYellow);

            int top = (Console.WindowHeight > art.Length + 5) ? (Console.WindowHeight - art.Length - 5) / 2 : 0;
            Console.CursorTop = top;

            foreach (var line in art)
            {
                UIHelper.WriteLineCentered(line);
            }
            UIHelper.ResetColor();
            Console.WriteLine();

            if (string.IsNullOrEmpty(SettingsManager.Current.PasswordHash))
            {
                // First-time setup
                CreateUserAndLogin();
            }
            else
            {
                // Subsequent login
                Login();
            }
            
            Console.Clear();
        }

        private static void CreateUserAndLogin()
        {
            // Username creation
            while (true)
            {
                string username = UIHelper.ReadInputCentered("Create a username: ");
                if (!string.IsNullOrWhiteSpace(username))
                {
                    SettingsManager.Current.Username = username;
                    SettingsManager.Current.AccountCreationDate = DateTime.Now;
                    break;
                }
                CenterAlignCursor("Username cannot be empty. Please try again.", true);
                Console.ReadKey(true);
                Console.Clear();
            }

            // Password creation
            string password;
            while (true)
            {
                CenterAlignCursor("Create a password (5-11 characters): ");
                password = ReadPassword();
                if (password.Length > 4 && password.Length < 12)
                {
                    CenterAlignCursor("Confirm password: ");
                    string confirmation = ReadPassword();
                    if (password == confirmation)
                    {
                        SettingsManager.Current.PasswordHash = SettingsManager.ComputeSha256Hash(password);
                        SettingsManager.Save();
                        CenterAlignCursor("Account created successfully. Press any key to continue...");
                        Console.ReadKey(true);
                        break;
                    }
                    else
                    {
                        CenterAlignCursor("Passwords do not match. Please try again.", true);
                        Console.ReadKey(true);
                    }
                }
                else
                {
                    CenterAlignCursor("Password must be between 5 and 11 characters. Please try again.", true);
                    Console.ReadKey(true);
                }
                Console.Clear(); // Clear screen for re-attempt
            }
        }

        private static void Login()
        {
            while (true)
            {
                CenterAlignCursor($"Enter password for {SettingsManager.Current.Username}: ");
                string password = ReadPassword();
                string hash = SettingsManager.ComputeSha256Hash(password);
                if (hash == SettingsManager.Current.PasswordHash)
                {
                    CenterAlignCursor("Login successful. Press any key to continue...");
                    Console.ReadKey(true);
                    break;
                }
                else
                {
                    CenterAlignCursor("Incorrect password. Please try again.", true);
                    Console.ReadKey(true);
                    Console.Clear(); // Clear screen for re-attempt
                }
            }
        }

        private static string ReadPassword()
        {
            var pass = new System.Text.StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    pass.Remove(pass.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    pass.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            Console.WriteLine();
            return pass.ToString();
        }

        private static void CenterAlignCursor(string text, bool isError = false)
        {
            if (isError) UIHelper.SetColor(ConsoleColor.Red);
            else UIHelper.SetColor(ConsoleColor.Gray);

            UIHelper.WriteCentered(text);
            
            if(isError) UIHelper.ResetColor();
        }

        private static async Task ShowJournalMenuAsync()
        {
            bool inJournalMenu = true;
            while(inJournalMenu)
            {
                Console.Clear();
                ShowBanner(1); // Journal Menu
                UIHelper.WriteLineCentered("1. Write a new book entry");
                UIHelper.WriteLineCentered("2. Manage Book Entries");
                UIHelper.WriteLineCentered("3. Read a Book Entry");
                UIHelper.WriteLineCentered("4. Back to Main Menu");

                int choice = ReadChoice(1, 4, "\nEnter your choice: ");
                Console.Clear();

                switch (choice)
                {
                    case 1:
                        ShowBanner(1);
                        WriteNewEntry();
                        break;
                    case 2:
                        ShowBanner(1);
                        ManageEntries();
                        break;
                    case 3:
                        ShowBanner(1);
                        EntryManager.ReadEntry();
                        WaitForKeyReturn();
                        break;
                    case 4:
                        inJournalMenu = false;
                        break;
                }
            }
        }

        private static async Task ShowOnlineSourcingMenuAsync()
        {
            bool inOnlineMenu = true;
            while(inOnlineMenu)
            {
                Console.Clear();
                ShowBanner(2); // Online Sourcing
                UIHelper.WriteLineCentered("1. Online Library");
                UIHelper.WriteLineCentered("2. AI Topic Explorer");
                UIHelper.WriteLineCentered("3. Back to Main Menu");

                int choice = ReadChoice(1, 3, "\nEnter your choice: ");
                Console.Clear();

                switch (choice)
                {
                    case 1:
                        await OnlineBookManager.OnlineBooksMenuAsync(ShowIntroduction);
                        break;
                    case 2:
                        await AiAssistant.ShowAiTopicExplorerAsync();
                        break;
                    case 3:
                        inOnlineMenu = false;
                        break;
                }
            }
        }

        private static void InitializeConsole()
        {
            Console.Title = "Book Entry Journal";
            int width = SettingsManager.Current.WindowWidth > 0 ? SettingsManager.Current.WindowWidth : DefaultWindowWidth;
            int height = SettingsManager.Current.WindowHeight > 0 ? SettingsManager.Current.WindowHeight : DefaultWindowHeight;
            try
            {
                Console.SetWindowSize(Math.Min(width, Console.LargestWindowWidth), Math.Min(height, Console.LargestWindowHeight));
                Console.SetBufferSize(Math.Min(width, Console.LargestWindowWidth), Math.Min(height, Console.LargestWindowHeight));
            }
            catch { }
        }

        private static void ShowIntroduction()
        {
            UIHelper.SetColor(ConsoleColor.Cyan);
            UIHelper.WriteHeader("Welcome to Book Entry Journal!");
            UIHelper.ResetColor();
            UIHelper.WriteLineCentered("Your personal space to write, manage, and explore book entries and online libraries.");
            UIHelper.WriteLineCentered("Tip: Use the menu below to navigate. Entries are saved automatically.\n");
        }

        private static void ShowBanner(int choice)
        {
            if (choice >= 1 && choice <= Banners.Length)
            {
                UIHelper.WriteHeader(Banners[choice - 1]);
            }
        }

        private static void WriteHeader(string title)
        {
            // This method is now replaced by UIHelper.WriteHeader
            // but we keep it to avoid breaking changes in other files that might call it.
            // For new code, UIHelper.WriteHeader should be used.
            UIHelper.WriteHeader(title);
        }

        private static int ShowMenuAndGetChoice()
        {
            UIHelper.SetColor(ConsoleColor.Magenta);
            UIHelper.WriteHeader("Main Menu");
            UIHelper.ResetColor();

            var menuItems = GetMenuItems();
            for (int i = 0; i < menuItems.Length; i++)
            {
                UIHelper.WriteLineCentered($"{i + 1}. {menuItems[i]}");
            }

            var input = UIHelper.ReadInputCentered($"\nEnter your choice (1-{menuItems.Length}): ");

            if (input.Equals("letmein", StringComparison.OrdinalIgnoreCase))
            {
                ShowDevMenu();
                return -1; // Special value to re-display main menu
            }

            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= menuItems.Length)
            {
                return choice;
            }

            UIHelper.SetColor(ConsoleColor.Red);
            UIHelper.WriteLineCentered("\nInvalid choice, please try again.\n");
            UIHelper.ResetColor();
            WaitForKeyReturn("Press any key to continue...");
            return -1; // Special value to re-display main menu
        }

        private static void ShowDevMenu()
        {
            Console.Clear();
            UIHelper.WriteHeader("Developer Menu");
            UIHelper.WriteLineCentered("1. Reset to First Time Use");
            UIHelper.WriteLineCentered("2. Back to Main Menu");

            var choice = ReadChoice(1, 2, "\nEnter your choice: ");
            if (choice == 1)
            {
                SettingsManager.ResetToDefaults();
                UIHelper.WriteLineCentered("\nApplication has been reset. Please restart the application.");
                Console.ReadKey(true);
                Environment.Exit(0);
            }
        }

        private static int ReadChoice(int min, int max, string prompt)
        {
            Console.Write(prompt);
            while (true)
            {
                string input = Console.ReadLine();
                int choice;
                if (!int.TryParse(input, out choice) || choice < min || choice > max)
                {
                    UIHelper.SetColor(ConsoleColor.Red);
                    UIHelper.WriteLineCentered("\nInvalid choice, please try again.\n");
                    UIHelper.ResetColor();
                    Console.Write(prompt);
                }
                else
                {
                    return choice;
                }
            }
        }

        private static void WaitForKeyReturn(string message = "\nPress any key to return to main menu...")
        {
            UIHelper.WriteLineCentered(message);
            Console.ReadKey(true);
        }

        private static void WriteNewEntry()
        {
            UIHelper.SetColor(ConsoleColor.Cyan);
            UIHelper.WriteLineCentered("\nLet's create a new book entry!");
            UIHelper.ResetColor();

            string title = UIHelper.ReadInputCentered("\n What is the title of your book? ");

            string entryType = PromptEntryType();
            PromptOptionalInfo(out var description, out var author, out var genre);

            PrintSelectedMeta(title, entryType);

            string book = CaptureMultilineEntry(
                intro: "Start typing your book entry. Type '>end' on a new line to finish.\n");

            book = ConfirmAndPossiblyEdit(book);

            string entry = BuildEntry(title, entryType, description, author, genre, book);
            EntryManager.SaveNewEntry(title, entry);

            UIHelper.SetColor(ConsoleColor.Green);
            UIHelper.WriteLineCentered("\nEntry saved!\n");
            UIHelper.ResetColor();
            WaitForKeyReturn();
        }

        private static string PromptEntryType()
        {
            while (true)
            {
                UIHelper.SetColor(ConsoleColor.Yellow);
                UIHelper.WriteLineCentered("\nSelect entry type:");
                UIHelper.ResetColor();
                UIHelper.WriteLineCentered("1. Book   2. Novel   3. Research   4. Newspaper   5. Magazine");
                UIHelper.WriteLineCentered("6. Article   7. Essay   8. Journal   9. Scope   10. Other");
                string opt = UIHelper.ReadInputCentered("Enter your choice (1-10): ");
                switch (opt)
                {
                    case "1": return "Book";
                    case "2": return "Novel";
                    case "3": return "Research";
                    case "4": return "Newspaper";
                    case "5": return "Magazine";
                    case "6": return "Article";
                    case "7": return "Essay";
                    case "8": return "Journal";
                    case "9": return "Scope";
                    case "10":
                        return UIHelper.ReadInputCentered("Enter custom type: ");
                    default:
                        UIHelper.SetColor(ConsoleColor.Red);
                        UIHelper.WriteLineCentered("\nInvalid choice. Try again.\n");
                        UIHelper.ResetColor();
                        break;
                }
            }
        }

        private static void PromptOptionalInfo(out string description, out string author, out string genre)
        {
            description = string.Empty;
            author = string.Empty;
            genre = string.Empty;

            while (true)
            {
                UIHelper.SetColor(ConsoleColor.Yellow);
                UIHelper.WriteLineCentered("\nWould you like to add optional information?");
                UIHelper.ResetColor();
                UIHelper.WriteLineCentered("1. Add/Edit Description   2. Add/Edit Author   3. Add/Edit Genre   4. Continue to entry");
                string opt = UIHelper.ReadInputCentered("Enter your choice (1-4): ");
                switch (opt)
                {
                    case "1":
                        description = UIHelper.ReadInputCentered("\nEnter description: ");
                        break;
                    case "2":
                        author = UIHelper.ReadInputCentered("\nEnter author: ");
                        break;
                    case "3":
                        genre = PromptGenre();
                        break;
                    case "4":
                        return;
                    default:
                        UIHelper.SetColor(ConsoleColor.Red);
                        UIHelper.WriteLineCentered("\nInvalid choice. Try again.\n");
                        UIHelper.ResetColor();
                        break;
                }
            }
        }

        private static string PromptGenre()
        {
            while (true)
            {
                UIHelper.SetColor(ConsoleColor.Yellow);
                UIHelper.WriteLineCentered("\nSelect genre:");
                UIHelper.ResetColor();
                UIHelper.WriteLineCentered("1. Fiction   2. Non-Fiction   3. Mystery   4. Fantasy   5. Science Fiction");
                UIHelper.WriteLineCentered("6. Biography   7. Romance   8. Thriller   9. Historical   10. Horror");
                UIHelper.WriteLineCentered("11. Poetry   12. Drama   13. Adventure   14. Children   15. Other");
                string gopt = UIHelper.ReadInputCentered("Enter your choice (1-15): ");
                switch (gopt)
                {
                    case "1": return "Fiction";
                    case "2": return "Non-Fiction";
                    case "3": return "Mystery";
                    case "4": return "Fantasy";
                    case "5": return "Science Fiction";
                    case "6": return "Biography";
                    case "7": return "Romance";
                    case "8": return "Thriller";
                    case "9": return "Historical";
                    case "10": return "Horror";
                    case "11": return "Poetry";
                    case "12": return "Drama";
                    case "13": return "Adventure";
                    case "14": return "Children";
                    case "15":
                        return UIHelper.ReadInputCentered("Enter custom genre: ");
                    default:
                        UIHelper.SetColor(ConsoleColor.Red);
                        UIHelper.WriteLineCentered("\nInvalid choice. Try again.\n");
                        UIHelper.ResetColor();
                        break;
                }
            }
        }

        private static void PrintSelectedMeta(string title, string entryType)
        {
            UIHelper.SetColor(ConsoleColor.Cyan);
            UIHelper.WriteLineCentered($"\nBook Title: {title}");
            UIHelper.ResetColor();
        }

        private static string CaptureMultilineEntry(string intro)
        {
            UIHelper.WriteLineCentered(intro);
            var entryLines = new System.Collections.Generic.List<string>();
            while (true)
            {
                string line = Console.ReadLine();
                if (line.Trim() == ">end")
                    break;
                entryLines.Add(line);
            }
            return string.Join(Environment.NewLine, entryLines);
        }

        private static string ConfirmAndPossiblyEdit(string book)
        {
            string confirmation;
            do
            {
                UIHelper.SetColor(ConsoleColor.Yellow);
                UIHelper.WriteLineCentered("\nIs this your entry? (Yes or No)");
                UIHelper.ResetColor();
                confirmation = Console.ReadLine();

                if (confirmation.Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    UIHelper.WriteLineCentered("\nHere is your current entry:\n");
                    UIHelper.SetColor(ConsoleColor.Yellow);
                    UIHelper.WriteLineCentered(book);
                    UIHelper.ResetColor();
                    UIHelper.WriteLineCentered("\nEdit your entry below (type '>end' to finish):\n");
                    book = CaptureMultilineEntry(string.Empty);
                }
                else if (!confirmation.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    UIHelper.SetColor(ConsoleColor.Red);
                    UIHelper.WriteLineCentered("\nInvalid response. Please enter 'Yes' or 'No'.\n");
                    UIHelper.ResetColor();
                }
            }
            while (!confirmation.Equals("yes", StringComparison.OrdinalIgnoreCase));

            return book;
        }

        private static string BuildEntry(string title, string entryType, string description, string author, string genre, string book)
        {
            return
                $"Title: {title}\n" +
                $"Type: {entryType}\n" +
                (string.IsNullOrWhiteSpace(description) ? "" : $"Description: {description}\n") +
                (string.IsNullOrWhiteSpace(author) ? "" : $"Author: {author}\n") +
                (string.IsNullOrWhiteSpace(genre) ? "" : $"Genre: {genre}\n") +
                $"Entry:\n{book}\n---";
        }

        private static void ManageEntries()
        {
            if (!System.IO.File.Exists(EntryManager.filePath))
            {
                UIHelper.SetColor(ConsoleColor.Red);
                UIHelper.WriteLineCentered("\nNo previous entries found.\n");
                UIHelper.ResetColor();
                WaitForKeyReturn();
                return;
            }

            var entryBlocks = EntryManager.LoadEntries();
            if (entryBlocks.Count == 0)
            {
                UIHelper.SetColor(ConsoleColor.Red);
                UIHelper.WriteLineCentered("\nNo entries to manage.\n");
                UIHelper.ResetColor();
                WaitForKeyReturn();
                return;
            }

            EntryManager.DisplayEntryTitles(entryBlocks);

            string action = UIHelper.ReadInputCentered("Would you like to edit or delete an entry? (edit/delete/cancel)");
            if (action.Equals("edit", StringComparison.OrdinalIgnoreCase))
            {
                EntryManager.EditEntry(entryBlocks);
            }
            else if (action.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                EntryManager.DeleteEntry(entryBlocks);
            }
            else
            {
                UIHelper.WriteLineCentered("\nCancelled.\n");
            }
            WaitForKeyReturn();
        }
    }
}
