using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Teigha.Colors;

namespace BricsCAD_Agent
{
    public class ManageLayersTool : ITool
    {
        public string ActionTag => "[ACTION:MANAGE_LAYERS]";
        public string Description => "Zarządza warstwami (Tworzenie, Modyfikacja, Usuwanie, Łączenie, Purge).";

        public string Execute(Document doc, string jsonArgs)
        {
            string cleanArgs = jsonArgs.Replace("\\\"", "\"").Replace("\\n", "\n");

            string mode = "Modify";
            Match mMode = Regex.Match(cleanArgs, @"""Mode""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (mMode.Success) mode = mMode.Groups[1].Value;

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    LayerTable lt = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForWrite) as LayerTable;

                    // ==========================================
                    // 1. TWORZENIE I MODYFIKACJA (Obsługa wielu warstw)
                    // ==========================================
                    if (mode.Equals("Create", StringComparison.OrdinalIgnoreCase) || mode.Equals("Modify", StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> targetLayers = new List<string>();
                        
                        // Zczytuje z "Layer" (nawet jeśli to lista CSV z Checkboxów)
                        Match mLayer = Regex.Match(cleanArgs, @"""Layer""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                        if (mLayer.Success)
                        {
                            string[] parts = mLayer.Groups[1].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string p in parts) targetLayers.Add(p.Trim());
                        }

                        // Alternatywny zapis JSON: "Layers": ["Warstwa1", "Warstwa2"]
                        Match mLayersArr = Regex.Match(cleanArgs, @"""Layers""\s*:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                        if (mLayersArr.Success)
                        {
                            MatchCollection mc = Regex.Matches(mLayersArr.Groups[1].Value, @"""([^""]+)""");
                            foreach (Match m in mc) targetLayers.Add(m.Groups[1].Value);
                        }

                        if (targetLayers.Count == 0) return "WYNIK: BŁĄD - Nie podano warstw do operacji w kluczu 'Layer'.";

                        int successCount = 0;
                        List<string> failed = new List<string>();

                        foreach (string layerName in targetLayers)
                        {
                            LayerTableRecord ltr = null;
                            bool isNew = false;

                            if (mode.Equals("Create", StringComparison.OrdinalIgnoreCase))
                            {
                                if (lt.Has(layerName)) { failed.Add($"{layerName} (już istnieje)"); continue; }
                                ltr = new LayerTableRecord();
                                ltr.Name = layerName;
                                isNew = true;
                            }
                            else 
                            {
                                if (!lt.Has(layerName)) { failed.Add($"{layerName} (nie istnieje)"); continue; }
                                ltr = tr.GetObject(lt[layerName], OpenMode.ForWrite) as LayerTableRecord;
                            }

                            // Zmiana nazwy działa tylko, gdy edytujemy jedną warstwę na raz!
                            Match mNewName = Regex.Match(cleanArgs, @"""NewName""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (mNewName.Success && !isNew)
                            {
                                if (targetLayers.Count > 1)
                                {
                                    failed.Add($"{layerName} (nie można zmienić nazwy przy modyfikacji masowej)");
                                }
                                else
                                {
                                    string nn = mNewName.Groups[1].Value;

                                    // --- NOWE: Ewaluacja RPN dla nowej nazwy warstwy ---
                                    if (nn.StartsWith("RPN:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            nn = RpnCalculator.Evaluate(nn.Substring(4).Trim());
                                        }
                                        catch (Exception ex)
                                        {
                                            failed.Add($"{layerName} (błąd RPN: {ex.Message})");
                                            continue;
                                        }
                                    }
                                    // ---------------------------------------------------

                                    if (layerName == "0" || layerName.Equals("Defpoints", StringComparison.OrdinalIgnoreCase))
                                    { failed.Add($"{layerName} (warstwa systemowa)"); continue; }

                                    if (lt.Has(nn)) { failed.Add($"{layerName} -> {nn} (nazwa zajęta)"); continue; }
                                    ltr.Name = nn;
                                }
                            }

                            Match mColor = Regex.Match(cleanArgs, @"""Color""\s*:\s*(\d+)");
                            if (mColor.Success) ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, short.Parse(mColor.Groups[1].Value));

                            Match mLineWeight = Regex.Match(cleanArgs, @"""LineWeight""\s*:\s*(-?\d+)");
                            if (mLineWeight.Success) ltr.LineWeight = (LineWeight)int.Parse(mLineWeight.Groups[1].Value);

                            Match mLT = Regex.Match(cleanArgs, @"""Linetype""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (mLT.Success)
                            {
                                LinetypeTable ltt = tr.GetObject(doc.Database.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                                if (ltt.Has(mLT.Groups[1].Value)) ltr.LinetypeObjectId = ltt[mLT.Groups[1].Value];
                            }

                            if (Regex.IsMatch(cleanArgs, @"""IsOff""\s*:\s*(true|""true"")", RegexOptions.IgnoreCase)) ltr.IsOff = true;
                            else if (Regex.IsMatch(cleanArgs, @"""IsOff""\s*:\s*(false|""false"")", RegexOptions.IgnoreCase)) ltr.IsOff = false;

                            if (Regex.IsMatch(cleanArgs, @"""IsFrozen""\s*:\s*(true|""true"")", RegexOptions.IgnoreCase)) ltr.IsFrozen = true;
                            else if (Regex.IsMatch(cleanArgs, @"""IsFrozen""\s*:\s*(false|""false"")", RegexOptions.IgnoreCase)) ltr.IsFrozen = false;

                            if (Regex.IsMatch(cleanArgs, @"""IsLocked""\s*:\s*(true|""true"")", RegexOptions.IgnoreCase)) ltr.IsLocked = true;
                            else if (Regex.IsMatch(cleanArgs, @"""IsLocked""\s*:\s*(false|""false"")", RegexOptions.IgnoreCase)) ltr.IsLocked = false;

                            Match mTrans = Regex.Match(cleanArgs, @"""Transparency""\s*:\s*(\d+)");
                            if (mTrans.Success)
                            {
                                byte alpha = (byte)(255 - (int.Parse(mTrans.Groups[1].Value) * 255 / 100));
                                ltr.Transparency = new Transparency(alpha);
                            }

                            if (isNew) { lt.Add(ltr); tr.AddNewlyCreatedDBObject(ltr, true); }
                            successCount++;
                        }

                        tr.Commit();
                        if (failed.Count > 0) return $"WYNIK: Przetworzono {successCount} warstw. BŁĄDY (zignorowano): {string.Join(", ", failed)}";
                        return mode.Equals("Create", StringComparison.OrdinalIgnoreCase) ? $"WYNIK: Pomyślnie utworzono {successCount} warstw(y)." : $"WYNIK: Pomyślnie zaktualizowano {successCount} warstw.";
                    }

                    // ==========================================
                    // 2. CZYSZCZENIE (PURGE) - Usuwa nieużywane
                    // ==========================================
                    else if (mode.Equals("Purge", StringComparison.OrdinalIgnoreCase))
                    {
                        ObjectIdCollection layerIds = new ObjectIdCollection();
                        foreach (ObjectId ltrId in lt)
                        {
                            LayerTableRecord ltr = tr.GetObject(ltrId, OpenMode.ForRead) as LayerTableRecord;
                            if (ltr.Name != "0" && !ltr.Name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase) && ltrId != doc.Database.Clayer)
                            {
                                layerIds.Add(ltrId);
                            }
                        }
                        
                        doc.Database.Purge(layerIds); 
                        
                        int purgedCount = 0;
                        foreach (ObjectId id in layerIds)
                        {
                            LayerTableRecord ltrToDel = tr.GetObject(id, OpenMode.ForWrite) as LayerTableRecord;
                            ltrToDel.Erase(true);
                            purgedCount++;
                        }
                        tr.Commit();
                        return $"WYNIK: Pomyślnie wyczyszczono (Purge) {purgedCount} nieużywanych warstw z rysunku.";
                    }

                    // ==========================================
                    // 3. USUWANIE I ŁĄCZENIE (MERGE)
                    // ==========================================
                    else if (mode.Equals("Delete", StringComparison.OrdinalIgnoreCase) || mode.Equals("Merge", StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> sourceLayers = new List<string>();
                        Match mSourceArray = Regex.Match(cleanArgs, @"""SourceLayers""\s*:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                        if (mSourceArray.Success)
                        {
                            MatchCollection optionMatches = Regex.Matches(mSourceArray.Groups[1].Value, @"""([^""]+)""");
                            foreach (Match m in optionMatches) sourceLayers.Add(m.Groups[1].Value);
                        }
                        if (sourceLayers.Count == 0) return "WYNIK: BŁĄD - Nie podano warstw źródłowych w 'SourceLayers'.";

                        bool isMerge = mode.Equals("Merge", StringComparison.OrdinalIgnoreCase);
                        string targetLayer = "";

                        if (isMerge)
                        {
                            Match mTarget = Regex.Match(cleanArgs, @"""TargetLayer""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (mTarget.Success) targetLayer = mTarget.Groups[1].Value;
                            if (string.IsNullOrEmpty(targetLayer)) return "WYNIK: BŁĄD - W trybie Merge musisz podać 'TargetLayer'.";
                            
                            if (!lt.Has(targetLayer)) 
                            {
                                LayerTableRecord newTarget = new LayerTableRecord();
                                newTarget.Name = targetLayer;
                                lt.Add(newTarget);
                                tr.AddNewlyCreatedDBObject(newTarget, true);
                            }
                        }

                        int movedEntities = 0, deletedLayers = 0;
                        List<string> failedLayers = new List<string>();

                        if (isMerge)
                        {
                            BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                            foreach (ObjectId btrId in bt)
                            {
                                BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                                foreach (ObjectId entId in btr)
                                {
                                    Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                    if (ent != null && sourceLayers.Exists(s => s.Equals(ent.Layer, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        ent.UpgradeOpen();
                                        ent.Layer = targetLayer;
                                        movedEntities++;
                                    }
                                }
                            }
                        }

                        string activeLayer = doc.Database.Clayer.ObjectClass.Name; 
                        LayerTableRecord activeLtr = tr.GetObject(doc.Database.Clayer, OpenMode.ForRead) as LayerTableRecord;

                        foreach (string sLayer in sourceLayers)
                        {
                            if (isMerge && sLayer.Equals(targetLayer, StringComparison.OrdinalIgnoreCase)) continue; 
                            if (sLayer == "0" || sLayer.Equals("Defpoints", StringComparison.OrdinalIgnoreCase)) continue; 
                            if (activeLtr != null && sLayer.Equals(activeLtr.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                failedLayers.Add($"{sLayer} (aktywna)");
                                continue;
                            }

                            if (lt.Has(sLayer))
                            {
                                try
                                {
                                    LayerTableRecord ltrToDel = tr.GetObject(lt[sLayer], OpenMode.ForWrite) as LayerTableRecord;
                                    ltrToDel.Erase(true);
                                    deletedLayers++;
                                }
                                catch { failedLayers.Add(sLayer); }
                            }
                        }

                        tr.Commit();

                        if (isMerge)
                        {
                            string failMsg = failedLayers.Count > 0 ? $" (Nie usunięto: {string.Join(", ", failedLayers)})" : "";
                            return $"WYNIK: Przeniesiono {movedEntities} obiektów na '{targetLayer}'. Usunięto {deletedLayers} warstw źródłowych{failMsg}.";
                        }
                        else
                        {
                            if (failedLayers.Count == 0) return $"WYNIK: Pomyślnie usunięto {deletedLayers} warstw.";
                            else return $"WYNIK: Usunięto {deletedLayers} warstw. BŁĄD przy usuwaniu: {string.Join(", ", failedLayers)}. Upewnij się, że są puste!";
                        }
                    }

                    return $"WYNIK: BŁĄD - Nieznany tryb '{mode}'.";
                }
            }
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}