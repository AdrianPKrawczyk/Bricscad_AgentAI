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
        public string Description => "Wyświetla interaktywne okno wyboru (pojedyncze lub Checkboxy - wielokrotne).";

        private const string RegistryPath = @"Software\BricsCADAgentAI\Settings";

        public string Execute(Document doc, string jsonArgs)
        {
            // --- PANCERNE CZYSZCZENIE JSON-a ---
            // Zdejmujemy wszelkie backslashe narzucone przez format JSONL w DB Managerze
            string cleanArgs = jsonArgs.Replace("\\\"", "\"").Replace("\\n", "\n");

            string question = "Wybierz opcje:";
            Match mQuestion = Regex.Match(cleanArgs, @"""Question""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (mQuestion.Success) question = mQuestion.Groups[1].Value;

            // --- KULOODPORNE WYKRYWANIE MULTISELECT ---
            bool multiSelect = false;
            // Dopuszcza spacje, true w cudzysłowach, brak cudzysłowów, duże/małe litery
            if (Regex.IsMatch(cleanArgs, @"""MultiSelect""\s*:\s*(true|""true"")", RegexOptions.IgnoreCase))
            {
                multiSelect = true;
            }

            List<string> options = new List<string>();

            Match mFetchTarget = Regex.Match(cleanArgs, @"""FetchTarget""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (mFetchTarget.Success)
            {
                string target = mFetchTarget.Groups[1].Value;
                Match mFetchScope = Regex.Match(cleanArgs, @"""FetchScope""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                string scope = mFetchScope.Success ? mFetchScope.Groups[1].Value : "Selection";

                Match mFetchProp = Regex.Match(cleanArgs, @"""FetchProperty""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                string prop = mFetchProp.Success ? mFetchProp.Groups[1].Value : "";

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
                else return $"WYNIK: BŁĄD - Nie udało się pobrać listy: {wynikListUnique}";
            }
            else
            {
                Match mOptionsArray = Regex.Match(cleanArgs, @"""Options""\s*:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                if (mOptionsArray.Success)
                {
                    string arrayContent = mOptionsArray.Groups[1].Value;
                    MatchCollection optionMatches = Regex.Matches(arrayContent, @"""([^""]+)""");
                    foreach (Match m in optionMatches) options.Add(m.Groups[1].Value);
                }
            }

            if (options.Count == 0) return "WYNIK: BŁĄD - Brak opcji do wyświetlenia.";

            string selectedOption = "";

            using (Form prompt = new Form())
            {
                int defaultWidth = 900, defaultHeight = 1250;
                LoadWindowSizeFromRegistry(out defaultWidth, out defaultHeight, defaultWidth, defaultHeight);

                prompt.Width = defaultWidth; prompt.Height = defaultHeight;
                prompt.FormBorderStyle = FormBorderStyle.Sizable;
                prompt.Text = multiSelect ? "Decyzja Agenta AI (Wielokrotny Wybór)" : "Decyzja Agenta AI";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.KeyPreview = true;

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
                prompt.Controls.Add(textLabel);

                Control listControl;
                if (multiSelect)
                {
                    CheckedListBox clb = new CheckedListBox()
                    {
                        Left = 15,
                        Top = 65,
                        Width = 850,
                        Height = 1000,
                        Font = new Font("Segoe UI", 10),
                        CheckOnClick = true,
                        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                    };
                    foreach (var opt in options) clb.Items.Add(opt);
                    listControl = clb;

                    CheckBox selectAllCheck = new CheckBox()
                    {
                        Text = "Zaznacz / Odznacz wszystkie",
                        Left = 15,
                        Top = 1105,
                        Width = 250,
                        Height = 30,
                        Font = new Font("Segoe UI", 10, FontStyle.Bold),
                        Anchor = AnchorStyles.Bottom | AnchorStyles.Left
                    };

                    selectAllCheck.CheckedChanged += (s, e) =>
                    {
                        for (int i = 0; i < clb.Items.Count; i++)
                        {
                            clb.SetItemChecked(i, selectAllCheck.Checked);
                        }
                    };

                    prompt.Controls.Add(selectAllCheck);
                }
                else
                {
                    ListBox lb = new ListBox()
                    {
                        Left = 15,
                        Top = 65,
                        Width = 850,
                        Height = 1000,
                        Font = new Font("Segoe UI", 10),
                        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                    };
                    foreach (var opt in options) lb.Items.Add(opt);
                    if (lb.Items.Count > 0) lb.SelectedIndex = 0;
                    lb.DoubleClick += (s, e) => { prompt.DialogResult = DialogResult.OK; prompt.Close(); };
                    listControl = lb;
                }
                prompt.Controls.Add(listControl);

                Button confirmation = new Button()
                {
                    Text = "Zatwierdź (ENTER)",
                    Left = 650,
                    Width = 200,
                    Height = 40,
                    Top = 1100,
                    DialogResult = DialogResult.OK,
                    Font = new Font("Segoe UI", 9),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                DialogResult dialogResult = Bricscad.ApplicationServices.Application.ShowModalDialog(prompt);
                SaveWindowSizeToRegistry(prompt.Width, prompt.Height);

                if (dialogResult == DialogResult.OK)
                {
                    if (multiSelect)
                    {
                        var clb = (CheckedListBox)listControl;
                        List<string> selected = new List<string>();
                        foreach (var item in clb.CheckedItems) selected.Add(item.ToString());

                        if (selected.Count == 0) return "WYNIK: Użytkownik anulował (nie zaznaczono żadnej opcji).";
                        selectedOption = string.Join(", ", selected);
                    }
                    else
                    {
                        var lb = (ListBox)listControl;
                        if (lb.SelectedItem != null) selectedOption = lb.SelectedItem.ToString();
                        else return "WYNIK: Użytkownik anulował (ESC).";
                    }
                }
                else return "WYNIK: Użytkownik anulował wybór (ESC lub zamknięcie okna).";
            }

            return $"WYNIK: Użytkownik wybrał opcje: {selectedOption}";
        }

        public string Execute(Document doc) => Execute(doc, "");

        private void LoadWindowSizeFromRegistry(out int width, out int height, int defW, int defH)
        {
            width = defW; height = defH;
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegistryPath)) { if (k != null) { object w = k.GetValue("WindowWidth"); object h = k.GetValue("WindowHeight"); if (w != null && int.TryParse(w.ToString(), out int sw)) width = Math.Max(sw, 300); if (h != null && int.TryParse(h.ToString(), out int sh)) height = Math.Max(sh, 200); } } } catch { }
        }

        private void SaveWindowSizeToRegistry(int width, int height)
        {
            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegistryPath)) { if (k != null) { k.SetValue("WindowWidth", width, RegistryValueKind.DWord); k.SetValue("WindowHeight", height, RegistryValueKind.DWord); } } } catch { }
        }
    }
}