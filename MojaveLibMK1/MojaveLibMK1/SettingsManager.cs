using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MojaveLibMk1
{
    internal static class SettingsManager
    {
        private const string SettingsFile = "UserSettings.json";

        public class UserSettings
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
            public DateTime AccountCreationDate { get; set; }
            public int ProfilePictureIndex { get; set; } = 0;
            public int OnlineBooksOpenedCount { get; set; } = 0;
            public string LastReadBookTitle { get; set; }

            public bool UseColors { get; set; } = true;
            public bool UseAsciiUi { get; set; } = false;
            public int WindowWidth { get; set; } = 100;
            public int WindowHeight { get; set; } = 30;
        }

        public static UserSettings Current { get; private set; } = new UserSettings();

        public static readonly List<string[]> AsciiArtProfiles = new List<string[]>
        {
            new[] // Wise Owl
            {
                @" /\\_/\\  ",
                @" ( o.o ) ",
                @"  > ^ <  "
            },
            new[] // Bookworm
            {
                @"   / _  \ ",
                @" /- / \ -\",
                @"(o_/   \__)"
            },
            new[] // Classic Scholar
            {
                @"  .---.  ",
                @" (o)-(o) ",
                @" ( 'o' ) "
            },
            new[] // Abstract Globe
            {
                @"  ,--.   ",
                @" ( () )  ",
                @"  `--'   "
            },
            new[] // Minimalist Sun
            {
                @"   \\|/   ",
                @"  --  --  ",
                @"   /|\\   "
            }
        };

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var loaded = JsonConvert.DeserializeObject<UserSettings>(json);
                    if (loaded != null)
                        Current = loaded;
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        public static void ResetToDefaults()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    File.Delete(SettingsFile);
                }
                Current = new UserSettings(); // Reset in-memory settings
            }
            catch { }
        }

        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                var builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static async Task ShowSettingsMenuAsync()
        {
            Load();

            bool inSettings = true;
            while (inSettings)
            {
                Console.Clear();
                UIHelper.WriteHeader("Settings");
                UIHelper.WriteLineCentered("1. Change Profile Picture");
                UIHelper.WriteLineCentered("2. Toggle Color Mode (currently: " + (Current.UseColors ? "On" : "Off") + ")");
                UIHelper.WriteLineCentered("3. Toggle ASCII UI (currently: " + (Current.UseAsciiUi ? "On" : "Off") + ")");
                UIHelper.WriteLineCentered("4. Set Console Window Size (currently: " + Current.WindowWidth + " x " + Current.WindowHeight + ")");
                UIHelper.WriteLineCentered("5. Clear All Bookmarks");
                UIHelper.WriteLineCentered("6. Regenerate Recommended Books");
                UIHelper.WriteLineCentered("7. Clear Recommended Books File");
                UIHelper.WriteLineCentered("8. Reset All Settings to Defaults");
                UIHelper.WriteLineCentered("9. Back to Main Menu");

                var input = UIHelper.ReadInputCentered("\nSelect an option (1-9): ");
                switch (input)
                {
                    case "1":
                        ChangeProfilePicture();
                        break;
                    case "2":
                        Current.UseColors = !Current.UseColors;
                        Save();
                        UIHelper.WriteLineCentered("\nColor mode set to: " + (Current.UseColors ? "On" : "Off"));
                        Pause();
                        break;
                    case "3":
                        Current.UseAsciiUi = !Current.UseAsciiUi;
                        // Ensure console encoding can render Unicode when ASCII UI is off
                        if (!Current.UseAsciiUi)
                        {
                            try { Console.OutputEncoding = Encoding.UTF8; Console.InputEncoding = Encoding.UTF8; } catch { }
                        }
                        Save();
                        UIHelper.WriteLineCentered("\nASCII UI set to: " + (Current.UseAsciiUi ? "On" : "Off"));
                        Pause();
                        break;
                    case "4":
                        SetConsoleSize();
                        Save();
                        Pause();
                        break;
                    case "5":
                        OnlineBookManager.ClearAllBookmarks();
                        UIHelper.WriteLineCentered("\nAll bookmarks cleared.");
                        Pause();
                        break;
                    case "6":
                        await OnlineBookManager.GenerateRecommendedBooksFileAsync();
                        UIHelper.WriteLineCentered("\nRecommended books regenerated.");
                        Pause();
                        break;
                    case "7":
                        OnlineBookManager.ClearRecommendedBooksFile();
                        UIHelper.WriteLineCentered("\nRecommended books file cleared.");
                        Pause();
                        break;
                    case "8":
                        Current = new UserSettings();
                        Save();
                        UIHelper.WriteLineCentered("\nSettings reset to defaults.");
                        Pause();
                        break;
                    case "9":
                        inSettings = false;
                        break;
                    default:
                        UIHelper.WriteLineCentered("\nInvalid option. Please select 1-9.");
                        Pause();
                        break;
                }
            }
        }

        private static void ChangeProfilePicture()
        {
            Console.Clear();
            WriteHeader("Change Profile Picture");
            for (int i = 0; i < AsciiArtProfiles.Count; i++)
            {
                Console.WriteLine($"\n--- Option {i + 1} ---");
                foreach (var line in AsciiArtProfiles[i])
                {
                    Console.WriteLine(line);
                }
            }

            Console.Write("\nEnter your choice (1-5): ");
            if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= AsciiArtProfiles.Count)
            {
                Current.ProfilePictureIndex = choice - 1;
                Save();
                Console.WriteLine("\nProfile picture updated!");
            }
            else
            {
                Console.WriteLine("\nInvalid choice.");
            }
            Pause();
        }

        public static void SetColor(ConsoleColor color)
        {
            if (Current.UseColors)
            {
                try { Console.ForegroundColor = color; } catch { }
            }
        }

        public static void ResetColor()
        {
            if (Current.UseColors)
            {
                try { Console.ResetColor(); } catch { }
            }
        }

        public static void WriteHeader(string title)
        {
            var useAscii = Current.UseAsciiUi;
            var top = useAscii ? "+" + new string('-', 46) + "+" : "╔" + new string('═', 46) + "╗";
            var mid = useAscii ? "|" + Center(title, 46) + "|" : "║" + Center(title, 46) + "║";
            var bot = useAscii ? "+" + new string('-', 46) + "+" : "╚" + new string('═', 46) + "╝";

            SetColor(ConsoleColor.Yellow);
            Console.WriteLine(top);
            Console.WriteLine(mid);
            Console.WriteLine(bot);
            ResetColor();
        }

        private static void SetConsoleSize()
        {
            Console.Write("Enter new window width (current: " + Current.WindowWidth + "): ");
            var w = Console.ReadLine();
            Console.Write("Enter new window height (current: " + Current.WindowHeight + "): ");
            var h = Console.ReadLine();
            int wi, he;
            if (int.TryParse(w, out wi) && int.TryParse(h, out he) && wi > 0 && he > 0)
            {
                Current.WindowWidth = wi;
                Current.WindowHeight = he;
                try
                {
                    Console.SetWindowSize(Math.Min(wi, Console.LargestWindowWidth), Math.Min(he, Console.LargestWindowHeight));
                    Console.SetBufferSize(Math.Min(wi, Console.LargestWindowWidth), Math.Min(he, Console.LargestWindowHeight));
                }
                catch { }
                Console.WriteLine("\nWindow size updated.");
            }
            else
            {
                Console.WriteLine("\nInvalid size input.");
            }
        }

        private static void Pause()
        {
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }

        private static string Center(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) text = string.Empty;
            text = text.Length > width ? text.Substring(0, width) : text;
            int padLeft = (width - text.Length) / 2 + text.Length;
            return text.PadLeft(padLeft).PadRight(width);
        }
    }
}
