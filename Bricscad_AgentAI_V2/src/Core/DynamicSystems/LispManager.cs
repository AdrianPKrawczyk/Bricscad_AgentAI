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

            string metadataPath = Path.Combine(categoryPath, $"{metadata.LispId}.json");
            string codePath = Path.Combine(categoryPath, $"{metadata.LispId}.lsp");
            
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
            return LoadAllLisps().FirstOrDefault(m => m.LispId == lispId);
        }

        public static void DeleteLisp(string lispId)
        {
            string basePath = AppPaths.GetLispPath();
            var jsonFiles = Directory.GetFiles(basePath, $"{lispId}.json", SearchOption.AllDirectories);
            var lspFiles = Directory.GetFiles(basePath, $"{lispId}.lsp", SearchOption.AllDirectories);

            foreach (var f in jsonFiles) File.Delete(f);
            foreach (var f in lspFiles) File.Delete(f);
            BielikLogger.LogInfo($"[LispManager] Usunięto skrypt LISP: {lispId}");
        }

        public static string GetLispCode(string lispId)
        {
            string basePath = AppPaths.GetLispPath();
            var files = Directory.GetFiles(basePath, $"{lispId}.lsp", SearchOption.AllDirectories);
            
            if (files.Length == 0)
            {
                return null;
            }

            return File.ReadAllText(files[0]);
        }
    }
}
