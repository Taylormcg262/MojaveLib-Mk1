using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MojaveLibMk1
{

    // Console-based AI Topic Explorer integrated with a local Ollama server.
    // Provides single-turn and chat flows, with live streaming and a scrollable viewer.

    internal static class AiAssistant
    {
        public static async Task ShowAiTopicExplorerAsync()
        {
            // Older .NET Frameworks may need this to call TLS endpoints reliably
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nAI Topic Explorer (Ollama)");
            Console.ResetColor();
            Console.Write("\nEnter a topic: ");
            var topic = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(topic))
            {
                Console.WriteLine("\nNo topic entered. Press any key to return...");
                Console.ReadKey(true);
                return;
            }

            // Ollama-only configuration
            var baseUrl = EnvOrConfig("OLLAMA_BASE_URL") ?? "http://localhost:11434";
            var model = EnvOrConfig("OLLAMA_MODEL") ?? "llama3:8b";

            Console.WriteLine($"\nProvider: Ollama");
            Console.WriteLine($"Model: {model}");
            Console.WriteLine($"num_predict: {EnvOrConfigInt("OLLAMA_NUM_PREDICT", 2048)}, top_p: {EnvOrConfigDouble("OLLAMA_TOP_P", 0.9)}, temperature: {EnvOrConfigDouble("OLLAMA_TEMPERATURE", 0.6)}");
            Console.WriteLine("Connecting to Ollama and streaming output...");

            try
            {
                string content;

                // Prefer streaming for live progress; fallback to non-streaming
                content = await TryStreamingFirstAsync(() => StreamOllamaAsync(baseUrl, model, topic, CancellationToken.None),
                                                       () => CallOllamaAsync(baseUrl, model, topic));

                if (string.IsNullOrWhiteSpace(content))
                {
                    WriteError("No content returned from Ollama.");
                }
                else
                {
                    // Initialize a chat session so user can continue the conversation
                    var chat = new ChatSession(baseUrl, model);
                    chat.AddSystem(BuildSystemPrompt());
                    chat.AddUser(BuildUserPrompt(topic));
                    chat.AddAssistant(content);

                    // Open scrollable viewer with controls and continue-chat support
                    await ShowScrollableViewerAsync(content, chat);
                }
            }
            catch (Exception ex)
            {
                WriteError($"AI request failed: {ex.Message}");
                Console.WriteLine("Tip: Ensure Ollama is running (e.g., 'ollama serve') and the base URL is reachable.");
            }
        }

        // Prompt builders
        private static string BuildSystemPrompt()
        {
            return "You are a domain expert. Produce a long-form, highly informative explanation (roughly 1200–2000 words). " +
                   "Focus on clear, well-structured paragraphs and optional short subheadings only where needed. " +
                   "Do not include decorative headers or a 'Fun facts' section. " +
                   "Cover definitions, core concepts, mechanisms, step-by-step reasoning, practical examples, trade-offs, " +
                   "common pitfalls with mitigations, and concise takeaways. " +
                   "Prioritize factual accuracy, clarity, and depth. Use bullet lists sparingly and only to improve readability." +
                   "Provide information, Pros and Cons, about what the user will find out with a topic and what they shouldt expect to find out about the topic" +
                   "Provide other similar topics, Goals, Roadmaps, Exercises for the user to consider based off of topic.";
		}

        private static string BuildUserPrompt(string topic)
        {
            return $"Write an in-depth, informative exposition about \"{topic}\". " +
                   "Emphasize practical details, real-world considerations, and precise explanations without fluff.";
        }

        // Viewer with keyboard navigation and actions
        private static async Task ShowScrollableViewerAsync(string initialContent, ChatSession chat)
        {
            var content = initialContent ?? string.Empty;
            while (true)
            {
                var exitViewer = await ScrollAndHandleActionsAsync(
                    content,
                    onSaveToBook: async text =>
                    {
                        Console.Write("\nEnter a title for your new personal book entry: ");
                        var title = Console.ReadLine() ?? string.Empty;
                        var titleWithTag = title + " !AI GENERATED!";
                        var entry =
                            $"Title: {titleWithTag}\n" +
                            $"Type: AI Topic Explorer\n" +
                            $"Entry:\n{text}\n---";
                        EntryManager.SaveNewEntry(titleWithTag, entry);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\nSaved to your personal book entries.");
                        Console.ResetColor();
                        await Task.Delay(600);
                    },
                    onContinueChat: async () =>
                    {
                        Console.Write("\nEnter your follow-up question (blank to cancel): ");
                        var followUp = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(followUp))
                            return content;

                        chat.AddUser(followUp);
                        Console.WriteLine("\nContinuing the conversation (streaming)...");

                        string assistant = await TryStreamingFirstAsync(
                            () => StreamOllamaChatAsync(chat, CancellationToken.None),
                            () => CallOllamaChatAsync(chat)
                        );

                        chat.AddAssistant(assistant ?? string.Empty);

                        var appended = content +
                                       "\n\n---\nUser: " + followUp +
                                       "\n\nAssistant:\n" + (assistant ?? string.Empty);
                        return appended;
                    });

                if (exitViewer.shouldExit)
                    break;

                if (exitViewer.updatedContent != null)
                    content = exitViewer.updatedContent;
            }
        }

        private static async Task<(bool shouldExit, string updatedContent)> ScrollAndHandleActionsAsync(
            string content,
            Func<string, Task> onSaveToBook,
            Func<Task<string>> onContinueChat)
        {
            // Prepare wrapped lines for viewport
            var width = Math.Max(40, Console.WindowWidth - 1);
            var height = Math.Max(10, Console.WindowHeight - 6); // leave space for header/footer
            var lines = WrapToWidth(content ?? string.Empty, width);
            var top = 0;

            void Render()
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("AI Result (Ollama)");
                Console.ResetColor();
                Console.WriteLine($"Lines {Math.Min(lines.Count, top + 1)}-{Math.Min(lines.Count, top + height)} of {lines.Count}");
                Console.WriteLine(new string('-', Math.Max(10, Math.Min(width, Console.WindowWidth - 1))));

                for (int i = 0; i < height; i++)
                {
                    var li = top + i;
                    Console.WriteLine(li >= 0 && li < lines.Count ? lines[li] : string.Empty);
                }

                Console.WriteLine(new string('-', Math.Max(10, Math.Min(width, Console.WindowWidth - 1))));
                Console.WriteLine("Controls: q=Up, e=Down, B=Save to Book, T=Ask follow-up, Esc=Exit");
            }

            Render();

            while (true)
            {
                var key = Console.ReadKey(true);
                var ch = char.ToLowerInvariant(key.KeyChar);

                if (key.Key == ConsoleKey.Escape)
                    return (true, null);

                if (ch == 'q')
                {
                    if (top > 0) { top--; Render(); }
                }
                else if (ch == 'e')
                {
                    if (top < Math.Max(0, lines.Count - height)) { top++; Render(); }
                }
                else if (ch == 'b')
                {
                    await onSaveToBook(content);
                    Render();
                }
                else if (ch == 't')
                {
                    var updated = await onContinueChat();
                    if (!string.IsNullOrEmpty(updated) && !ReferenceEquals(updated, content))
                    {
                        content = updated;
                        lines = WrapToWidth(content, width);
                        top = Math.Max(0, lines.Count - height); // jump to bottom to see new content
                        Render();
                        return (false, content);
                    }
                    Render();
                }
            }
        }

        // Word-wrapping utility
        private static List<string> WrapToWidth(string text, int width)
        {
            var result = new List<string>(capacity: text.Length / Math.Max(1, width));
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        result.Add(string.Empty);
                        continue;
                    }

                    int start = 0;
                    while (start < line.Length)
                    {
                        int remaining = line.Length - start;
                        if (remaining <= width)
                        {
                            result.Add(line.Substring(start));
                            break;
                        }

                        // Try to wrap at the last space within width
                        int wrapAt = line.LastIndexOf(' ', start + width, width);
                        if (wrapAt <= start)
                        {
                            // No space found, hard wrap
                            result.Add(line.Substring(start, width));
                            start += width;
                        }
                        else
                        {
                            result.Add(line.Substring(start, wrapAt - start));
                            start = wrapAt + 1;
                        }
                    }
                }
            }
            return result;
        }

        // Non-streaming Ollama (single-turn fallback)
        private static async Task<string> CallOllamaAsync(string baseUrl, string model, string topic)
        {
            var prompt = $"{BuildSystemPrompt()}\n\nTopic: \"{topic}\"\n\n{BuildUserPrompt(topic)}";
            var payload = BuildOptionsPayload(model);
            payload["prompt"] = prompt;
            payload["stream"] = false;

            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(10);
                var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var url = $"{baseUrl.TrimEnd('/')}/api/generate";
                using (var resp = await http.PostAsync(url, body))
                {
                    var text = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Ollama error {(int)resp.StatusCode}: {text}");

                    var json = JObject.Parse(text);
                    return json["response"]?.ToString();
                }
            }
        }

        // Streaming Ollama (single-turn)
        private static async Task<string> StreamOllamaAsync(string baseUrl, string model, string topic, CancellationToken ct)
        {
            var prompt = $"{BuildSystemPrompt()}\n\nTopic: \"{topic}\"\n\n{BuildUserPrompt(topic)}";
            var payload = BuildOptionsPayload(model);
            payload["prompt"] = prompt;
            payload["stream"] = true;

            using (var http = new HttpClient())
            using (var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/generate"))
            {
                http.Timeout = TimeSpan.FromMinutes(15);
                req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                using (var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        var err = await resp.Content.ReadAsStringAsync();
                        throw new InvalidOperationException($"Ollama stream error {(int)resp.StatusCode}: {err}");
                    }

                    var sb = new StringBuilder();
                    using (var stream = await resp.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        Console.WriteLine(); // start streaming on a new line
                        while (!reader.EndOfStream && !ct.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            JObject json;
                            try { json = JObject.Parse(line); }
                            catch { continue; }

                            var piece = json["response"]?.ToString();
                            if (!string.IsNullOrEmpty(piece))
                            {
                                Console.Write(piece);
                                sb.Append(piece);
                            }

                            var done = json["done"]?.ToObject<bool>() ?? false;
                            if (done) break;
                        }
                        Console.WriteLine();
                    }
                    return sb.ToString();
                }
            }
        }

        // Chat support: continue the same conversation (non-streaming fallback)
        private static async Task<string> CallOllamaChatAsync(ChatSession chat)
        {
            var payload = BuildOptionsPayload(chat.Model);
            payload["messages"] = chat.ToMessagesArray();
            payload["stream"] = false;

            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(10);
                var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var url = $"{chat.BaseUrl.TrimEnd('/')}/api/chat";
                using (var resp = await http.PostAsync(url, body))
                {
                    var text = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Ollama chat error {(int)resp.StatusCode}: {text}");

                    var json = JObject.Parse(text);
                    var msg = json["message"]?["content"]?.ToString();
                    return msg;
                }
            }
        }

        // Chat support: streaming continuation
        private static async Task<string> StreamOllamaChatAsync(ChatSession chat, CancellationToken ct)
        {
            var payload = BuildOptionsPayload(chat.Model);
            payload["messages"] = chat.ToMessagesArray();
            payload["stream"] = true;

            using (var http = new HttpClient())
            using (var req = new HttpRequestMessage(HttpMethod.Post, $"{chat.BaseUrl.TrimEnd('/')}/api/chat"))
            {
                http.Timeout = TimeSpan.FromMinutes(15);
                req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                using (var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        var err = await resp.Content.ReadAsStringAsync();
                        throw new InvalidOperationException($"Ollama chat stream error {(int)resp.StatusCode}: {err}");
                    }

                    var sb = new StringBuilder();
                    using (var stream = await resp.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        Console.WriteLine(); // new line for streaming
                        while (!reader.EndOfStream && !ct.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            JObject json;
                            try { json = JObject.Parse(line); }
                            catch { continue; }

                            var piece = json["message"]?["content"]?.ToString();
                            if (!string.IsNullOrEmpty(piece))
                            {
                                Console.Write(piece);
                                sb.Append(piece);
                            }

                            var done = json["done"]?.ToObject<bool>() ?? false;
                            if (done) break;
                        }
                        Console.WriteLine();
                    }
                    return sb.ToString();
                }
            }
        }

        // Shared helpers
        private static JObject BuildOptionsPayload(string model)
        {
            return new JObject
            {
                ["model"] = model,
                ["options"] = new JObject
                {
                    ["temperature"] = EnvOrConfigDouble("OLLAMA_TEMPERATURE", 0.6),
                    ["top_p"] = EnvOrConfigDouble("OLLAMA_TOP_P", 0.9),
                    ["num_predict"] = EnvOrConfigInt("OLLAMA_NUM_PREDICT", 2048)
                }
            };
        }

        private static async Task<string> TryStreamingFirstAsync(Func<Task<string>> streaming, Func<Task<string>> fallback)
        {
            try { return await streaming(); }
            catch { return await fallback(); }
        }

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n" + message);
            Console.ResetColor();
        }

        private static string EnvOrConfig(string key)
        {
            return Environment.GetEnvironmentVariable(key) ?? ConfigurationManager.AppSettings[key];
        }

        private static int EnvOrConfigInt(string key, int @default)
        {
            var s = EnvOrConfig(key);
            int v; return int.TryParse(s, out v) ? v : @default;
        }

        private static double EnvOrConfigDouble(string key, double @default)
        {
            var s = EnvOrConfig(key);
            double v; return double.TryParse(s, out v) ? v : @default;
        }

        // Simple message model and chat session holder
        private class ChatMessage
        {
            public string Role;
            public string Content;
            public ChatMessage(string role, string content) { Role = role; Content = content; }
            public JObject ToJson() => new JObject { ["role"] = Role, ["content"] = Content };
        }

        private class ChatSession
        {
            public string BaseUrl { get; }
            public string Model { get; }
            private readonly List<ChatMessage> _messages = new List<ChatMessage>();

            public ChatSession(string baseUrl, string model)
            {
                BaseUrl = baseUrl;
                Model = model;
            }

            public void AddSystem(string content) => _messages.Add(new ChatMessage("system", content));
            public void AddUser(string content) => _messages.Add(new ChatMessage("user", content));
            public void AddAssistant(string content) => _messages.Add(new ChatMessage("assistant", content));

            public JArray ToMessagesArray()
            {
                var arr = new JArray();
                foreach (var m in _messages) arr.Add(m.ToJson());
                return arr;
            }
        }
    }
}
