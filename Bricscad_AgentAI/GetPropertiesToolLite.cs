using Bricscad.ApplicationServices;
using BricsCAD_Agent;
using System;
using Teigha.DatabaseServices;

public class GetPropertiesToolLite : ITool // <--- ZMIANA NAZWY KLASY
{
    public string ActionTag => "[ACTION:GET_PROPERTIES_LITE]"; // <--- ZMIANA TAGU

    public string Description =>
        "Odczytuje podstawowe właściwości zaznaczonych obiektów (Warstwa, Kolor, itp.).";

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

                // Tłumaczenie przezroczystości z obiektu CAD na czytelne liczby
                string transpStr = "0";
                if (ent.Transparency.IsByAlpha) transpStr = Math.Round((255.0 - ent.Transparency.Alpha) / 255.0 * 100.0).ToString();
                else if (ent.Transparency.IsByBlock) transpStr = "ByBlock";
                else if (ent.Transparency.IsByLayer) transpStr = "ByLayer";

                // Właściwości bazowe (wspólne dla każdego Entity)
                sb.AppendLine($"- Obiekt {i + 1} [{ent.GetType().Name}]: Layer: {ent.Layer}, Color: {ent.ColorIndex}, Linetype: {ent.Linetype}, LineWeight: {ent.LineWeight}, Transp: {transpStr}");

                // Właściwości specyficzne dla danego typu
                if (ent is Line line)
                    sb.AppendLine($"  -> Length: {Math.Round(line.Length, 3)}, StartPt: {FormatPt(line.StartPoint)}, EndPt: {FormatPt(line.EndPoint)}");
                else if (ent is Polyline pline)
                    sb.AppendLine($"  -> Length: {Math.Round(pline.Length, 3)}, Area: {Math.Round(pline.Area, 3)}, Closed: {pline.Closed}, Elevation: {Math.Round(pline.Elevation, 3)}");
                else if (ent is Circle circle)
                    sb.AppendLine($"  -> Radius: {Math.Round(circle.Radius, 3)}, Area: {Math.Round(circle.Area, 3)}, Center: {FormatPt(circle.Center)}");
                else if (ent is Arc arc)
                    sb.AppendLine($"  -> Radius: {Math.Round(arc.Radius, 3)}, Length: {Math.Round(arc.Length, 3)}, Center: {FormatPt(arc.Center)}");
                else if (ent is DBText txt)
                    sb.AppendLine($"  -> Text: \"{txt.TextString}\", Height: {Math.Round(txt.Height, 3)}, Style: {txt.TextStyleName}");
                else if (ent is MText mtxt)
                    sb.AppendLine($"  -> Text: \"{mtxt.Text}\", Height: {Math.Round(mtxt.TextHeight, 3)}, Style: {mtxt.TextStyleName}");
                else if (ent is Dimension dim)
                    sb.AppendLine($"  -> Measurement: {Math.Round(dim.Measurement, 3)}, DimText: \"{dim.DimensionText}\", Style: {dim.DimensionStyleName}");
                else if (ent is BlockReference br)
                    sb.AppendLine($"  -> BlockName: {br.Name}, Position: {FormatPt(br.Position)}, Rotation: {Math.Round(br.Rotation, 3)}");
                else if (ent is Hatch hatch)
                    sb.AppendLine($"  -> Pattern: {hatch.PatternName}, Scale: {Math.Round(hatch.PatternScale, 3)}, Area: {Math.Round(hatch.Area, 3)}");
            }
            tr.Commit();
        }

        if (zaznaczenie.Length > limit)
            sb.AppendLine($"... i {zaznaczenie.Length - limit} innych ukrytych obiektów.");

        return sb.ToString();
    }

    public string Execute(Document doc) => Execute(doc, "");

    private string FormatPt(Teigha.Geometry.Point3d pt) => $"({Math.Round(pt.X, 2)}, {Math.Round(pt.Y, 2)}, {Math.Round(pt.Z, 2)})";
}