using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bricscad.ApplicationServices;
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
    ///
    /// Fix v2.34.16: nie polega na AgentMemoryState.ModifiedEntities (wymaga subskrypcji
    /// EngineTracer). Zamiast tego uzywa roznicy ModelSpace count (before/after) i
    /// pobiera nowo pojawione Handle bezposrednio z BlockTableRecord.
    /// </summary>
    public static class AuditorAutoInjector
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
            if (!AgentMemoryState.AutoInjectPropertiesEnabled) return null;
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

                    int totalCount = 0;
                    int cap = AgentMemoryState.AutoInjectMaxSamples;
                    int targetCount = modelSpaceCountAfter;
                    int startFrom = targetCount - cap;
                    if (startFrom < 0) startFrom = 0;

                    // Iteruj od konca - nowe obiekty sa na koncu ModelSpace
                    int idx = 0;
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
            int cap = AgentMemoryState.AutoInjectMaxSamples;

            if (newCount <= 50)
            {
                int take = Math.Min(newCount, cap);
                return allIds.Skip(allIds.Count - take).ToList();
            }

            // 50+ - probkuj 2% z cap=20 (deterministyczne, co N-tego)
            int percent = AgentMemoryState.AutoInjectSamplePercent > 0
                ? AgentMemoryState.AutoInjectSamplePercent : 2;
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

                sb.AppendLine($"--- Handle 0x{snap.Handle} ({snap.ObjectType}) ---");
                foreach (var kvp in snap.Properties)
                {
                    if (kvp.Value.Length > 80) continue; // pomijaj dlugie (np. Contents MText)
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("(Powyzsze wlasciwosci zostaly pobrane bezposrednio z bazy DWG - to jest dowod wykonania.)");
            return sb.ToString();
        }
    }
}
