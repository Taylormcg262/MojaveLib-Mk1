// MojaveLibMk1/UIHelper.cs
using System;

namespace MojaveLibMk1
{
    /// <summary>
    /// Provides helper methods for rendering console UI elements.
    /// </summary>
    internal static class UIHelper
    {
        private static ConsoleColor _defaultColor = ConsoleColor.Gray;

        public static void SetColor(ConsoleColor color)
        {
            if (SettingsManager.Current.UseColors)
            {
                Console.ForegroundColor = color;
            }
        }

        public static void ResetColor()
        {
            if (SettingsManager.Current.UseColors)
            {
                Console.ForegroundColor = _defaultColor;
            }
        }

        /// <summary>
        /// Writes text centered on the current line of the console.
        /// </summary>
        public static void WriteCentered(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Console.WriteLine();
                return;
            }

            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                int left = (Console.WindowWidth > line.Length) ? (Console.WindowWidth - line.Length) / 2 : 0;
                Console.CursorLeft = left;
                Console.Write(line);
            }
        }

        /// <summary>
        /// Writes text centered in the console, followed by a new line.
        /// </summary>
        public static void WriteLineCentered(string text = "")
        {
            if (string.IsNullOrEmpty(text))
            {
                Console.WriteLine();
                return;
            }

            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                int left = (Console.WindowWidth > line.Length) ? (Console.WindowWidth - line.Length) / 2 : 0;
                Console.CursorLeft = left;
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Draws a styled header with centered text.
        /// </summary>
        public static void WriteHeader(string title)
        {
            var useAscii = SettingsManager.Current.UseAsciiUi;
            var top = useAscii ? "+" + new string('-', 46) + "+" : "?" + new string('?', 46) + "?";
            var mid = useAscii ? "|" + CenterText(title, 46) + "|" : "?" + CenterText(title, 46) + "?";
            var bot = useAscii ? "+" + new string('-', 46) + "+" : "?" + new string('?', 46) + "?";

            SetColor(ConsoleColor.Cyan);
            WriteLineCentered(top);
            WriteLineCentered(mid);
            WriteLineCentered(bot);
            ResetColor();
        }

        private static string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) text = string.Empty;
            text = text.Length > width ? text.Substring(0, width) : text;
            int padLeft = (width - text.Length) / 2 + text.Length;
            return text.PadLeft(padLeft).PadRight(width);
        }

        /// <summary>
        /// Prompts the user for input on a centered line.
        /// </summary>
        public static string ReadInputCentered(string prompt)
        {
            WriteCentered(prompt);
            return Console.ReadLine();
        }
    }
}