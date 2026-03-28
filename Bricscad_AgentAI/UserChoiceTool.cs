using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
// NOWE: Potrzebujemy tych przestrzeni do zapamiętywania rozmiaru w rejestrze
using System.Drawing;
using Microsoft.Win32;

namespace BricsCAD_Agent
{
    public class UserChoiceTool : ITool
    {
        public string ActionTag => "[ACTION:USER_CHOICE]";
        public string Description => "Wyświetla użytkownikowi interaktywne okno z pytaniem i listą opcji do wyboru. Zapamiętuje rozmiar.";

        // Ścieżka w rejestrze do zapamiętywania ustawień (BricsCAD Agent AI / Settings)
        private const string RegistryPath = @"Software\BricsCADAgentAI\Settings";

        public string Execute(Document doc, string jsonArgs)
        {
            // 1. Wyciąganie pytania
            string question = "Wybierz jedną z opcji:";
            Match mQuestion = Regex.Match(jsonArgs, @"\""Question\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mQuestion.Success) question = mQuestion.Groups[1].Value;

            // 2. Wyciąganie tablicy opcji
            List<string> options = new List<string>();
            Match mOptionsArray = Regex.Match(jsonArgs, @"\""Options\""\s*:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
            if (mOptionsArray.Success)
            {
                string arrayContent = mOptionsArray.Groups[1].Value;
                MatchCollection optionMatches = Regex.Matches(arrayContent, @"\""([^\""]+)\""");
                foreach (Match m in optionMatches) options.Add(m.Groups[1].Value);
            }

            if (options.Count == 0) return "WYNIK: BŁĄD - Nie podano żadnych opcji w kluczu 'Options'.";

            string selectedOption = "";

            // 3. Budowa interaktywnego okna dialogowego (WinForms)
            using (Form prompt = new Form())
            {
                // NOWE: Domyślny rozmiar (zgodnie z propozycją: Wx2, Hx5 od starego 450x250)
                // W: 900, H: 1250. To zapewni dobrą widoczność dla długich list.
                int defaultWidth = 900;
                int defaultHeight = 1250;

                // NOWE: Ładujemy zapisany rozmiar z rejestru (fallback do domyślnego jeśli brak wpisu)
                LoadWindowSizeFromRegistry(out defaultWidth, out defaultHeight, defaultWidth, defaultHeight);

                prompt.Width = defaultWidth;
                prompt.Height = defaultHeight;

                // NOWE: Zmieniamy na Sizable, aby użytkownik mógł chwytać za rogi!
                prompt.FormBorderStyle = FormBorderStyle.Sizable;

                prompt.Text = "Decyzja Agenta AI";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.KeyPreview = true;
                prompt.MaximizeBox = true; // Pozwalamy na maksymalizację
                prompt.MinimizeBox = false;

                Label textLabel = new Label()
                {
                    Left = 15,
                    Top = 15,
                    Width = 850,
                    Height = 40,
                    Text = question,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    // NOWE: Kotwiczenie, by Label rozciągał się wraz z oknem
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                ListBox listBox = new ListBox()
                {
                    Left = 15,
                    Top = 65,
                    Width = 850,
                    Height = 1000,
                    Font = new Font("Segoe UI", 10),
                    // NOWE: Kotwiczenie, by Lista rozciągała się we wszystkie strony
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
                    // NOWE: Kotwiczenie do prawego-dolnego rogu
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(listBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                // Podwójne kliknięcie myszką na liście też zatwierdza
                listBox.DoubleClick += (s, e) => { prompt.DialogResult = DialogResult.OK; prompt.Close(); };

                // Wywołanie okna w sposób bezpieczny dla BricsCADa
                DialogResult dialogResult = Bricscad.ApplicationServices.Application.ShowModalDialog(prompt);

                // NOWE: Bez względu na decyzję (OK czy Anuluj), ZAPISUJEMY rozmiar okna przed jego zniszczeniem!
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

        // =========================================================
        // NOWE: Funkcje pomocnicze do obsługi Rejestru Windows
        // =========================================================

        private void LoadWindowSizeFromRegistry(out int width, out int height, int defW, int defH)
        {
            width = defW;
            height = defH;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        object wValue = key.GetValue("WindowWidth");
                        object hValue = key.GetValue("WindowHeight");

                        if (wValue != null && int.TryParse(wValue.ToString(), out int storedW))
                        {
                            // Upewniamy się, że rozmiar nie jest absurdalnie mały
                            width = Math.Max(storedW, 300);
                        }

                        if (hValue != null && int.TryParse(hValue.ToString(), out int storedH))
                        {
                            height = Math.Max(storedH, 200);
                        }
                    }
                }
            }
            catch { } // Ciche ignorowanie błędu, użyje domyślnych
        }

        private void SaveWindowSizeToRegistry(int width, int height)
        {
            try
            {
                // Tworzymy klucz (jeśli nie istnieje) z uprawnieniami do zapisu
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        // Zapisujemy wartości DWORD (integer)
                        key.SetValue("WindowWidth", width, RegistryValueKind.DWord);
                        key.SetValue("WindowHeight", height, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                // Informujemy użytkownika w konsoli, jeśli z powodu uprawnień systemowych nie można zapisać ustawień
                Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[Uwaga]: Nie udało się zapisać rozmiaru okna w Rejestrze ({ex.Message}).");
            }
        }
    }
}