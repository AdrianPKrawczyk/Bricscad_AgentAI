using Bricscad.ApplicationServices;
using BricsCAD_Agent;
using Teigha.DatabaseServices;

public class EditBlockTool : ITool
{
    public string ActionTag => "[ACTION:EDIT_BLOCK]";
    public string Description => "Edytuje właściwości i teksty obiektów wewnątrz zaznaczonych bloków oraz ich atrybutów.";

    public string Execute(Document doc, string args = "")
    {
        ObjectId[] zaznaczenie = Komendy.AktywneZaznaczenie;
        if (zaznaczenie == null || zaznaczenie.Length == 0)
            return "WYNIK: Brak zaznaczonych obiektów. Zaznacz najpierw bloki (BlockReference).";

        int? targetColor = null;
        string targetLayer = null;
        int? filterColor = null;
        string findText = null;
        string replaceText = null;

        // Parsowanie JSON argumentów - wzmocnione o opcjonalne cudzysłowy wokół liczb
        System.Text.RegularExpressions.Match matchColor = System.Text.RegularExpressions.Regex.Match(args, @"\""Color\""\s*:\s*\""?(\d+)\""?");
        if (matchColor.Success) targetColor = int.Parse(matchColor.Groups[1].Value);

        System.Text.RegularExpressions.Match matchLayer = System.Text.RegularExpressions.Regex.Match(args, @"\""Layer\""\s*:\s*\""([^\""]+)\""");
        if (matchLayer.Success) targetLayer = matchLayer.Groups[1].Value;

        System.Text.RegularExpressions.Match matchFilter = System.Text.RegularExpressions.Regex.Match(args, @"\""FilterColor\""\s*:\s*\""?(\d+)\""?");
        if (matchFilter.Success) filterColor = int.Parse(matchFilter.Groups[1].Value);

        System.Text.RegularExpressions.Match matchFindText = System.Text.RegularExpressions.Regex.Match(args, @"\""FindText\""\s*:\s*\""([^\""]+)\""");
        if (matchFindText.Success) findText = matchFindText.Groups[1].Value;

        System.Text.RegularExpressions.Match matchReplaceText = System.Text.RegularExpressions.Regex.Match(args, @"\""ReplaceText\""\s*:\s*\""([^\""]*)\""");
        if (matchReplaceText.Success) replaceText = matchReplaceText.Groups[1].Value;

        // --- NOWOŚĆ: Parsowanie parametru usuwania wymiarów ---
        System.Text.RegularExpressions.Match matchRemoveDim = System.Text.RegularExpressions.Regex.Match(args, @"\""RemoveDimensions\""\s*:\s*(true|false)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        bool removeDimensions = matchRemoveDim.Success && matchRemoveDim.Groups[1].Value.ToLower() == "true";

        // Zaktualizowany warunek zabezpieczający
        if (targetColor == null && targetLayer == null && findText == null && !removeDimensions)
            return "WYNIK: Błąd. Podaj co chcesz zmienić (np. Color, Layer, FindText, ReplaceText lub RemoveDimensions).";

        System.Collections.Generic.HashSet<ObjectId> przetworzoneBloki = new System.Collections.Generic.HashSet<ObjectId>();
        int zmienioneObiektyWewnetrzne = 0;

        try
        {
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in zaznaczenie)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference br)
                    {
                        // 1. Edycja Atrybutów samego wstawienia bloku (ZMIENNE TEKSTY)
                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                            if (attRef != null)
                            {
                                if (filterColor.HasValue && attRef.ColorIndex != filterColor.Value) continue;

                                bool modified = false;
                                if (!string.IsNullOrEmpty(findText) && replaceText != null && attRef.TextString.Contains(findText))
                                {
                                    attRef.UpgradeOpen();
                                    attRef.TextString = attRef.TextString.Replace(findText, replaceText);
                                    modified = true;
                                }

                                if (targetColor.HasValue || targetLayer != null)
                                {
                                    if (!attRef.IsWriteEnabled) attRef.UpgradeOpen();
                                    if (targetColor.HasValue) attRef.ColorIndex = targetColor.Value;
                                    if (targetLayer != null) { try { attRef.Layer = targetLayer; } catch { } }
                                    modified = true;
                                }

                                if (modified) zmienioneObiektyWewnetrzne++;
                            }
                        }

                        // 2. Edycja definicji geometrii i tekstów na stałe w bloku (REKURENCJA)
                        PrzetworzBlokRekurencyjnie(br.BlockTableRecord, tr, targetColor, targetLayer, filterColor, findText, replaceText, removeDimensions, przetworzoneBloki, ref zmienioneObiektyWewnetrzne);
                    }
                }
                tr.Commit();
            }

            if (zmienioneObiektyWewnetrzne > 0)
            {
                doc.Editor.Regen();
                return $"WYNIK: Zmodyfikowano {zmienioneObiektyWewnetrzne} elementów (w tym atrybutów lub tekstów) wewnątrz zaznaczonych bloków.";
            }
            else
            {
                return "WYNIK: Nie znaleziono obiektów spełniających kryteria wewnątrz tych bloków.";
            }
        }
        catch (System.Exception ex)
        {
            return "WYNIK: Błąd edycji bloku: " + ex.Message;
        }
    }

    public string Execute(Document doc) => Execute(doc, "");

    private void PrzetworzBlokRekurencyjnie(ObjectId btrId, Transaction tr, int? targetColor, string targetLayer, int? filterColor, string findText, string replaceText, bool removeDimensions, System.Collections.Generic.HashSet<ObjectId> przetworzoneBloki, ref int zmienioneObiektyWewnetrzne)
    {
        if (przetworzoneBloki.Contains(btrId)) return;
        BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
        if (btr.IsFromExternalReference || btr.IsDependent) return;

        btr.UpgradeOpen();
        przetworzoneBloki.Add(btrId);

        foreach (ObjectId innerId in btr)
        {
            Entity innerEnt = tr.GetObject(innerId, OpenMode.ForRead) as Entity;
            if (innerEnt != null)
            {
                // --- NOWOŚĆ: USUWANIE WYMIARÓW ---
                if (removeDimensions && innerEnt is Dimension)
                {
                    innerEnt.UpgradeOpen();
                    innerEnt.Erase(); // Fizyczne usunięcie wymiaru z bloku
                    zmienioneObiektyWewnetrzne++;
                    continue; // Pomiń dalszą edycję tego obiektu, bo już go nie ma
                }

                // Sprawdzamy filtr koloru (np. tylko czerwone obiekty wewnątrz bloku)
                if (filterColor.HasValue && innerEnt.ColorIndex != filterColor.Value) continue;

                bool modified = false;

                // Edycja tekstów (statyczne w bloku)
                if (!string.IsNullOrEmpty(findText) && replaceText != null)
                {
                    if (innerEnt is DBText dbText && dbText.TextString.Contains(findText))
                    {
                        innerEnt.UpgradeOpen();
                        dbText.TextString = dbText.TextString.Replace(findText, replaceText);
                        modified = true;
                    }
                    else if (innerEnt is MText mText && mText.Text.Contains(findText))
                    {
                        innerEnt.UpgradeOpen();
                        mText.Contents = mText.Contents.Replace(findText, replaceText);
                        modified = true;
                    }
                }

                // Edycja właściwości wspólnych (Kolor/Warstwa)
                if (targetColor.HasValue || targetLayer != null)
                {
                    if (!innerEnt.IsWriteEnabled) innerEnt.UpgradeOpen();
                    if (targetColor.HasValue) innerEnt.ColorIndex = targetColor.Value;
                    if (targetLayer != null) { try { innerEnt.Layer = targetLayer; } catch { } }
                    modified = true;
                }

                if (modified) zmienioneObiektyWewnetrzne++;

                // Incepcja - blok w bloku
                if (innerEnt is BlockReference nestedBr)
                {
                    // Tutaj również musimy przekazać removeDimensions!
                    PrzetworzBlokRekurencyjnie(nestedBr.BlockTableRecord, tr, targetColor, targetLayer, filterColor, findText, replaceText, removeDimensions, przetworzoneBloki, ref zmienioneObiektyWewnetrzne);
                }
            }
        }
    }
}