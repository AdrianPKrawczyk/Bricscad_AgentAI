using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using System.Drawing;
using Microsoft.Win32;

namespace BricsCAD_Agent
{
    public class UserChoiceTool : ITool
    {
        public string ActionTag => "[ACTION:USER_CHOICE]";
        public string Description => "Wyświetla użytkownikowi okno wyboru. Opcje mogą być podane w tagu lub automatycznie pobrane z rysunku.";

        private const string RegistryPath = @"Software\BricsCADAgentAI\Settings";

        public string Execute(Document doc, string jsonArgs)
        {
            // 1. Wyciąganie pytania
            string question = "Wybierz jedną z opcji:";
            Match mQuestion = Regex.Match(jsonArgs, @"\""Question\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mQuestion.Success) question = mQuestion.Groups[1].Value;

            List<string> options = new List<string>();

            // 2A. Sprawdzamy, czy Agent chce automatycznie pobrać listę z rysunku (MOCNE ULEPSZENIE)
            Match mFetchTarget = Regex.Match(jsonArgs, @"\""FetchTarget\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mFetchTarget.Success)
            {
                string target = mFetchTarget.Groups[1].Value;

                Match mFetchScope = Regex.Match(jsonArgs, @"\""FetchScope\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
                string scope = mFetchScope.Success ? mFetchScope.Groups[1].Value : "Selection";

                Match mFetchProp = Regex.Match(jsonArgs, @"\""FetchProperty\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
                string prop = mFetchProp.Success ? mFetchProp.Groups[1].Value : "";

                // Uruchamiamy ListUniqueTool "w tle"
                ListUniqueTool luTool = new ListUniqueTool();
                string jsonArgsLu = target.Equals("Property", StringComparison.OrdinalIgnoreCase)
                    ? $"[ACTION:LIST_UNIQUE {{\"Target\": \"Property\", \"Scope\": \"{scope}\", \"Property\": \"{prop}\"}}]"
                    : $"[ACTION:LIST_UNIQUE {{\"Target\": \"Class\", \"Scope\": \"{scope}\"}}]";

                string wynikListUnique = luTool.Execute(doc, jsonArgsLu);

                int dwukropekIdx = wynikListUnique.LastIndexOf("): ");
                if (dwukropekIdx != -1 && !wynikListUnique.Contains("nie znaleziono żadnych"))
                {
                    string wartosciCsv = wynikListUnique.Substring(dwukropekIdx + 3);
                    string[] elementy = wartosciCsv.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string el in elementy) options.Add(el.Trim());
                }
                else
                {
                    return $"WYNIK: BŁĄD - Nie udało się automatycznie wygenerować listy. Narzędzie zwróciło: {wynikListUnique}";
                }
            }
            // 2B. Jeśli Agent sam podał listę opcji w tablicy (stara metoda)
            else
            {
                Match mOptionsArray = Regex.Match(jsonArgs, @"\""Options\""\s*:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                if (mOptionsArray.Success)
                {
                    string arrayContent = mOptionsArray.Groups[1].Value;
                    MatchCollection optionMatches = Regex.Matches(arrayContent, @"\""([^\""]+)\""");
                    foreach (Match m in optionMatches) options.Add(m.Groups[1].Value);
                }
            }

            if (options.Count == 0) return "WYNIK: BŁĄD - Nie podano żadnych opcji ani parametrów Fetch.";

            string selectedOption = "";

            // 3. Budowa interaktywnego okna dialogowego (WinForms)
            using (Form prompt = new Form())
            {
                int defaultWidth = 900;
                int defaultHeight = 1250;
                LoadWindowSizeFromRegistry(out defaultWidth, out defaultHeight, defaultWidth, defaultHeight);

                prompt.Width = defaultWidth;
                prompt.Height = defaultHeight;
                prompt.FormBorderStyle = FormBorderStyle.Sizable;
                prompt.Text = "Decyzja Agenta AI";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.KeyPreview = true;
                prompt.MaximizeBox = true;
                prompt.MinimizeBox = false;

                Label textLabel = new Label()
                {
                    Left = 15,
                    Top = 15,
                    Width = 850,
                    Height = 40,
                    Text = question,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                ListBox listBox = new ListBox()
                {
                    Left = 15,
                    Top = 65,
                    Width = 850,
                    Height = 1000,
                    Font = new Font("Segoe UI", 10),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                };

                foreach (var opt in options) listBox.Items.Add(opt);
                if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;

                Button confirmation = new Button()
                {
                    Text = "Zatwierdź wybór (ENTER)",
                    Left = 650,
                    Width = 200,
                    Height = 40,
                    Top = 1100,
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(listBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                listBox.DoubleClick += (s, e) => { prompt.DialogResult = DialogResult.OK; prompt.Close(); };

                DialogResult dialogResult = Bricscad.ApplicationServices.Application.ShowModalDialog(prompt);
                SaveWindowSizeToRegistry(prompt.Width, prompt.Height);

                if (dialogResult == DialogResult.OK && listBox.SelectedItem != null)
                {
                    selectedOption = listBox.SelectedItem.ToString();
                }
                else
                {
                    return "WYNIK: Użytkownik anulował wybór (ESC lub zamknięcie okna).";
                }
            }

            return $"WYNIK: Użytkownik wybrał opcję: {selectedOption}";
        }

        public string Execute(Document doc) => Execute(doc, "");

        private void LoadWindowSizeFromRegistry(out int width, out int height, int defW, int defH)
        {
            width = defW; height = defH;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        object wValue = key.GetValue("WindowWidth");
                        object hValue = key.GetValue("WindowHeight");
                        if (wValue != null && int.TryParse(wValue.ToString(), out int storedW)) width = Math.Max(storedW, 300);
                        if (hValue != null && int.TryParse(hValue.ToString(), out int storedH)) height = Math.Max(storedH, 200);
                    }
                }
            }
            catch { }
        }

        private void SaveWindowSizeToRegistry(int width, int height)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue("WindowWidth", width, RegistryValueKind.DWord);
                        key.SetValue("WindowHeight", height, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }
    }
}