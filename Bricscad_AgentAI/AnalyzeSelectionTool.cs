using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using System.Collections.Generic;

namespace BricsCAD_Agent
{
    public class AnalyzeSelectionTool : ITool
    {
        public string ActionTag => "[ACTION:ANALYZE]";
        public string Description => "Sprawdza co obecnie użytkownik ma zaznaczone. Użyj, gdy nie wiesz jakie obiekty są w pamięci. Wymaga JSON: {}";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.OstatnieZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Nic nie jest zaznaczone.";

            Dictionary<string, int> licznik = new Dictionary<string, int>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    string typ = ent.GetType().Name;
                    if (licznik.ContainsKey(typ)) licznik[typ]++;
                    else licznik[typ] = 1;
                }
                tr.Commit();
            }

            List<string> czesci = new List<string>();
            foreach (var kvp in licznik) czesci.Add($"{kvp.Value}x {kvp.Key}");

            return $"WYNIK ANALIZY (Łącznie {ids.Length} obiektów w pamięci): " + string.Join(", ", czesci);
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}