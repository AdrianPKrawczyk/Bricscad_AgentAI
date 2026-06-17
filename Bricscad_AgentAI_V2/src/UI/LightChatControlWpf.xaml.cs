using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.UI
{
    public partial class LightChatControlWpf : UserControl, IDisposable
    {
        public LightChatControlWpf()
        {
            InitializeComponent();
            
                        // Subskrypcja zdarzeĹ„ z silnika w tle
            if (AgentControl.Instance != null && AgentControl.Instance.LlmClient != null)
            {
                AgentControl.Instance.LlmClient.OnStatusUpdate += Instance_OnStatusUpdate;
                AgentControl.Instance.LlmClient.OnStatsUpdate += Instance_OnStatsUpdate;
                AgentControl.Instance.OnHistoryAppended += Instance_OnHistoryAppended;
                AgentControl.Instance.OnSessionReloaded += Instance_OnSessionReloaded;
            }

            // Inicjalizacja pustego dokumentu
            rtbHistory.Document = new FlowDocument();
            AppendLog("System", "Panel czatu (WPF) zainicjowany pomyĹ›lnie.\nWpisz wiadomoĹ›Ä‡ aby rozpoczÄ…Ä‡ konwersacjÄ™.", Colors.DarkGray);
        }

        private void Instance_OnStatusUpdate(string statusText)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                lblStatus.Text = statusText;
            }));
        }

        private void Instance_OnStatsUpdate(LLMStats stats)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                lblStats.Text = $"Tokens: {stats.TotalTokens}";
            }));
        }

        private void Instance_OnHistoryAppended(string role, string text, System.Drawing.Color color)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Color wpfColor = Color.FromArgb(color.A, color.R, color.G, color.B);
                AppendLog(role.ToUpper(), text, wpfColor);
            }));
        }

        private void Instance_OnSessionReloaded()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                rtbHistory.Document.Blocks.Clear();
                AppendLog("System", "Sesja zostaĹ‚a przeĹ‚adowana.", Colors.DarkGray);
                
                // OdĹ›wieĹĽenie peĹ‚nej historii
                if (AgentControl.Instance?.LlmClient != null)
                {
                    foreach (var msg in SessionManager.CurrentSession?.Messages ?? new System.Collections.Generic.List<ChatMessage>())
                    {
                        Color c = Colors.White;
                        if (msg.Role.ToLower() == "user") c = Colors.LightGreen;
                        else if (msg.Role.ToLower() == "system") c = Colors.Orange;
                        AppendLog(msg.Role, msg.Content?.ToString() ?? "", c);
                    }
                }
            }));
        }

        private void AppendLog(string role, string text, Color color)
        {
            var p = new Paragraph();
            p.Margin = new Thickness(0, 0, 0, 5);

            var runRole = new Run($"[{role}] ")
            {
                Foreground = new SolidColorBrush(color),
                FontWeight = FontWeights.Bold
            };
            
            var runText = new Run(text)
            {
                Foreground = new SolidColorBrush(Colors.White)
            };

            p.Inlines.Add(runRole);
            p.Inlines.Add(runText);

            rtbHistory.Document.Blocks.Add(p);
            scrollViewer.ScrollToEnd();
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void txtInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            // Focus input na kaÄąÄ˝dy wciskany znak, jeÄąâ€şli uÄąÄ˝ytkownik nie pisze jeszcze w polu tekstu
            if (!txtInput.IsFocused && e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && e.Key != Key.LeftAlt && e.Key != Key.RightAlt && e.Key != Key.LeftShift && e.Key != Key.RightShift)
            {
                txtInput.Focus();
            }
        }

        private void SendMessage()
        {
            string msg = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            if (AgentControl.Instance != null && AgentControl.Instance.LlmClient != null)
            {
                txtInput.Clear();
                
                // Aktualizuj status UI manualnie przed asynchronicznym strzaÄąâ€šem
                lblStatus.Text = "[Model] Oczekiwanie na odpowiedÄąĹź...";
                
                // Uruchom zadanie delegujĂ„â€¦c komunikacjĂ„â„˘ z LLMClient poprzez Main Orchestrator z AgentControl (fire & forget)
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await AgentControl.Instance.HandleUserInputAsync(msg);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AppendLog("ERROR", $"BÄąâ€šĂ„â€¦d wysyÄąâ€šania: {ex.Message}", Colors.Red);
                        }));
                    }
                });
            }
            else
            {
                MessageBox.Show("GÄąâ€šÄ‚Ĺ‚wny silnik AgentControl nie jest jeszcze zainicjowany.", "BÄąâ€šĂ„â€¦d", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            if (AgentControl.Instance != null && AgentControl.Instance.LlmClient != null)
            {
                AgentControl.Instance.LlmClient.OnStatusUpdate -= Instance_OnStatusUpdate;
                AgentControl.Instance.LlmClient.OnStatsUpdate -= Instance_OnStatsUpdate;
                AgentControl.Instance.OnHistoryAppended -= Instance_OnHistoryAppended;
                AgentControl.Instance.OnSessionReloaded -= Instance_OnSessionReloaded;
            }
        }
    }
}



