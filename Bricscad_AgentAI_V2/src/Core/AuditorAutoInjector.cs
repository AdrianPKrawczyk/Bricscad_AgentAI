using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Filar 5 Agenta Rewidenta: Auto-Inject Properties.
    /// Po kazdym mutujacym narzedziu automatycznie wstrzykuje wlasciwosci
    /// obiektu (Type, Layer, Color, Radius, itp.) do kontekstu modelu.
    /// Model widzi EFEKTY swojej pracy (dowod) zamiast polegac na wlasnych deklaracjach.
    ///
    /// Strategia probkowania (zgodnie z Q4):
    /// - 1 obiekt -> 1 probka
    /// - 2-50 obiektow -> wszystkie (cap=20)
    /// - 50+ obiektow -> AutoInjectSamplePercent=2% z cap=20
    /// </summary>
    public static class AuditorAutoInjector
    {
        /// <summary>
        /// Decyduje ktore Handle'a wziac do wstrzykniecia i zwraca ich wlasciwosci
        /// sformatowane jako tekst do wstrzykniecia do kontekstu.
        ///
        /// Dla MODYFIKACJI (jesli mamy before snapshot) zwraca diff przed/po.
        /// Dla TWORZENIA (brak before) zwraca tylko wlasciwosci "po".
        /// </summary>
        public static string BuildInjectionForHandles(IReadOnlyList<ObjectId> newHandles, int totalMutations)
        {
            if (newHandles == null || newHandles.Count == 0) return null;
            if (!AgentMemoryState.AutoInjectPropertiesEnabled) return null;
            if (!EngineTracer.HasActiveDocument()) return null;

            // Strategia probkowania
            var sampled = SampleHandles(newHandles, totalMutations);

            var sb = new StringBuilder();
            sb.AppendLine("[AUTO-INJECTED PROPERTIES - Auditor Filar 5]");
            sb.AppendLine($"Profil wykonal {totalMutations} mutacji. Pokazuje {sampled.Count} probek (z {newHandles.Count} nowo dodanych/zmienionych):");
            sb.AppendLine();

            foreach (var id in sampled)
            {
                if (id.IsNull) continue;
                string handleHex = id.Handle.ToString();
                string afterJson = SharedMemoryState.Read(EvidenceSnapshot.BlackboardKey("after", handleHex));
                string beforeJson = SharedMemoryState.Read(EvidenceSnapshot.BlackboardKey("before", handleHex));

                var after = EvidenceSnapshot.FromJson(afterJson);
                var before = EvidenceSnapshot.FromJson(beforeJson);

                if (after == null) continue;

                sb.AppendLine($"--- Handle 0x{handleHex} ({after.ObjectType}) ---");

                if (before != null)
                {
                    // MODYFIKACJA - diff przed/po
                    var diffs = GetDiff(before.Properties, after.Properties);
                    if (diffs.Count == 0)
                    {
                        sb.AppendLine("  (brak zmian wlasciwosci - prawdopodobnie mutacja geometryczna)");
                    }
                    else
                    {
                        foreach (var d in diffs)
                        {
                            sb.AppendLine($"  {d.Key}: {d.Value.before} -> {d.Value.after}");
                        }
                    }
                }
                else
                {
                    // TWORZENIE - tylko "po"
                    foreach (var kvp in after.Properties)
                    {
                        if (kvp.Value.Length > 80) continue; // pomijaj dlugie (np. Contents MText)
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("(Powyzsze wlasciwosci zostaly pobrane bezposrednio z bazy DWG - to jest dowod wykonania.)");
            return sb.ToString();
        }

        /// <summary>
        /// Wybiera ktore Handle'a wziac do wstrzykniecia wg strategii z Q4.
        /// </summary>
        private static List<ObjectId> SampleHandles(IReadOnlyList<ObjectId> newHandles, int totalMutations)
        {
            int cap = AgentMemoryState.AutoInjectMaxSamples;

            if (newHandles.Count == 1)
            {
                return new List<ObjectId> { newHandles[0] };
            }

            if (newHandles.Count <= 50)
            {
                return newHandles.Take(cap).ToList();
            }

            // 50+ - probkuj 2% z cap=20
            int percent = AgentMemoryState.AutoInjectSamplePercent > 0
                ? AgentMemoryState.AutoInjectSamplePercent : 2;
            int sampleCount = Math.Min(cap, Math.Max(1, (newHandles.Count * percent) / 100));

            // Deterministyczne probkowanie - bierz co N-tego Handle
            // (lepsze niz losowe - powtarzalne, audytowalne)
            int step = Math.Max(1, newHandles.Count / sampleCount);
            var sampled = new List<ObjectId>();
            for (int i = 0; i < newHandles.Count && sampled.Count < sampleCount; i += step)
            {
                sampled.Add(newHandles[i]);
            }
            return sampled;
        }

        /// <summary>
        /// Zwraca liste roznic miedzy before/after - tylko te properties, ktore sie zmienily.
        /// </summary>
        private static List<KeyValuePair<string, (string before, string after)>> GetDiff(
            Dictionary<string, string> before,
            Dictionary<string, string> after)
        {
            var diffs = new List<KeyValuePair<string, (string, string)>>();
            if (before == null || after == null) return diffs;

            foreach (var kvp in after)
            {
                string beforeVal = before.TryGetValue(kvp.Key, out var b) ? b : null;
                if (beforeVal == null) continue; // pomijaj nowe (np. nowe property)
                if (beforeVal == kvp.Value) continue; // brak zmiany
                diffs.Add(new KeyValuePair<string, (string, string)>(kvp.Key, (beforeVal, kvp.Value)));
            }
            return diffs;
        }
    }
}
