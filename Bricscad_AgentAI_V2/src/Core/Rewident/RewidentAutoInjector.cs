using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core; // BielikLogger
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Core.Rewident
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
    ///
    /// Fix v2.34.16: nie polega na ModifiedEntities (wymaga subskrypcji
    /// EngineTracer). Zamiast tego uzywa roznicy ModelSpace count (before/after) i
    /// pobiera nowo pojawione Handle bezposrednio z BlockTableRecord.
    ///
    /// Przeniesiony do src/Core/Rewident/ w v2.35.0 (refaktor Auditor -> Rewident).
    /// </summary>
    public static class RewidentAutoInjector
    {
        /// <summary>
        /// Glowna metoda - buduje tekst do wstrzykniecia do kontekstu modelu.
        /// Samodzielnie wykrywa ktore Handle'y sa nowe (diff ModelSpace count) i
        /// pobiera ich wlasciwosci.
        /// </summary>
        /// <param name="modelSpaceCountBefore">Liczba obiektow PRZED mutacja (z hooka ToolOrchestrator)</param>
        /// <param name="modelSpaceCountAfter">Liczba obiektow PO mutacji</param>
        public static string BuildInjection(int modelSpaceCountBefore, int modelSpaceCountAfter)
        {
            if (!RewidentState.AutoInjectPropertiesEnabled) return null;
            if (!EngineTracer.HasActiveDocument()) return null;
            if (modelSpaceCountBefore < 0 || modelSpaceCountAfter < 0) return null;
            int newCount = modelSpaceCountAfter - modelSpaceCountBefore;
            if (newCount <= 0) return null;

            // Pobierz nowo dodane Handle z ModelSpace (snapshot PRZED tego wywolania
            // byl zapisywany przez EngineTracer w poprzednim wywolaniu hook'a).
            // Dla uproszczenia - bierz ostatnie N Handle z ModelSpace.
            var newHandles = GetRecentlyAddedHandles(modelSpaceCountBefore, modelSpaceCountAfter);
            if (newHandles.Count == 0) return null;

            return FormatHandlesForInjection(newHandles, newCount);
        }

        /// <summary>
        /// Pobiera ostatnio dodane Handle z ModelSpace (od pozycji "before" do konca).
        /// Nie wymaga subskrypcji EngineTracer - dziala nawet gdy jest wylaczony.
        /// </summary>
        private static List<ObjectId> GetRecentlyAddedHandles(int modelSpaceCountBefore, int modelSpaceCountAfter)
        {
            var result = new List<ObjectId>();
            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;
            Database db = doc.Database;
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    BlockTableRecord modelSpace = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
                    if (modelSpace == null) return result;

                    // Zbierz wszystkie Handle w ModelSpace (Handle nowo dodanych
                    // obiektow sa na koncu, bo CreateObject dodaje na koniec).
                    var allIds = new List<ObjectId>();
                    foreach (var id in modelSpace)
                    {
                        allIds.Add(id);
                    }

                    int actualCount = allIds.Count;
                    int newCount = actualCount - modelSpaceCountBefore;
                    if (newCount <= 0) return result;

                    // Bierz ostatnie newCount Handle (od konca)
                    var sampledIds = SampleTail(allIds, newCount);
                    tr.Commit();
                    return sampledIds;
                }
            }
            catch (Exception ex)
            {
                BielikLogger.LogWarn($"[AUTO-INJECT] Blad pobierania Handle z ModelSpace: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Bierz ostatnie N Handle z listy (od konca) z limitem cap.
        /// </summary>
        private static List<ObjectId> SampleTail(List<ObjectId> allIds, int newCount)
        {
            int cap = RewidentState.AutoInjectMaxSamples;

            if (newCount <= 50)
            {
                int take = Math.Min(newCount, cap);
                return allIds.Skip(allIds.Count - take).ToList();
            }

            // 50+ - probkuj 2% z cap=20 (deterministyczne, co N-tego)
            int percent = RewidentState.AutoInjectSamplePercent > 0
                ? RewidentState.AutoInjectSamplePercent : 2;
            int sampleCount = Math.Min(cap, Math.Max(1, (newCount * percent) / 100));
            int step = Math.Max(1, newCount / sampleCount);

            var sampled = new List<ObjectId>();
            int startIdx = allIds.Count - newCount;
            for (int i = startIdx; i < allIds.Count && sampled.Count < sampleCount; i += step)
            {
                sampled.Add(allIds[i]);
            }
            return sampled;
        }

        /// <summary>
        /// Format Handle'ow jako tekst do wstrzykniecia. Dla kazdego Handle'a
        /// pobiera wlasciwosci PRzez EngineTracer.CaptureSnapshot (czyste C#,
        /// nie wymaga subskrypcji).
        /// </summary>
        private static string FormatHandlesForInjection(List<ObjectId> handles, int totalMutations)
        {
            if (handles == null || handles.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("[AUTO-INJECTED PROPERTIES - Auditor Filar 5]");
            sb.AppendLine($"Profil wykonal {totalMutations} mutacji. Pokazuje {handles.Count} probek z ostatnio dodanych obiektow:");
            sb.AppendLine();

            foreach (var id in handles)
            {
                if (id.IsNull) continue;
                // CaptureSnapshot dziala bez subskrypcji EngineTracer (otwiera wlasna transakcje)
                var snap = EngineTracer.CaptureSnapshot(id, "after");
                if (snap == null || snap.IsEmpty) continue;

                // Whitelist kluczowych wlasciwosci (max 6) - zmniejsza rozmiar
                // wstrzykniecia do kontekstu modelu (ktory jest propagowany przez
                // LogLoop jako [EARLY EXIT] itp. - patrz v2.34.20).
                var priority = new[] { "Layer", "ColorIndex", "Linetype", "Radius", "Area",
                                       "Length", "Center", "TextString", "Contents",
                                       "Position", "Height", "NumberOfVertices" };
                int shown = 0;
                sb.AppendLine($"--- Handle 0x{snap.Handle} ({snap.ObjectType}) ---");
                foreach (var key in priority)
                {
                    if (shown >= 6) break;
                    if (snap.Properties.TryGetValue(key, out var val) && val.Length <= 80)
                    {
                        sb.AppendLine($"  {key}: {val}");
                        shown++;
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("(Wlasciwosci pobrane bezposrednio z bazy DWG - dowod wykonania.)");
            return sb.ToString();
        }

        /// <summary>
        /// Publiczny wrapper dla GetRecentlyAddedHandles - zwraca ObjectId[]
        /// (zamiast List) dla kompatybilnosci z AgentMemoryState.GetModifiedEntitiesSnapshot().
        /// Uzywane przez ToolOrchestrator jako fallback gdy EngineTracer wylaczony.
        /// </summary>
        public static ObjectId[] GetRecentHandlesFromModelSpacePublic(int modelSpaceCountBefore, int modelSpaceCountAfter)
        {
            return GetRecentlyAddedHandles(modelSpaceCountBefore, modelSpaceCountAfter).ToArray();
        }
    }
}
