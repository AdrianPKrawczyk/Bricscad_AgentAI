using Bricscad.ApplicationServices;
using BricsCAD_Agent;
using System;
using Teigha.DatabaseServices;

public class GetPropertiesTool : ITool
{
    public string ActionTag => "[ACTION:GET_PROPERTIES]";
    public string Description => "Pobiera dokładne właściwości geometryczne (np. długość, promień, pole powierzchni) z zaznaczonych obiektów.";
    public string Execute(Document doc, string args = "")
    {
        ObjectId[] zaznaczenie = Komendy.AktywneZaznaczenie;
        if (zaznaczenie == null || zaznaczenie.Length == 0)
            return "WYNIK: Brak zaznaczonych obiektów. Użyj najpierw tagu SELECT.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"WYNIK: Pobrano właściwości dla {zaznaczenie.Length} zaznaczonych obiektów:");

        int limit = Math.Min(zaznaczenie.Length, 15); // Ograniczamy do 15, żeby nie przepalić pamięci LLM

        using (Transaction tr = doc.TransactionManager.StartTransaction())
        {
            for (int i = 0; i < limit; i++)
            {
                Entity ent = tr.GetObject(zaznaczenie[i], OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                sb.AppendLine($"- Obiekt {i + 1} [{ent.GetType().Name}]: Layer: {ent.Layer}, Color: {ent.ColorIndex}");

                if (ent is Line line)
                    sb.AppendLine($"  -> Length: {Math.Round(line.Length, 3)}, StartPt: {FormatPt(line.StartPoint)}, EndPt: {FormatPt(line.EndPoint)}");
                else if (ent is Polyline pline)
                    sb.AppendLine($"  -> Length: {Math.Round(pline.Length, 3)}, Area: {Math.Round(pline.Area, 3)}, Closed: {pline.Closed}");
                else if (ent is Circle circle)
                    sb.AppendLine($"  -> Radius: {Math.Round(circle.Radius, 3)}, Diameter: {Math.Round(circle.Diameter, 3)}, Area: {Math.Round(circle.Area, 3)}");
                else if (ent is Arc arc)
                    sb.AppendLine($"  -> Radius: {Math.Round(arc.Radius, 3)}, Length: {Math.Round(arc.Length, 3)}");
                else if (ent is DBText txt)
                    sb.AppendLine($"  -> Text: \"{txt.TextString}\", Height: {Math.Round(txt.Height, 3)}");
                else if (ent is MText mtxt)
                    sb.AppendLine($"  -> Text: \"{mtxt.Text}\", Height: {Math.Round(mtxt.TextHeight, 3)}");
                else if (ent is Dimension dim)
                    sb.AppendLine($"  -> Measurement: {Math.Round(dim.Measurement, 3)}, DimText: \"{dim.DimensionText}\"");
            }
            tr.Commit();
        }

        if (zaznaczenie.Length > limit)
            sb.AppendLine($"... i {zaznaczenie.Length - limit} innych ukrytych obiektów.");

        return sb.ToString();
    }

    public string Execute(Document doc) => Execute(doc, "");

    private string FormatPt(Teigha.Geometry.Point3d pt) => $"({Math.Round(pt.X, 2)}, {Math.Round(pt.Y, 2)})";
}