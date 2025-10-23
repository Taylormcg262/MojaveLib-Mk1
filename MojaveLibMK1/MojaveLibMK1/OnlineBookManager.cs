using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MojaveLibMk1
{
    internal static class OnlineBookManager
    {
        private const string BookmarksFile = "bookmarked_books.json";
        private const string RecommendedFile = "recommended_books.json";

        // Expose file paths to settings/features that need to manage files.
        public static string GetBookmarksFilePath() => BookmarksFile;
        public static string GetRecommendedFilePath() => RecommendedFile;

        // Clear all bookmarks (used by Settings page)

        public static void ClearAllBookmarks()
        {
            try
            {
                if (File.Exists(BookmarksFile))
                    File.WriteAllText(BookmarksFile, string.Empty);
            }
            catch { }
        }

        // Delete/clear the recommended books file (used by Settings page)
        public static void ClearRecommendedBooksFile()
        {
            try
            {
                if (File.Exists(RecommendedFile))
                    File.WriteAllText(RecommendedFile, string.Empty);
            }
            catch { }
        }

        // Query Google Books and return a list of basic results.
        public static async Task<List<OnlineBookResult>> SearchOnlineBooksAsync(string query, string orderBy = "relevance")
        {
            var results = new List<OnlineBookResult>();
            string url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults=10&orderBy={orderBy}";
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync(url);
                    var json = JObject.Parse(response);
                    var items = json["items"];
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var info = item["volumeInfo"];
                            results.Add(new OnlineBookResult
                            {
                                Title = info?["title"]?.ToString() ?? "No Title",
                                Url = info?["previewLink"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                }
                catch { }
            }
            return results;
        }

        // Append a simple bookmark line to the bookmarks file.
        public static void BookmarkOnlineBook(string title, string url)
        {
            string bookmarkEntry = string.Format("{0}|{1}", title ?? string.Empty, url ?? string.Empty);
            File.AppendAllLines(BookmarksFile, new[] { bookmarkEntry });
            Console.Clear();
            SettingsManager.SetColor(ConsoleColor.Green);
            Console.WriteLine("\nBookmarked!\n");
            SettingsManager.ResetColor();
        }

        // Load bookmarks from disk.

        public static List<OnlineBookResult> LoadBookmarkedBooks()
        {
            var results = new List<OnlineBookResult>();
            if (File.Exists(BookmarksFile))
            {
                foreach (var line in File.ReadAllLines(BookmarksFile))
                {
                    var parts = line.Split('|');
                    if (parts.Length == 2)
                    {
                        results.Add(new OnlineBookResult { Title = parts[0], Url = parts[1] });
                    }
                }
            }
            return results;
        }

        // Fetch a broad list and pick N random recommendations.
        public static async Task<List<OnlineBookResult>> GetRandomRecommendedBooksAsync(int count = 5)
        {
            string url = $"https://www.googleapis.com/books/v1/volumes?q=&maxResults=40";
            var results = new List<OnlineBookResult>();
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync(url);
                    var json = JObject.Parse(response);
                    var items = json["items"];
                    if (items != null)
                    {
                        var rnd = new Random();
                        var bookList = new List<OnlineBookResult>();
                        foreach (var item in items)
                        {
                            var info = item["volumeInfo"];
                            bookList.Add(new OnlineBookResult
                            {
                                Title = info?["title"]?.ToString() ?? "No Title",
                                Url = info?["previewLink"]?.ToString() ?? string.Empty
                            });
                        }
                        while (bookList.Count > 0 && results.Count < count)
                        {
                            int idx = rnd.Next(bookList.Count);
                            results.Add(bookList[idx]);
                            bookList.RemoveAt(idx);
                        }
                    }
                }
                catch { }
            }
            return results;
        }

        // Search-and-select flow with ability to open or bookmark.
        public static async Task BrowseOnlineLibraryWithBookmark()
        {
            Console.Write("Enter search query: ");
            string query = Console.ReadLine();
            Console.Write("Sort by (1 for Relevance, 2 for Newest): ");
            string sortInput = Console.ReadLine();
            string orderBy = sortInput == "2" ? "newest" : "relevance";

            var results = await SearchOnlineBooksAsync(query, orderBy);
            if (results == null || results.Count == 0)
            {
                Console.WriteLine("No online books found.");
                return;
            }

            SettingsManager.SetColor(ConsoleColor.Green);
            Console.WriteLine("Online Library Results:");
            for (int i = 0; i < results.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {results[i].Title}");
            }
            SettingsManager.ResetColor();
            Console.WriteLine("\nSelect a book by number, ESC to exit.");

            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                    return;
                if (char.IsDigit(key.KeyChar))
                {
                    int idx = key.KeyChar - '1';
                    if (idx >= 0 && idx < results.Count)
                    {
                        Console.Clear();
                        Console.WriteLine($"Title: {results[idx].Title}");
                        Console.WriteLine($"URL: {results[idx].Url}");
                        Console.WriteLine("\nPress 'B' to Bookmark, 'Enter' to Read, or 'ESC' to go back.");

                        while (true)
                        {
                            var actionKey = Console.ReadKey(true);
                            if (actionKey.Key == ConsoleKey.B)
                            {
                                BookmarkOnlineBook(results[idx].Title, results[idx].Url);
                                break;
                            }
                            if (actionKey.Key == ConsoleKey.Enter)
                            {
                                TryOpenBrowser(results[idx].Url);
                                break;
                            }
                            if (actionKey.Key == ConsoleKey.Escape)
                                break;
                        }
                        Console.WriteLine("\nPress any key to return to results...");
                        Console.ReadKey(true);
                        Console.Clear();
                        return;
                    }
                }
            }
        }

        // Generate a simple recommendations file from a preset query if not already present.
        public static async Task GenerateRecommendedBooksFileAsync()
        {
            var allBooks = new List<OnlineBookResult>();
            string url = $"https://www.googleapis.com/books/v1/volumes?q=subject:fiction&maxResults=40";
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync(url);
                    var json = JObject.Parse(response);
                    var items = json["items"];
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var info = item["volumeInfo"];
                            allBooks.Add(new OnlineBookResult
                            {
                                Title = info?["title"]?.ToString() ?? "No Title",
                                Url = info?["previewLink"]?.ToString() ?? string.Empty,
                            });
                        }
                    }
                }
                catch { }
            }
            using (var writer = new StreamWriter(RecommendedFile, false))
            {
                foreach (var book in allBooks)
                {
                    writer.WriteLine(string.Format("{0}|{1}", book.Title, book.Url));
                }
            }
        }

        // Load recommendations from the local file.
        public static List<OnlineBookResult> LoadRecommendedBooksFromFile()
        {
            var books = new List<OnlineBookResult>();
            if (File.Exists(RecommendedFile))
            {
                foreach (var line in File.ReadAllLines(RecommendedFile))
                {
                    var parts = line.Split('|');
                    if (parts.Length == 2)
                    {
                        books.Add(new OnlineBookResult { Title = parts[0], Url = parts[1] });
                    }
                }
            }
            return books;
        }


        // Menu surface for online book actions.
        public static async Task OnlineBooksMenuAsync(Action showMainIntro = null)
        {
            if (!File.Exists(RecommendedFile))
                await GenerateRecommendedBooksFileAsync();

            while (true)
            {
                Console.Clear();
                SettingsManager.SetColor(ConsoleColor.Yellow);
                SettingsManager.WriteHeader("Welcome to the Online Library");
                SettingsManager.ResetColor();
                Console.WriteLine("Navigate the online book features below:");
                Console.WriteLine("1. Search for Books");
                Console.WriteLine("2. View Recommended Books");
                Console.WriteLine("3. View Bookmarked Books");
                Console.WriteLine("4. Back to Main Menu");
                Console.Write("\nSelect an option (1-4): ");
                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        Console.Clear();
                        await BrowseOnlineLibraryWithBookmark();
                        break;
                    case "2":
                        await ShowRecommendedBooksAsync();
                        break;
                    case "3":
                        await ShowBookmarksAsync();
                        break;
                    case "4":
                        Console.Clear();
                        if (showMainIntro != null) showMainIntro();
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please select 1-4.");
                        break;
                }
            }
        }

        private static async Task ShowRecommendedBooksAsync()
        {
            bool viewingRecommended = true;
            int recommendedPage = 0;
            const int recommendedPageSize = 5;
            while (viewingRecommended)
            {
                Console.Clear();
                var allRecommended = LoadRecommendedBooksFromFile();
                if (allRecommended.Count == 0)
                {
                    Console.WriteLine("No recommended books found.");
                    break;
                }
                SettingsManager.SetColor(ConsoleColor.Cyan);
                SettingsManager.WriteHeader("Recommended Books");
                SettingsManager.ResetColor();
                var rnd = new Random();
                var pageBooks = allRecommended.OrderBy(x => rnd.Next()).Skip(recommendedPage * recommendedPageSize).Take(recommendedPageSize).ToList();
                for (int i = 0; i < pageBooks.Count; i++)
                {
                    SettingsManager.SetColor(ConsoleColor.Cyan);
                    Console.WriteLine($"{i + 1}. {pageBooks[i].Title}");
                    SettingsManager.SetColor(ConsoleColor.Green);
                    Console.WriteLine($"   {pageBooks[i].Url}");
                    SettingsManager.ResetColor();
                }
                Console.WriteLine("\nType a number to select a book, 'refresh' to see another set, or 'back' to return to main menu...");
                var refreshInput = Console.ReadLine();
                if (refreshInput.Trim().ToLower() == "refresh")
                {
                    recommendedPage = (recommendedPage + 1) % Math.Max(1, (allRecommended.Count / recommendedPageSize));
                    continue;
                }
                if (refreshInput.Trim().ToLower() == "back" || string.IsNullOrWhiteSpace(refreshInput))
                {
                    viewingRecommended = false;
                    break;
                }
                int selNum;
                if (int.TryParse(refreshInput, out selNum) && selNum >= 1 && selNum <= pageBooks.Count)
                {
                    var selected = pageBooks[selNum - 1];
                    Console.Clear();
                    Console.WriteLine($"Title: {selected.Title}");
                    Console.WriteLine($"URL: {selected.Url}");
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("1. Read");
                    Console.WriteLine("2. Bookmark");
                    Console.WriteLine("3. Return to Recommended Books");
                    Console.Write("Select an option (1-3): ");
                    var action = Console.ReadLine();
                    if (action == "1")
                    {
                        Console.Clear();
                        TryOpenBrowser(selected.Url);
                        Console.WriteLine("\nPress any key to return...");
                        Console.ReadKey(true);
                    }
                    else if (action == "2")
                    {
                        BookmarkOnlineBook(selected.Title, selected.Url);
                        Console.WriteLine("\nBook bookmarked!\nPress any key to return...");
                        Console.ReadKey(true);
                    }
                }
            }
        }

        private static async Task ShowBookmarksAsync()
        {
            Console.Clear();
            var bookmarks = LoadBookmarkedBooks();

            while (true)
            {
                SettingsManager.SetColor(ConsoleColor.Green);
                SettingsManager.WriteHeader("Bookmarked Books");
                SettingsManager.ResetColor();

                if (bookmarks.Count == 0)
                {
                    Console.WriteLine("\nNo bookmarked books found.\n");
                    Console.WriteLine("\nPress any key to return to menu...");
                    Console.ReadKey(true);
                    return;
                }

                for (int i = 0; i < bookmarks.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {bookmarks[i].Title}");
                }

                Console.WriteLine("\nOptions: [M]anage, [S]ort, [F]ilter, [C]ancel");
                Console.Write("> ");
                var choice = Console.ReadLine()?.ToLowerInvariant();

                if (choice == "m")
                {
                    Console.Write("\nEnter the number of a bookmarked book to manage: ");
                    var sel = Console.ReadLine();
                    int selNum;
                    if (int.TryParse(sel, out selNum) && selNum >= 1 && selNum <= bookmarks.Count)
                    {
                        ManageSingleBookmark(bookmarks, selNum);
                        bookmarks = LoadBookmarkedBooks(); // Reload after potential unbookmark
                    }
                }
                else if (choice == "s")
                {
                    Console.Write("Sort by Title (1 for A-Z, 2 for Z-A): ");
                    var sortChoice = Console.ReadLine();
                    if (sortChoice == "1")
                        bookmarks = bookmarks.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase).ToList();
                    else if (sortChoice == "2")
                        bookmarks = bookmarks.OrderByDescending(b => b.Title, StringComparer.OrdinalIgnoreCase).ToList();
                    Console.Clear();
                }
                else if (choice == "f")
                {
                    Console.Write("Enter text to filter titles by: ");
                    var filterText = Console.ReadLine();
                    bookmarks = LoadBookmarkedBooks().Where(b => b.Title.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    Console.Clear();
                }
                else
                {
                    return;
                }
            }
        }

        private static void ManageSingleBookmark(List<OnlineBookResult> bookmarks, int selNum)
        {
            Console.Clear();
            var selected = bookmarks[selNum - 1];
            Console.WriteLine($"Title: {selected.Title}");
            Console.WriteLine($"URL: {selected.Url}");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("1. Read");
            Console.WriteLine("2. Unbookmark");
            Console.WriteLine("3. Return to Bookmarks");
            Console.Write("Select an option (1-3): ");
            var action = Console.ReadLine();
            if (action == "1")
            {
                Console.Clear();
                TryOpenBrowser(selected.Url);
                Console.WriteLine("\nPress any key to return...");
                Console.ReadKey(true);
            }
            else if (action == "2")
            {
                var allLines = File.ReadAllLines(BookmarksFile).ToList();
                // This is a simplification; a robust implementation would find the exact line to remove
                allLines.RemoveAt(selNum - 1);
                File.WriteAllLines(BookmarksFile, allLines);
                Console.WriteLine("\nBook unbookmarked!\nPress any key to return...");
                Console.ReadKey(true);
            }
        }

        private static void TryOpenBrowser(string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    System.Diagnostics.Process.Start(url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to open browser: " + ex.Message);
            }
        }

        public class OnlineBookResult
        {
            public string Title { get; set; }
            public string Url { get; set; }
        }
    }
}
