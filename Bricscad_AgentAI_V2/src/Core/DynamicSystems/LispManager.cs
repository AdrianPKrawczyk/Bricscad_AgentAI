using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public class LispManager
    {
        public LispManager()
        {
            AppPaths.EnsureDirectoriesExist();
        }

        public static event Action<string, string> OnLispExecutionRequested;

        public static void TriggerLispExecution(string lispId, string code)
        {
            OnLispExecutionRequested?.Invoke(lispId, code);
        }

        public static void SaveLisp(LispMetadata metadata, string lispCode)
        {
            if (string.IsNullOrWhiteSpace(metadata.LispId)) throw new ArgumentException("LispId cannot be empty");
            // Fix v2.35.4 (BUG #8): Dwukropek ':' jest niedozwolony w nazwach plikow
            // na Windows (File.WriteAllText rzuca ArgumentException). Zostawiamy ':'
            // TYLKO dla atrybutu LispId (uzywany do wywolania), ale do sciezki uzywamy
            // safeId bez dwukropka. Przyklad: 'c:agent_replace_HHMMSSfff' ->
            //   metadata.LispId = 'c:agent_replace_HHMMSSfff' (do SendCommand)
            //   safeFileName    = 'c_agent_replace_HHMMSSfff' (do .lsp/.json)
            string safeFileName = new string(metadata.LispId
                .Replace(":", "_")
                .Where(c => char.IsLetterOrDigit(c) || c == '_')
                .ToArray());
            if (string.IsNullOrWhiteSpace(safeFileName))
                throw new ArgumentException("LispId contains only invalid characters");

            AppPaths.EnsureDirectoriesExist();

            string safeCategory = string.IsNullOrWhiteSpace(metadata.Category) ? "Uncategorized" : metadata.Category;
            foreach (char c in Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()))
            {
                if (c != '/' && c != '\\')
                {
                    safeCategory = safeCategory.Replace(c.ToString(), "");
                }
            }

            string categoryPath = Path.Combine(AppPaths.GetLispPath(), safeCategory);
            Directory.CreateDirectory(categoryPath);

            string metadataPath = Path.Combine(categoryPath, $"{safeFileName}.json");
            string codePath = Path.Combine(categoryPath, $"{safeFileName}.lsp");

            // Zapisz metadane z ORIGINAL LispId (z dwukropkiem - dla SendCommand)
            string metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            File.WriteAllText(metadataPath, metadataJson);
            File.WriteAllText(codePath, lispCode);

            BielikLogger.LogInfo($"[LispManager] Zapisano skrypt LISP: {metadata.LispId} w {safeCategory}");
        }

        public static IEnumerable<LispMetadata> LoadAllLisps()
        {
            string path = AppPaths.GetLispPath();
            if (!Directory.Exists(path)) return new List<LispMetadata>();

            var lisps = new List<LispMetadata>();
            var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var metadata = JsonConvert.DeserializeObject<LispMetadata>(json);
                    
                    if (metadata == null || string.IsNullOrEmpty(metadata.LispId))
                    {
                        continue;
                    }
                    
                    lisps.Add(metadata);
                }
                catch (Exception ex)
                {
                    BielikLogger.LogWarn($"[LispManager] Błąd deserializacji pliku metadanych {file}: {ex.Message}");
                }
            }

            return lisps;
        }

        public static LispMetadata GetMetadata(string lispId)
        {
            if (string.IsNullOrWhiteSpace(lispId)) return null;
            // Fix v2.35.4 (BUG #8): Dwukropek ':' w LispId jest akceptowany (to atrybut
            // dla SendCommand), ale plik na dysku ma '_' zamiast ':'. Przy odczycie
            // musimy porownac z oryginalnym LispId z metadanych (z dwukropkiem),
            // ale tez zaakceptowac wariant z '_' (gdyby ktos podal wariant sciezki).
            var all = LoadAllLisps().ToList();
            return all.FirstOrDefault(m =>
                string.Equals(m.LispId, lispId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.LispId.Replace(":", "_"), lispId.Replace(":", "_"), StringComparison.OrdinalIgnoreCase));
        }

        public static void DeleteLisp(string lispId)
        {
            if (string.IsNullOrWhiteSpace(lispId)) throw new ArgumentException("LispId cannot be empty");
            // Fix v2.35.4 (BUG #8): Dwukropek w nazwie pliku jest nielegalny na Windows.
            // Zastepuj ':' znakiem '_' do wyszukiwania plikow.
            string safeId = new string(lispId
                .Replace(":", "_")
                .Where(c => char.IsLetterOrDigit(c) || c == '_')
                .ToArray());
            if (safeId.Length < 2) throw new ArgumentException("LispId is too short or invalid for safe deletion");

            string basePath = AppPaths.GetLispPath();
            var jsonFiles = Directory.GetFiles(basePath, $"{safeId}.json", SearchOption.AllDirectories);
            var lspFiles = Directory.GetFiles(basePath, $"{safeId}.lsp", SearchOption.AllDirectories);

            foreach (var f in jsonFiles) File.Delete(f);
            foreach (var f in lspFiles) File.Delete(f);
            BielikLogger.LogInfo($"[LispManager] Usunięto skrypt LISP: {safeId}");
        }

        public static string GetLispCode(string lispId)
        {
            if (string.IsNullOrWhiteSpace(lispId)) return null;
            // Fix v2.35.4 (BUG #8): Dwukropek ':' jest nielegalny w nazwach plikow
            // na Windows. Zamieniamy na '_' przy wyszukiwaniu plikow.
            string safeId = new string(lispId
                .Replace(":", "_")
                .Where(c => char.IsLetterOrDigit(c) || c == '_')
                .ToArray());
            if (string.IsNullOrWhiteSpace(safeId)) return null;

            string basePath = AppPaths.GetLispPath();
            var files = Directory.GetFiles(basePath, $"{safeId}.lsp", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                return null;
            }

            return File.ReadAllText(files[0]);
        }
    }
}
