// MyFirstProject/UserProfileManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MojaveLibMk1
{
    internal static class UserProfileManager
    {
        public static async Task ShowUserProfilePageAsync()
        {
            bool inProfile = true;
            while (inProfile)
            {
                Console.Clear();
                RenderProfilePage();

                UIHelper.WriteLineCentered("\nOptions: [U]pdate Username, [B]ack to Main Menu");
                var choice = UIHelper.ReadInputCentered("> ")?.ToLowerInvariant();

                switch (choice)
                {
                    case "u":
                        UpdateUsername();
                        break;
                    case "b":
                        inProfile = false;
                        break;
                }
            }
        }

        private static void RenderProfilePage()
        {
            var settings = SettingsManager.Current;
            var profileArt = SettingsManager.AsciiArtProfiles[settings.ProfilePictureIndex];

            // --- Header ---
            UIHelper.WriteHeader($"{settings.Username}'s Profile");

            // --- Profile Picture ---
            UIHelper.WriteLineCentered("");
            foreach (var line in profileArt)
            {
                UIHelper.WriteLineCentered(line);
            }
            UIHelper.WriteLineCentered("");

            // --- Core Stats ---
            var savedEntries = EntryManager.LoadEntries();
            var bookmarkedBooks = OnlineBookManager.LoadBookmarkedBooks();
            var accountAge = DateTime.Now - settings.AccountCreationDate;

            UIHelper.WriteLineCentered($"Journal Entries: {savedEntries.Count}");
            UIHelper.WriteLineCentered($"Bookmarked Books: {bookmarkedBooks.Count}");
            UIHelper.WriteLineCentered($"Online Books Opened: {settings.OnlineBooksOpenedCount}");
            
            UIHelper.WriteLineCentered($"\nAccount Age: {accountAge.Days} days");
            if (!string.IsNullOrEmpty(settings.LastReadBookTitle))
            {
                UIHelper.WriteLineCentered($"Last Read: {settings.LastReadBookTitle}");
            }

            // --- Insights ---
            UIHelper.WriteLineCentered("\n--- Reading Habits ---");
            DisplayTopItems(savedEntries, "Genre");
            DisplayTopItems(savedEntries, "Author");
        }

        private static void DisplayTopItems(List<string> entries, string metadataType)
        {
            var counts = entries
                .Select(entry =>
                {
                    EntryManager.ExtractMetadata(entry, out _, out _, out var author, out var genre);
                    return metadataType == "Genre" ? genre : author;
                })
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(group => new { Name = group.Key, Count = group.Count() })
                .OrderByDescending(x => x.Count)
                .Take(3)
                .ToList();

            if (counts.Any())
            {
                UIHelper.SetColor(ConsoleColor.Cyan);
                UIHelper.WriteLineCentered($"\nTop {metadataType}s:");
                UIHelper.ResetColor();
                foreach (var item in counts)
                {
                    UIHelper.WriteLineCentered($"- {item.Name} ({item.Count} entries)");
                }
            }
        }

        private static void UpdateUsername()
        {
            string newUsername = UIHelper.ReadInputCentered("\nEnter new username: ");
            if (!string.IsNullOrWhiteSpace(newUsername))
            {
                SettingsManager.Current.Username = newUsername;
                SettingsManager.Save();
                UIHelper.WriteLineCentered("\nUsername updated!");
            }
            else
            {
                UIHelper.WriteLineCentered("\nUsername cannot be empty.");
            }
            UIHelper.WriteLineCentered("Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}