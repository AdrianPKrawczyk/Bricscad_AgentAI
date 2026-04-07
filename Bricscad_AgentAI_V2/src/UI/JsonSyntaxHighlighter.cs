using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Bricscad_AgentAI_V2.UI
{
    /// <summary>
    /// Prosty silnik kolorowania składni JSON dla RichTextBox.
    /// Emuluje wygląd ciemnego motywu Visual Studio Code.
    /// </summary>
    public static class JsonSyntaxHighlighter
    {
        // Regexy dla elementów JSON
        private static readonly Regex KeyRegex = new Regex(@"""[^""]+""(?=\s*:)", RegexOptions.Compiled);
        private static readonly Regex StringRegex = new Regex(@"(?<=:\s*)""[^""]*""", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(@"\b-?\d+(\.\d+)?\b", RegexOptions.Compiled);
        private static readonly Regex BoolRegex = new Regex(@"\b(true|false|null)\b", RegexOptions.Compiled);

        // Paleta kolorów (Dark Mode VSC)
        private static readonly Color ColorKey = Color.FromArgb(156, 220, 254);      // Jasny niebieski
        private static readonly Color ColorString = Color.FromArgb(206, 145, 120);   // Koralowy/Pomarańczowy
        private static readonly Color ColorNumber = Color.FromArgb(181, 206, 168);   // Jasna zieleń
        private static readonly Color ColorValue = Color.FromArgb(197, 134, 192);    // Różowy/Fioletowy
        private static readonly Color ColorNormal = Color.FromArgb(220, 220, 220);   // Szary/Biały

        public static void Highlight(RichTextBox rtb)
        {
            if (string.IsNullOrEmpty(rtb.Text)) return;

            // Zapamiętanie pozycji kursora i scrolla
            int originalIndex = rtb.SelectionStart;
            int originalLength = rtb.SelectionLength;

            // Blokowanie odświeżania GUI (Win32 API pod spodem, ale RichTextBox ma swoje mechanizmy)
            rtb.BeginUpdate(); 
            
            // Resetowanie formatowania
            rtb.SelectAll();
            rtb.SelectionColor = ColorNormal;

            // Wyświetlanie kluczy
            ApplyRegex(rtb, KeyRegex, ColorKey);
            
            // Wyświetlanie wartości tekstowych
            ApplyRegex(rtb, StringRegex, ColorString);
            
            // Wyświetlanie liczb
            ApplyRegex(rtb, NumberRegex, ColorNumber);
            
            // Wyświetlanie typów bool/null
            ApplyRegex(rtb, BoolRegex, ColorValue);

            // Powrót do pierwotnej pozycji
            rtb.Select(originalIndex, originalLength);
            rtb.SelectionColor = ColorNormal;
            rtb.EndUpdate();
        }

        private static void ApplyRegex(RichTextBox rtb, Regex regex, Color color)
        {
            foreach (Match match in regex.Matches(rtb.Text))
            {
                rtb.Select(match.Index, match.Length);
                rtb.SelectionColor = color;
            }
        }
    }

    // Dodatek do RichTextBox, aby uniknąć migotania
    public static class RichTextBoxExtensions
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        private const int WM_SETREDRAW = 0x0b;

        public static void BeginUpdate(this RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);
        }

        public static void EndUpdate(this RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            rtb.Invalidate();
        }
    }
}
