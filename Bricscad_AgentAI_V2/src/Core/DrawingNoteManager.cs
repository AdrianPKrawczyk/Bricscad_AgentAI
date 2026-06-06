using System;
using System.IO;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    public static class DrawingNoteManager
    {
        private static string GetNoteFilePath(string dwgPath)
        {
            if (string.IsNullOrEmpty(dwgPath) || !Path.IsPathRooted(dwgPath))
            {
                // Rysunek niezapisany, np. "Drawing1" lub pusty
                string fileName = string.IsNullOrEmpty(dwgPath) ? "NieznanyRysunek" : dwgPath;
                string tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bricscad_AgentAI", "TempNotes");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                return Path.Combine(tempDir, $"{fileName}.ai_note.md");
            }
            else
            {
                // Rysunek na dysku
                string directory = Path.GetDirectoryName(dwgPath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(dwgPath);
                return Path.Combine(directory, $"{fileNameWithoutExt}.ai_note.md");
            }
        }

        public static string ReadNote(string dwgPath)
        {
            try
            {
                string notePath = GetNoteFilePath(dwgPath);
                if (File.Exists(notePath))
                {
                    return File.ReadAllText(notePath);
                }
            }
            catch (Exception ex)
            {
                BielikLogger.LogWarn($"Nie udało się odczytać notatki dla {dwgPath}: {ex.Message}");
            }
            return string.Empty;
        }

        public static void SaveNote(string dwgPath, string markdownContent)
        {
            try
            {
                string notePath = GetNoteFilePath(dwgPath);
                File.WriteAllText(notePath, markdownContent);
            }
            catch (Exception ex)
            {
                BielikLogger.LogWarn($"Nie udało się zapisać notatki dla {dwgPath}: {ex.Message}");
            }
        }
    }
}
