using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MojaveLibMk1
{
    internal class EntryManager
    {
        public static string filePath = "BookEntries.txt";
        private static readonly object _fileLock = new object();

        private static string ResolveStorePath()
        {
            return Path.GetFullPath(filePath);
        }

        private static void EnsureStoreExists()
        {
            var fullPath = ResolveStorePath();
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (!File.Exists(fullPath))
                File.WriteAllText(fullPath, string.Empty);
        }

        public static List<string> LoadEntries()
        {
            EnsureStoreExists();

            var lines = File.ReadAllLines(ResolveStorePath());
            var entryBlocks = new List<string>();
            var currentBlock = "";
            foreach (var line in lines)
            {
                if (line.Trim() == "---")
                {
                    if (!string.IsNullOrWhiteSpace(currentBlock))
                        entryBlocks.Add(currentBlock.TrimEnd());
                    currentBlock = "";
                }
                else
                {
                    currentBlock += line + "\n";
                }
            }
            if (!string.IsNullOrWhiteSpace(currentBlock))
                entryBlocks.Add(currentBlock.TrimEnd());
            return entryBlocks;
        }

        public static void DisplayEntryTitles(List<string> entryBlocks)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\nSaved Book Entries:\n");
            Console.ResetColor();
            for (int i = 0; i < entryBlocks.Count; i++)
            {
                string[] entryLines = entryBlocks[i].Split('\n');
                string titleLine = entryLines.Length > 0 ? entryLines[0] : "(No Title)";
                Console.WriteLine(string.Format("Entry #{0}: {1}", i + 1, titleLine.Trim()));
            }
            Console.WriteLine();
        }

        public static void EditEntry(List<string> entryBlocks)
        {
            Console.Write("Enter the entry number to edit: ");
            int editNum;
            if (int.TryParse(Console.ReadLine(), out editNum) && editNum >= 1 && editNum <= entryBlocks.Count)
            {
                string[] entryLines = entryBlocks[editNum - 1].Split('\n');
                string title = "", description = "", author = "", genre = "", entryType = "";
                int entryStartIndex = -1;

                for (int i = 0; i < entryLines.Length; i++)
                {
                    string line = entryLines[i].Trim();
                    if (line.StartsWith("Title:")) title = line.Substring("Title:".Length).Trim();
                    else if (line.StartsWith("Type:")) entryType = line.Substring("Type:".Length).Trim();
                    else if (line.StartsWith("Description:")) description = line.Substring("Description:".Length).Trim();
                    else if (line.StartsWith("Author:")) author = line.Substring("Author:".Length).Trim();
                    else if (line.StartsWith("Genre:")) genre = line.Substring("Genre:".Length).Trim();
                    else if (line.StartsWith("Entry:")) { entryStartIndex = i + 1; break; }
                }

                List<string> bookSections = new List<string>();
                if (entryStartIndex != -1)
                {
                    for (int i = entryStartIndex; i < entryLines.Length; i++)
                    {
                        string line = entryLines[i];
                        if (line.Trim() == "---") break;
                        bookSections.Add(line);
                    }
                }

                Console.WriteLine(string.Format("\nTitle: {0}", title));
                Console.WriteLine(string.Format("Type: {0}", entryType));
                if (!string.IsNullOrWhiteSpace(description)) Console.WriteLine(string.Format("Description: {0}", description));
                if (!string.IsNullOrWhiteSpace(author)) Console.WriteLine(string.Format("Author: {0}", author));
                if (!string.IsNullOrWhiteSpace(genre)) Console.WriteLine(string.Format("Genre: {0}", genre));
                Console.WriteLine("\nCurrent book sections:");
                for (int i = 0; i < bookSections.Count; i++)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(string.Format("[{0}] {1}", i + 1, bookSections[i]));
                    Console.ResetColor();
                }

                Console.WriteLine("\nChoose an action:");
                Console.WriteLine("1. Update a section");
                Console.WriteLine("2. Append a new section");
                Console.WriteLine("3. Delete a section");
                Console.WriteLine("4. Cancel");
                Console.Write("Enter your choice (1-4): ");
                string action = Console.ReadLine();

                if (action == "1")
                {
                    Console.Write("Enter section number to update: ");
                    int secNum;
                    if (int.TryParse(Console.ReadLine(), out secNum) && secNum >= 1 && secNum <= bookSections.Count)
                    {
                        Console.WriteLine("Enter new text for this section (type '>end' to finish):");
                        List<string> newSectionLines = new List<string>();
                        while (true)
                        {
                            string line = Console.ReadLine();
                            if (line.Trim() == ">end") break;
                            newSectionLines.Add(line);
                        }
                        bookSections[secNum - 1] = string.Join(Environment.NewLine, newSectionLines);
                    }
                }
                else if (action == "2")
                {
                    Console.WriteLine("Enter new section text (type '>end' to finish):");
                    List<string> newSectionLines = new List<string>();
                    while (true)
                    {
                        string line = Console.ReadLine();
                        if (line.Trim() == ">end") break;
                        newSectionLines.Add(line);
                    }
                    bookSections.Add(string.Join(Environment.NewLine, newSectionLines));
                }
                else if (action == "3")
                {
                    Console.Write("Enter section numbers to delete (e.g. 1,3,5-7): ");
                    string secInput = Console.ReadLine();
                    var secIndices = ParseIndices(secInput, bookSections.Count);
                    if (secIndices.Count == 0)
                    {
                        Console.WriteLine("\nNo valid sections selected.\n");
                    }
                    else
                    {
                        foreach (var idx in secIndices)
                            bookSections.RemoveAt(idx - 1);
                    }
                }
                else
                {
                    Console.WriteLine("Cancelled edit.\n");
                    return;
                }

                string newBook = string.Join(Environment.NewLine, bookSections);
                string updatedEntry =
                    string.Format("Title: {0}\n", title) +
                    string.Format("Type: {0}\n", entryType) +
                    (string.IsNullOrWhiteSpace(description) ? "" : string.Format("Description: {0}\n", description)) +
                    (string.IsNullOrWhiteSpace(author) ? "" : string.Format("Author: {0}\n", author)) +
                    (string.IsNullOrWhiteSpace(genre) ? "" : string.Format("Genre: {0}\n", genre)) +
                    string.Format("Entry:\n{0}\n---", newBook);
                entryBlocks[editNum - 1] = updatedEntry;

                File.WriteAllLines(filePath, entryBlocks);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nEntry updated!\n");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nInvalid entry number.\n");
                Console.ResetColor();
            }
        }

        public static void DeleteEntry(List<string> entryBlocks)
        {
            Console.Write("Enter the entry number to delete: ");
            int delNum;
            if (int.TryParse(Console.ReadLine(), out delNum) && delNum >= 1 && delNum <= entryBlocks.Count)
            {
                entryBlocks.RemoveAt(delNum - 1);
                List<string> nonEmptyBlocks = new List<string>();
                foreach (var block in entryBlocks)
                {
                    if (!string.IsNullOrWhiteSpace(block))
                        nonEmptyBlocks.Add(block);
                }
                File.WriteAllLines(filePath, nonEmptyBlocks);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nEntry deleted!\n");
                Console.ResetColor();
                if (nonEmptyBlocks.Count == 0)
                {
                    Console.WriteLine("No entries to manage.\n");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nInvalid entry number.\n");
                Console.ResetColor();
            }
        }

        public static void ReadEntry()
        {
            var entryBlocks = LoadEntries();
            if (entryBlocks.Count == 0)
            {
                Console.WriteLine("\nNo entries to read.\n");
                return;
            }

            DisplayEntryTitles(entryBlocks);

            Console.Write("Enter the entry number to read: ");
            int readNum;
            if (int.TryParse(Console.ReadLine(), out readNum) && readNum >= 1 && readNum <= entryBlocks.Count)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n------------------- Book Entry -------------------\n");
                Console.ResetColor();
                Console.WriteLine(entryBlocks[readNum - 1]);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n--------------------------------------------------\n");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nInvalid entry number.\n");
                Console.ResetColor();
            }
        }

        public static async void SearchBooks()
        {
            Console.Write("Enter search query: ");
            string query = Console.ReadLine();
            string url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}";

            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync(url);
                    var json = JObject.Parse(response);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(string.Format("\nSearch Results for '{0}':\n", query));
                    Console.ResetColor();
                    foreach (var item in json["items"])
                    {
                        Console.WriteLine(string.Format("- {0}", item["volumeInfo"]["title"]));
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(string.Format("\nError fetching data: {0}\n", ex.Message));
                    Console.ResetColor();
                }
            }
        }

        public static async Task BrowseOnlineLibrary()
        {
            int startIndex = 0;
            const int pageSize = 10;
            string lastQuery = "";

            while (true)
            {
                if (string.IsNullOrEmpty(lastQuery))
                {
                    Console.Write("Enter search query: ");
                    lastQuery = Console.ReadLine();
                }

                string url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(lastQuery)}&startIndex={startIndex}&maxResults={pageSize}";
                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = await client.GetStringAsync(url);
                        var json = JObject.Parse(response);
                        var items = json["items"];
                        if (items == null || !items.Children().Any())
                        {
                            Console.WriteLine("\nNo more results.\n");
                            break;
                        }

                        int itemCount = items.Children().Count();
                        Console.WriteLine(string.Format("\nShowing books {0} to {1} for '{2}':\n", startIndex + 1, startIndex + itemCount, lastQuery));

                        Console.ForegroundColor = ConsoleColor.Green;
                        int idx = 1;
                        foreach (var item in items)
                        {
                            var info = item["volumeInfo"];
                            Console.WriteLine(string.Format("{0}. {1} {2}", idx, info?["title"], (info?["authors"] != null ? "- " + string.Join(", ", info["authors"]) : "")));
                            idx++;
                        }
                        Console.ResetColor();

                        Console.WriteLine("\nType a number to preview, 'next' for more, 'back' for previous, or 'exit' to leave.");
                        string input = Console.ReadLine().Trim().ToLower();

                        if (input == "next")
                        {
                            startIndex += pageSize;
                        }
                        else if (input == "back" && startIndex >= pageSize)
                        {
                            startIndex -= pageSize;
                        }
                        else if (input == "exit")
                        {
                            Console.Clear();
                            break;
                        }
                        else if (int.TryParse(input, out int bookNum) && bookNum >= 1 && bookNum <= itemCount)
                        {
                            var selectedBook = items.Children().ElementAt(bookNum - 1);
                            var info = selectedBook["volumeInfo"];
                            string previewLink = info?["previewLink"]?.ToString() ?? "N/A";
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine(string.Format("\nTitle: {0}", info?["title"]));
                            Console.WriteLine(string.Format("Authors: {0}", (info?["authors"] != null ? string.Join(", ", info["authors"]) : "N/A")));
                            Console.WriteLine(string.Format("Description: {0}", info?["description"] ?? "No description available."));
                            Console.WriteLine(string.Format("Preview Link: {0}", previewLink));
                            Console.ResetColor();

                            // Automatically open the preview link in the default browser if available
                            if (previewLink != "N/A")
                            {
                                Process.Start(previewLink);
                                Console.WriteLine("\nOpened preview link in your browser.");
                            }
                            Console.WriteLine("\nPress Enter to return to the list...");
                            Console.ReadLine();
                        }
                        else
                        {
                            Console.WriteLine("\nInvalid input.\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(string.Format("\nError fetching data: {0}\n", ex.Message));
                        Console.ResetColor();
                        break;
                    }
                }
            }
        }

        // Example: Download and display plain text from a URL
        public static async void ReadOnlineText(string url)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    string text = await client.GetStringAsync(url);
                    Console.WriteLine(text);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }

        // Add this class to represent online book results
        public class OnlineBookResult
        {
            public string Title { get; set; }
            public string Url { get; set; }
        }

        // Add this method to fix CS0117
        public static List<OnlineBookResult> GetOnlineBookResults()
        {
            // Example stub implementation. Replace with actual logic as needed.
            return new List<OnlineBookResult>
            {
                new OnlineBookResult { Title = "Sample Book 1", Url = "http://example.com/book1" },
                new OnlineBookResult { Title = "Sample Book 2", Url = "http://example.com/book2" }
            };
        }

        // Add this helper to parse comma/range input
        private static List<int> ParseIndices(string input, int max)
        {
            var indices = new List<int>();
            var parts = input.Split(',');
            foreach (var part in parts)
            {
                if (part.Contains("-"))
                {
                    var range = part.Split('-');
                    if (int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                    {
                        for (int i = start; i <= end && i <= max; i++)
                            indices.Add(i);
                    }
                }
                else if (int.TryParse(part, out int idx) && idx >= 1 && idx <= max)
                {
                    indices.Add(idx);
                }
            }
            return indices.Distinct().OrderByDescending(i => i).ToList(); // Descending for safe removal
        }

        // Mass delete books
        public static void MassDeleteEntries(List<string> entryBlocks)
        {
            DisplayEntryTitles(entryBlocks);
            Console.Write("Enter entry numbers to delete (e.g. 1,3,5-7): ");
            string input = Console.ReadLine();
            var indices = ParseIndices(input, entryBlocks.Count);
            if (indices.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nNo valid entries selected.\n");
                Console.ResetColor();
                return;
            }
            foreach (var idx in indices)
                entryBlocks.RemoveAt(idx - 1);
            File.WriteAllLines(filePath, entryBlocks);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nSelected entries deleted!\n");
            Console.ResetColor();
        }

        // UPDATED: Save to the aggregated BookEntries.txt so entries appear in Manage/Read lists
        public static void SaveNewEntry(string title, string entryContent)
        {
            var fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Ensure block ends with delimiter
            var content = entryContent?.TrimEnd() ?? "";
            if (!content.EndsWith("---"))
                content += "\n---";

            File.AppendAllText(fullPath, content + Environment.NewLine);
        }

        // Per-file helpers kept for other features; not used by SaveNewEntry anymore
        public static string GetEntryFileName(string title)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                title = title.Replace(c, '_');
            return $"BookEntry_{title}.txt";
        }

        public static List<string> GetAllEntryFiles()
        {
            return Directory.GetFiles(Directory.GetCurrentDirectory(), "BookEntry_*.txt").ToList();
        }

        public static List<string> LoadEntriesFromFiles()
        {
            var files = GetAllEntryFiles();
            var entries = new List<string>();
            foreach (var file in files)
            {
                entries.Add(File.ReadAllText(file));
            }
            return entries;
        }

        public static void DeleteEntryFile(int entryIndex)
        {
            var files = GetAllEntryFiles();
            if (entryIndex >= 0 && entryIndex < files.Count)
            {
                File.Delete(files[entryIndex]);
            }
        }

        public static void EditEntryFile(int entryIndex, string newContent)
        {
            var files = GetAllEntryFiles();
            if (entryIndex >= 0 && entryIndex < files.Count)
            {
                File.WriteAllText(files[entryIndex], newContent);
            }
        }

        // NEW: Filter saved entries by Genre, Type, and Author (case-insensitive, contains)
        public static void FilterEntries()
        {
            var entryBlocks = LoadEntries();
            if (entryBlocks.Count == 0)
            {
                Console.WriteLine("\nNo entries to filter.\n");
                return;
            }

            Console.Write("Filter by Genre (leave blank for any): ");
            var genreFilter = (Console.ReadLine() ?? string.Empty).Trim();

            Console.Write("Filter by Type (leave blank for any): ");
            var typeFilter = (Console.ReadLine() ?? string.Empty).Trim();

            Console.Write("Filter by Author (leave blank for any): ");
            var authorFilter = (Console.ReadLine() ?? string.Empty).Trim();

            var filtered = new List<Tuple<int, string, string, string, string>>(); // (originalIndex, title, type, author, genre)

            for (int i = 0; i < entryBlocks.Count; i++)
            {
                string title, type, author, genre;
                ExtractMetadata(entryBlocks[i], out title, out type, out author, out genre);

                bool genreOk = string.IsNullOrWhiteSpace(genreFilter) || (!string.IsNullOrWhiteSpace(genre) && genre.IndexOf(genreFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                bool typeOk = string.IsNullOrWhiteSpace(typeFilter) || (!string.IsNullOrWhiteSpace(type) && type.IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                bool authorOk = string.IsNullOrWhiteSpace(authorFilter) || (!string.IsNullOrWhiteSpace(author) && author.IndexOf(authorFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                if (genreOk && typeOk && authorOk)
                {
                    filtered.Add(Tuple.Create(i, title, type, author, genre));
                }
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\nFiltered Entries:\n");
            Console.ResetColor();

            if (filtered.Count == 0)
            {
                Console.WriteLine("No entries matched your filters.\n");
                return;
            }

            for (int i = 0; i < filtered.Count; i++)
            {
                var f = filtered[i];
                Console.WriteLine(string.Format("{0}. {1}  [Type: {2}] [Author: {3}] [Genre: {4}] (Entry #{5})",
                    i + 1,
                    string.IsNullOrWhiteSpace(f.Item2) ? "(No Title)" : f.Item2,
                    string.IsNullOrWhiteSpace(f.Item3) ? "N/A" : f.Item3,
                    string.IsNullOrWhiteSpace(f.Item4) ? "N/A" : f.Item4,
                    string.IsNullOrWhiteSpace(f.Item5) ? "N/A" : f.Item5,
                    f.Item1 + 1));
            }

            Console.Write("\nEnter the number of a filtered entry to read, or press Enter to return: ");
            var sel = Console.ReadLine();
            int selNum;
            if (int.TryParse(sel, out selNum) && selNum >= 1 && selNum <= filtered.Count)
            {
                var originalIndex = filtered[selNum - 1].Item1;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n------------------- Book Entry -------------------\n");
                Console.ResetColor();
                Console.WriteLine(entryBlocks[originalIndex]);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n--------------------------------------------------\n");
                Console.ResetColor();
            }
        }

        internal static void ExtractMetadata(string entryBlock, out string title, out string type, out string author, out string genre)
        {
            title = ""; type = ""; author = ""; genre = "";
            var lines = (entryBlock ?? string.Empty).Split('\n');
            foreach (var raw in lines)
            {
                var line = (raw ?? string.Empty).Trim();
                if (line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
                    title = line.Substring("Title:".Length).Trim();
                else if (line.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
                    type = line.Substring("Type:".Length).Trim();
                else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
                    author = line.Substring("Author:".Length).Trim();
                else if (line.StartsWith("Genre:", StringComparison.OrdinalIgnoreCase))
                    genre = line.Substring("Genre:".Length).Trim();
            }
        }
    }
}
