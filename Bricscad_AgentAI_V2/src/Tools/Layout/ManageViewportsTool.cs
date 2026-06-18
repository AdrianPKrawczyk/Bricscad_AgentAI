using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.Colors;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using CadLayout = Teigha.DatabaseServices.Layout;
using CadViewport = Teigha.DatabaseServices.Viewport;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class ManageViewportsTool : IToolV2
    {
        private const string DefaultViewportLayerName = "_rzutnie";
        private const short DefaultViewportLayerColor = 200;
        private static readonly string[] ValidActions = { "List", "Create", "Modify", "Delete" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageViewportsTool",
                    Description = "Listuje, tworzy, modyfikuje i usuwa rzutnie papierowe na layoutach. Ustawia zakres modelu Window XY, widok nazwany, skale rzutni, skale opisowa, blokade, widocznosc, warstwe ramki, clipping nieprostokatny oraz zamrazanie warstw per rzutnia.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Description = "Akcja: List, Create, Modify, Delete.", Enum = new List<string> { "List", "Create", "Modify", "Delete" } } },
                            { "LayoutName", new ToolParameter { Type = "string", Description = "Nazwa layoutu. Wymagane dla Create; dla List/Modify/Delete opcjonalne, domyslnie biezacy layout." } },
                            { "ViewportHandle", new ToolParameter { Type = "string", Description = "Handle rzutni papierowej do Modify/Delete. Alternatywa dla ViewportIndex." } },
                            { "ViewportIndex", new ToolParameter { Type = "integer", Description = "Indeks rzutni na arkuszu liczony od 1, bez systemowej rzutni papieru. Alternatywa dla ViewportHandle." } },
                            { "CenterPaperX", new ToolParameter { Type = "number", Description = "Srodek rzutni na arkuszu, wspolrzedna X w jednostkach papieru." } },
                            { "CenterPaperY", new ToolParameter { Type = "number", Description = "Srodek rzutni na arkuszu, wspolrzedna Y w jednostkach papieru." } },
                            { "WidthPaper", new ToolParameter { Type = "number", Description = "Szerokosc rzutni na arkuszu w jednostkach papieru." } },
                            { "HeightPaper", new ToolParameter { Type = "number", Description = "Wysokosc rzutni na arkuszu w jednostkach papieru." } },
                            { "ModelMinX", new ToolParameter { Type = "number", Description = "Zakres modelu Window XY: minimalne X." } },
                            { "ModelMinY", new ToolParameter { Type = "number", Description = "Zakres modelu Window XY: minimalne Y." } },
                            { "ModelMaxX", new ToolParameter { Type = "number", Description = "Zakres modelu Window XY: maksymalne X." } },
                            { "ModelMaxY", new ToolParameter { Type = "number", Description = "Zakres modelu Window XY: maksymalne Y." } },
                            { "NamedView", new ToolParameter { Type = "string", Description = "Opcjonalna nazwa widoku z tabeli widokow. Ustawia ViewCenter, ViewHeight, ViewDirection, ViewTarget i TwistAngle; jawne Window XY/Scale moga go nadpisac." } },
                            { "Scale", new ToolParameter { Type = "string", Description = "Skala rzutni: '1:50', '1_50', '1/50' albo liczba CustomScale np. '0.02'." } },
                            { "AnnotationScale", new ToolParameter { Type = "string", Description = "Nazwa skali opisowej, np. '1:50'. Musi istniec w kolekcji ACDB_ANNOTATIONSCALES." } },
                            { "Locked", new ToolParameter { Type = "boolean", Description = "Czy zablokowac rzutnie po konfiguracji." } },
                            { "On", new ToolParameter { Type = "boolean", Description = "Czy rzutnia ma byc wlaczona/widoczna." } },
                            { "TwistAngle", new ToolParameter { Type = "number", Description = "Kat obrotu widoku rzutni w radianach." } },
                            { "HiddenLinesRemoved", new ToolParameter { Type = "boolean", Description = "Czy ukrywac linie niewidoczne w rzutni." } },
                            { "Layer", new ToolParameter { Type = "string", Description = "Warstwa ramki rzutni. Dla Create domyslnie '_rzutnie' (kolor 200, brak druku)." } },
                            { "CreateLayerIfMissing", new ToolParameter { Type = "boolean", Description = "Czy utworzyc wskazana warstwe, jesli nie istnieje. Dla domyslnej warstwy '_rzutnie' narzedzie tworzy ja automatycznie." } },
                            { "FreezeLayers", new ToolParameter { Type = "array", Description = "Lista nazw warstw do zamrozenia tylko w tej rzutni.", Items = new JObject { ["type"] = "string" } } },
                            { "ThawLayers", new ToolParameter { Type = "array", Description = "Lista nazw warstw do odmrozenia tylko w tej rzutni.", Items = new JObject { ["type"] = "string" } } },
                            { "ThawAllLayers", new ToolParameter { Type = "boolean", Description = "Odmraza wszystkie warstwy zamrozone lokalnie w tej rzutni." } },
                            { "ClipBoundaryHandle", new ToolParameter { Type = "string", Description = "Handle istniejacego obiektu papierowego w tym samym layoucie, ktory ma byc granica nieprostokatnego clippingu." } },
                            { "ClipBoundaryPaperPoints", new ToolParameter { Type = "array", Description = "Lista punktow papieru granicy clippingu, np. [{\"X\":20,\"Y\":20},{\"X\":200,\"Y\":20},{\"X\":190,\"Y\":140}]. Narzedzie utworzy zamknieta polilinie.", Items = JObject.Parse("{\"type\":\"object\",\"properties\":{\"X\":{\"type\":\"number\"},\"Y\":{\"type\":\"number\"}}}") } },
                            { "RemoveNonRectClip", new ToolParameter { Type = "boolean", Description = "Wylacza nieprostokatny clipping rzutni." } },
                            { "OverwriteUnlocked", new ToolParameter { Type = "boolean", Description = "Dla Modify: pozwala tymczasowo odblokowac zablokowana rzutnie i zmienic parametry widoku/geometrii." } },
                            { "SaveAs", new ToolParameter { Type = "string", Description = "Dla List: nazwa zmiennej do zapisu wyniku w pamieci Agenta (bez @)." } }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "BLAD: Brak aktywnego dokumentu CAD.";

            string action = args["Action"]?.ToString();
            if (string.IsNullOrWhiteSpace(action))
            {
                return "BLAD: Parametr Action jest wymagany. Dozwolone: List, Create, Modify, Delete.";
            }

            string normalizedAction = ValidActions.FirstOrDefault(a => a.Equals(action, StringComparison.OrdinalIgnoreCase));
            if (normalizedAction == null)
            {
                return $"BLAD: Nieobslugiwana akcja '{action}'. Dozwolone: {string.Join(", ", ValidActions)}.";
            }

            Database db = doc.Database;
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    string result;
                    switch (normalizedAction)
                    {
                        case "List":
                            result = ExecuteList(db, tr, args);
                            break;
                        case "Create":
                            result = ExecuteCreate(db, tr, args);
                            break;
                        case "Modify":
                            result = ExecuteModify(db, tr, args);
                            break;
                        case "Delete":
                            result = ExecuteDelete(db, tr, args);
                            break;
                        default:
                            result = $"BLAD: Nieobslugiwana akcja '{action}'.";
                            break;
                    }

                    if (!result.StartsWith("BLAD", StringComparison.OrdinalIgnoreCase))
                    {
                        tr.Commit();
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                return $"BLAD KRYTYCZNY ManageViewportsTool: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        private string ExecuteList(Database db, Transaction tr, JObject args)
        {
            CadLayout layout = ResolveLayout(db, tr, args["LayoutName"]?.ToString(), false, out string layoutError);
            if (layout == null) return layoutError;

            List<CadViewport> viewports = GetPaperViewports(layout, tr);
            var sb = new StringBuilder();
            sb.AppendLine($"RZUTNIE PAPIEROWE layoutu '{layout.LayoutName}':");

            if (viewports.Count == 0)
            {
                sb.AppendLine("Brak rzutni papierowych (poza systemowa rzutnia papieru).");
            }
            else
            {
                for (int i = 0; i < viewports.Count; i++)
                {
                    CadViewport vp = viewports[i];
                    sb.AppendLine($"[{i + 1}] Handle={vp.Handle} Layout={layout.LayoutName}");
                    sb.AppendLine($"    Paper: Center=({Format(vp.CenterPoint.X)}, {Format(vp.CenterPoint.Y)}) Size={Format(vp.Width)}x{Format(vp.Height)}");
                    sb.AppendLine($"    Model: ViewCenter=({Format(vp.ViewCenter.X)}, {Format(vp.ViewCenter.Y)}) ViewHeight={Format(vp.ViewHeight)} CustomScale={Format(vp.CustomScale)}");
                    sb.AppendLine($"    AnnotationScale={GetAnnotationScaleName(vp)} Locked={vp.Locked} On={vp.On} Number={vp.Number} Layer={vp.Layer}");
                    sb.AppendLine($"    TwistAngle={Format(vp.TwistAngle)} HiddenLinesRemoved={vp.HiddenLinesRemoved} NonRectClipOn={vp.NonRectClipOn} ClipHandle={GetNonRectClipHandle(vp)}");
                    sb.AppendLine($"    FrozenLayers={GetFrozenLayerNames(vp, tr)}");
                }
            }

            string result = sb.ToString();
            string saveAs = args["SaveAs"]?.ToString();
            if (!string.IsNullOrWhiteSpace(saveAs))
            {
                AgentMemoryState.Variables[saveAs] = result;
            }
            return result;
        }

        private string ExecuteCreate(Database db, Transaction tr, JObject args)
        {
            string layoutName = args["LayoutName"]?.ToString();
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                return "BLAD: Create wymaga parametru LayoutName.";
            }

            CadLayout layout = ResolveLayout(db, tr, layoutName, true, out string layoutError);
            if (layout == null) return layoutError;

            if (!TryGetRequiredDouble(args, "CenterPaperX", out double centerX, out string error) ||
                !TryGetRequiredDouble(args, "CenterPaperY", out double centerY, out error) ||
                !TryGetRequiredDouble(args, "WidthPaper", out double width, out error) ||
                !TryGetRequiredDouble(args, "HeightPaper", out double height, out error))
            {
                return error;
            }

            if (width <= 0 || height <= 0)
            {
                return "BLAD: WidthPaper i HeightPaper musza byc wieksze od 0.";
            }

            string layer = ResolveCreateLayerName(args);
            bool createLayerIfMissing = TryGetBool(args, "CreateLayerIfMissing", false);
            if (!EnsureLayer(db, tr, layer, createLayerIfMissing, IsDefaultViewportLayer(layer), out error))
            {
                return error;
            }

            BlockTableRecord btr = tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
            if (btr == null)
            {
                return $"BLAD: Nie mozna otworzyc przestrzeni papieru layoutu '{layout.LayoutName}'.";
            }

            CadViewport vp = new CadViewport();
            vp.SetDatabaseDefaults();
            vp.CenterPoint = new Point3d(centerX, centerY, 0.0);
            vp.Width = width;
            vp.Height = height;
            vp.On = TryGetBool(args, "On", true);
            vp.Locked = false;
            vp.Layer = layer;

            ObjectId vpId = btr.AppendEntity(vp);
            tr.AddNewlyCreatedDBObject(vp, true);

            if (!ApplyViewportSettings(db, tr, vp, args, true, out error))
            {
                return error;
            }

            bool locked = TryGetBool(args, "Locked", false);
            vp.Locked = locked;
            TryUpdateDisplay(vp);

            return $"SUKCES: Utworzono rzutnie papierowa na layoucie '{layout.LayoutName}'. Index={GetViewportIndex(layout, tr, vpId)}, Handle={vp.Handle}, Size={Format(vp.Width)}x{Format(vp.Height)}, CustomScale={Format(vp.CustomScale)}, Locked={vp.Locked}.";
        }

        private string ExecuteModify(Database db, Transaction tr, JObject args)
        {
            CadLayout layout = ResolveLayout(db, tr, args["LayoutName"]?.ToString(), false, out string layoutError);
            if (layout == null) return layoutError;

            CadViewport vp = ResolveViewport(db, tr, layout, args, OpenMode.ForWrite, out int index, out string error);
            if (vp == null) return error;

            bool overwriteUnlocked = TryGetBool(args, "OverwriteUnlocked", false);
            bool wasLocked = vp.Locked;
            bool hasProtectedChange = HasProtectedViewportChange(args);
            if (wasLocked && hasProtectedChange && !overwriteUnlocked)
            {
                return $"BLAD: Rzutnia [{index}] Handle={vp.Handle} jest zablokowana. Podaj OverwriteUnlocked=true albo zmien tylko Locked/On.";
            }

            if (wasLocked && overwriteUnlocked)
            {
                vp.Locked = false;
            }

            if (!ApplyViewportSettings(db, tr, vp, args, false, out error))
            {
                if (wasLocked && overwriteUnlocked) vp.Locked = wasLocked;
                return error;
            }

            if (HasArg(args, "Locked"))
            {
                vp.Locked = TryGetBool(args, "Locked", vp.Locked);
            }
            else if (wasLocked && overwriteUnlocked)
            {
                vp.Locked = true;
            }

            TryUpdateDisplay(vp);
            return $"SUKCES: Zmieniono rzutnie [{index}] Handle={vp.Handle} na layoucie '{layout.LayoutName}'. CustomScale={Format(vp.CustomScale)}, Locked={vp.Locked}, On={vp.On}.";
        }

        private string ExecuteDelete(Database db, Transaction tr, JObject args)
        {
            CadLayout layout = ResolveLayout(db, tr, args["LayoutName"]?.ToString(), false, out string layoutError);
            if (layout == null) return layoutError;

            CadViewport vp = ResolveViewport(db, tr, layout, args, OpenMode.ForWrite, out int index, out string error);
            if (vp == null) return error;
            if (IsSystemPaperViewport(vp))
            {
                return "BLAD: Nie mozna usunac systemowej rzutni papieru.";
            }

            string handle = vp.Handle.ToString();
            vp.Erase();
            return $"SUKCES: Usunieto rzutnie papierowa [{index}] Handle={handle} z layoutu '{layout.LayoutName}'.";
        }

        private static CadLayout ResolveLayout(Database db, Transaction tr, string layoutName, bool requireExplicit, out string error)
        {
            error = null;
            if (requireExplicit && string.IsNullOrWhiteSpace(layoutName))
            {
                error = "BLAD: LayoutName jest wymagany.";
                return null;
            }

            CadLayout layout = LayoutHelpers.GetLayoutByNameOrCurrent(db, tr, layoutName);
            if (layout == null)
            {
                error = $"BLAD: Layout '{(string.IsNullOrWhiteSpace(layoutName) ? "<biezacy>" : layoutName)}' nie istnieje.";
                return null;
            }

            if (LayoutHelpers.IsModelLayout(layout))
            {
                error = "BLAD: Rzutnie papierowe mozna tworzyc i edytowac tylko na layoutach papierowych, nie w 'Model'.";
                return null;
            }

            return layout;
        }

        private static List<CadViewport> GetPaperViewports(CadLayout layout, Transaction tr)
        {
            var result = new List<CadViewport>();
            BlockTableRecord btr = tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return result;

            foreach (ObjectId id in btr)
            {
                CadViewport vp = tr.GetObject(id, OpenMode.ForRead) as CadViewport;
                if (vp == null) continue;
                if (IsSystemPaperViewport(vp)) continue;
                result.Add(vp);
            }

            return result;
        }

        private static CadViewport ResolveViewport(Database db, Transaction tr, CadLayout layout, JObject args, OpenMode mode, out int index, out string error)
        {
            index = -1;
            error = null;

            string handleText = args["ViewportHandle"]?.ToString();
            if (!string.IsNullOrWhiteSpace(handleText))
            {
                if (!long.TryParse(handleText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long handleValue))
                {
                    error = $"BLAD: Niepoprawny format ViewportHandle '{handleText}'. Podaj handle w formacie hex.";
                    return null;
                }

                ObjectId objId;
                try
                {
                    objId = db.GetObjectId(false, new Handle(handleValue), 0);
                }
                catch
                {
                    error = $"BLAD: Rzutnia o handle '{handleText}' nie istnieje.";
                    return null;
                }

                if (objId.IsNull || objId.IsErased)
                {
                    error = $"BLAD: Rzutnia o handle '{handleText}' nie istnieje.";
                    return null;
                }

                CadViewport vp = tr.GetObject(objId, mode) as CadViewport;
                if (vp == null)
                {
                    error = $"BLAD: Handle '{handleText}' nie wskazuje na rzutnie.";
                    return null;
                }

                if (vp.OwnerId != layout.BlockTableRecordId)
                {
                    error = $"BLAD: Rzutnia Handle={handleText} nie nalezy do layoutu '{layout.LayoutName}'.";
                    return null;
                }

                if (IsSystemPaperViewport(vp))
                {
                    error = "BLAD: Wskazana rzutnia to systemowa rzutnia papieru. Nie wolno jej modyfikowac ani usuwac tym narzedziem.";
                    return null;
                }

                index = GetViewportIndex(layout, tr, objId);
                return vp;
            }

            if (!TryGetInt(args, "ViewportIndex", out int requestedIndex))
            {
                error = "BLAD: Modify/Delete wymaga ViewportHandle albo ViewportIndex.";
                return null;
            }

            List<ObjectId> viewportIds = GetPaperViewportIds(layout, tr);
            if (requestedIndex < 1 || requestedIndex > viewportIds.Count)
            {
                error = $"BLAD: ViewportIndex={requestedIndex} poza zakresem. Na layoucie '{layout.LayoutName}' jest {viewportIds.Count} rzutni papierowych.";
                return null;
            }

            index = requestedIndex;
            return tr.GetObject(viewportIds[requestedIndex - 1], mode) as CadViewport;
        }

        private static List<ObjectId> GetPaperViewportIds(CadLayout layout, Transaction tr)
        {
            var result = new List<ObjectId>();
            BlockTableRecord btr = tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return result;

            foreach (ObjectId id in btr)
            {
                CadViewport vp = tr.GetObject(id, OpenMode.ForRead) as CadViewport;
                if (vp != null && !IsSystemPaperViewport(vp)) result.Add(id);
            }
            return result;
        }

        private static bool IsSystemPaperViewport(CadViewport vp)
        {
            return vp != null && vp.Number == 1;
        }

        private static int GetViewportIndex(CadLayout layout, Transaction tr, ObjectId viewportId)
        {
            List<ObjectId> ids = GetPaperViewportIds(layout, tr);
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == viewportId) return i + 1;
            }
            return -1;
        }

        private static bool ApplyViewportSettings(Database db, Transaction tr, CadViewport vp, JObject args, bool isCreate, out string error)
        {
            error = null;
            if (!ValidateOptionalNumbers(args, out error))
            {
                return false;
            }

            if (TryGetOptionalDouble(args, "CenterPaperX", out double cx) && TryGetOptionalDouble(args, "CenterPaperY", out double cy))
            {
                vp.CenterPoint = new Point3d(cx, cy, vp.CenterPoint.Z);
            }
            else if (!isCreate && (HasArg(args, "CenterPaperX") || HasArg(args, "CenterPaperY")))
            {
                error = "BLAD: CenterPaperX i CenterPaperY musza byc podane razem.";
                return false;
            }

            if (TryGetOptionalDouble(args, "WidthPaper", out double width))
            {
                if (width <= 0) { error = "BLAD: WidthPaper musi byc wieksze od 0."; return false; }
                vp.Width = width;
            }

            if (TryGetOptionalDouble(args, "HeightPaper", out double height))
            {
                if (height <= 0) { error = "BLAD: HeightPaper musi byc wieksze od 0."; return false; }
                vp.Height = height;
            }

            bool hasScale = HasArg(args, "Scale");
            double customScale = 0.0;
            if (hasScale)
            {
                string scaleText = args["Scale"]?.ToString();
                if (!TryParseViewportScale(scaleText, out customScale))
                {
                    error = $"BLAD: Niepoprawna skala '{scaleText}'. Uzyj np. '1:50', '1_50', '1/50' albo '0.02'.";
                    return false;
                }
            }

            if (HasArg(args, "NamedView"))
            {
                string viewName = args["NamedView"]?.ToString();
                if (!ApplyNamedView(db, tr, vp, viewName, out error))
                {
                    return false;
                }
            }

            bool hasWindow = HasModelWindow(args);
            if (hasWindow)
            {
                if (!TryReadModelWindow(args, out double minX, out double minY, out double maxX, out double maxY, out error))
                {
                    return false;
                }

                double modelWidth = Math.Abs(maxX - minX);
                double modelHeight = Math.Abs(maxY - minY);
                double modelCenterX = (minX + maxX) / 2.0;
                double modelCenterY = (minY + maxY) / 2.0;

                vp.ViewCenter = new Point2d(modelCenterX, modelCenterY);

                if (hasScale)
                {
                    vp.CustomScale = customScale;
                    vp.ViewHeight = vp.Height / customScale;
                }
                else
                {
                    double viewportAspect = vp.Width / vp.Height;
                    double viewHeight = Math.Max(modelHeight, modelWidth / viewportAspect);
                    vp.ViewHeight = viewHeight;
                    if (viewHeight > 0.0)
                    {
                        vp.CustomScale = vp.Height / viewHeight;
                    }
                }
            }
            else if (hasScale)
            {
                vp.CustomScale = customScale;
                if (customScale > 0.0 && vp.Height > 0.0)
                {
                    vp.ViewHeight = vp.Height / customScale;
                }
            }

            if (HasArg(args, "AnnotationScale"))
            {
                string annotationScaleName = args["AnnotationScale"]?.ToString();
                if (!SetAnnotationScale(db, vp, annotationScaleName, out error))
                {
                    return false;
                }
            }

            if (TryGetOptionalDouble(args, "TwistAngle", out double twistAngle))
            {
                vp.TwistAngle = twistAngle;
            }

            if (HasArg(args, "HiddenLinesRemoved"))
            {
                vp.HiddenLinesRemoved = TryGetBool(args, "HiddenLinesRemoved", vp.HiddenLinesRemoved);
            }

            if (HasArg(args, "On"))
            {
                vp.On = TryGetBool(args, "On", vp.On);
            }

            if (HasArg(args, "Layer"))
            {
                string layer = args["Layer"]?.ToString();
                bool createLayerIfMissing = TryGetBool(args, "CreateLayerIfMissing", false);
                if (!EnsureLayer(db, tr, layer, createLayerIfMissing, IsDefaultViewportLayer(layer), out error))
                {
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(layer)) vp.Layer = layer;
            }

            if (TryGetBool(args, "RemoveNonRectClip", false))
            {
                vp.NonRectClipOn = false;
            }

            if (HasArg(args, "ClipBoundaryHandle"))
            {
                string handle = args["ClipBoundaryHandle"]?.ToString();
                if (!SetClipBoundaryFromHandle(db, tr, vp, handle, out error))
                {
                    return false;
                }
            }

            if (HasArg(args, "ClipBoundaryPaperPoints"))
            {
                if (!SetClipBoundaryFromPaperPoints(tr, vp, args["ClipBoundaryPaperPoints"], out error))
                {
                    return false;
                }
            }

            if (TryGetBool(args, "ThawAllLayers", false))
            {
                vp.ThawAllLayersInViewport();
            }

            if (HasArg(args, "ThawLayers"))
            {
                if (!TryReadStringArray(args["ThawLayers"], out List<string> thawLayers, out error) ||
                    !ApplyViewportLayerThaw(db, tr, vp, thawLayers, out error))
                {
                    return false;
                }
            }

            if (HasArg(args, "FreezeLayers"))
            {
                if (!TryReadStringArray(args["FreezeLayers"], out List<string> freezeLayers, out error) ||
                    !ApplyViewportLayerFreeze(db, tr, vp, freezeLayers, out error))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ApplyNamedView(Database db, Transaction tr, CadViewport vp, string viewName, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(viewName))
            {
                error = "BLAD: NamedView nie moze byc puste.";
                return false;
            }

            ViewTable viewTable = tr.GetObject(db.ViewTableId, OpenMode.ForRead) as ViewTable;
            if (viewTable == null || !viewTable.Has(viewName))
            {
                error = $"BLAD: Widok nazwany '{viewName}' nie istnieje w tym rysunku.";
                return false;
            }

            ViewTableRecord view = tr.GetObject(viewTable[viewName], OpenMode.ForRead) as ViewTableRecord;
            if (view == null)
            {
                error = $"BLAD: Nie mozna odczytac widoku nazwanego '{viewName}'.";
                return false;
            }

            vp.ViewCenter = view.CenterPoint;
            if (view.Height > 0.0)
            {
                vp.ViewHeight = view.Height;
                if (vp.Height > 0.0)
                {
                    vp.CustomScale = vp.Height / view.Height;
                }
            }

            vp.ViewTarget = view.Target;
            vp.ViewDirection = view.ViewDirection;
            vp.TwistAngle = view.ViewTwist;

            try
            {
                if (view.AnnotationScale != null)
                {
                    vp.AnnotationScale = view.AnnotationScale;
                }
            }
            catch
            {
                // BricsCAD may expose views without a usable annotation scale.
            }

            return true;
        }

        private static bool SetAnnotationScale(Database db, CadViewport vp, string scaleName, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(scaleName))
            {
                error = "BLAD: AnnotationScale nie moze byc puste.";
                return false;
            }

            ObjectContextManager ocm = db.ObjectContextManager;
            ObjectContextCollection occ = ocm?.GetContextCollection("ACDB_ANNOTATIONSCALES");
            if (occ == null)
            {
                error = "BLAD: Rysunek nie udostepnia kolekcji ACDB_ANNOTATIONSCALES.";
                return false;
            }

            ObjectContext ctx = occ.GetContext(scaleName);
            AnnotationScale annotationScale = ctx as AnnotationScale;
            if (annotationScale == null)
            {
                error = $"BLAD: Skala opisowa '{scaleName}' nie istnieje w tym rysunku.";
                return false;
            }

            vp.AnnotationScale = annotationScale;
            return true;
        }

        private static bool ApplyViewportLayerFreeze(Database db, Transaction tr, CadViewport vp, List<string> layerNames, out string error)
        {
            error = null;
            if (!ResolveLayerIds(db, tr, layerNames, out ObjectIdCollection layerIds, out error))
            {
                return false;
            }

            if (layerIds.Count > 0)
            {
                vp.FreezeLayersInViewport(layerIds.GetEnumerator());
            }
            return true;
        }

        private static bool ApplyViewportLayerThaw(Database db, Transaction tr, CadViewport vp, List<string> layerNames, out string error)
        {
            error = null;
            if (!ResolveLayerIds(db, tr, layerNames, out ObjectIdCollection layerIds, out error))
            {
                return false;
            }

            if (layerIds.Count > 0)
            {
                vp.ThawLayersInViewport(layerIds.GetEnumerator());
            }
            return true;
        }

        private static bool ResolveLayerIds(Database db, Transaction tr, List<string> layerNames, out ObjectIdCollection layerIds, out string error)
        {
            layerIds = new ObjectIdCollection();
            error = null;

            if (layerNames == null || layerNames.Count == 0)
            {
                return true;
            }

            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt == null)
            {
                error = "BLAD: Nie mozna odczytac tabeli warstw.";
                return false;
            }

            foreach (string rawName in layerNames)
            {
                string layerName = rawName?.Trim();
                if (string.IsNullOrWhiteSpace(layerName)) continue;

                if (!lt.Has(layerName))
                {
                    error = $"BLAD: Warstwa '{layerName}' nie istnieje. Nie mozna ustawic zamrozenia/odmrozenia per rzutnia.";
                    return false;
                }

                layerIds.Add(lt[layerName]);
            }

            return true;
        }

        private static bool SetClipBoundaryFromHandle(Database db, Transaction tr, CadViewport vp, string handleText, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(handleText))
            {
                error = "BLAD: ClipBoundaryHandle nie moze byc pusty.";
                return false;
            }

            if (!long.TryParse(handleText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long handleValue))
            {
                error = $"BLAD: Niepoprawny format ClipBoundaryHandle '{handleText}'. Podaj handle w formacie hex.";
                return false;
            }

            ObjectId objId;
            try
            {
                objId = db.GetObjectId(false, new Handle(handleValue), 0);
            }
            catch
            {
                error = $"BLAD: Obiekt clippingu o handle '{handleText}' nie istnieje.";
                return false;
            }

            Entity clipEntity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
            if (clipEntity == null)
            {
                error = $"BLAD: ClipBoundaryHandle '{handleText}' nie wskazuje na obiekt rysunkowy.";
                return false;
            }

            if (clipEntity.OwnerId != vp.OwnerId)
            {
                error = "BLAD: Granica clippingu musi lezec w tej samej przestrzeni papieru co rzutnia.";
                return false;
            }

            if (clipEntity is CadViewport)
            {
                error = "BLAD: Granica clippingu nie moze byc rzutnia.";
                return false;
            }

            Polyline polyline = clipEntity as Polyline;
            if (polyline != null && !polyline.Closed)
            {
                error = "BLAD: Polilinia granicy clippingu musi byc zamknieta.";
                return false;
            }

            vp.NonRectClipEntityId = objId;
            vp.NonRectClipOn = true;
            return true;
        }

        private static bool SetClipBoundaryFromPaperPoints(Transaction tr, CadViewport vp, JToken token, out string error)
        {
            error = null;
            if (!TryReadPaperPoints(token, out List<Point2d> points, out error))
            {
                return false;
            }

            BlockTableRecord btr = tr.GetObject(vp.OwnerId, OpenMode.ForWrite) as BlockTableRecord;
            if (btr == null)
            {
                error = "BLAD: Nie mozna otworzyc przestrzeni papieru rzutni do utworzenia granicy clippingu.";
                return false;
            }

            Polyline boundary = new Polyline();
            boundary.SetDatabaseDefaults();
            boundary.Layer = string.IsNullOrWhiteSpace(vp.Layer) ? DefaultViewportLayerName : vp.Layer;
            for (int i = 0; i < points.Count; i++)
            {
                boundary.AddVertexAt(i, points[i], 0.0, 0.0, 0.0);
            }
            boundary.Closed = true;

            ObjectId boundaryId = btr.AppendEntity(boundary);
            tr.AddNewlyCreatedDBObject(boundary, true);

            vp.NonRectClipEntityId = boundaryId;
            vp.NonRectClipOn = true;
            return true;
        }

        private static bool TryReadPaperPoints(JToken token, out List<Point2d> points, out string error)
        {
            points = new List<Point2d>();
            error = null;

            JArray array = token as JArray;
            if (array == null)
            {
                error = "BLAD: ClipBoundaryPaperPoints musi byc tablica punktow.";
                return false;
            }

            foreach (JToken item in array)
            {
                double x;
                double y;

                JArray pair = item as JArray;
                if (pair != null)
                {
                    if (pair.Count < 2 || !TryParseInvariant(pair[0].ToString(), out x) || !TryParseInvariant(pair[1].ToString(), out y))
                    {
                        error = "BLAD: Kazdy punkt ClipBoundaryPaperPoints jako tablica musi miec liczby [X,Y].";
                        return false;
                    }
                    points.Add(new Point2d(x, y));
                    continue;
                }

                JObject obj = item as JObject;
                if (obj == null ||
                    !HasArg(obj, "X") ||
                    !HasArg(obj, "Y") ||
                    !TryParseInvariant(obj["X"].ToString(), out x) ||
                    !TryParseInvariant(obj["Y"].ToString(), out y))
                {
                    error = "BLAD: Kazdy punkt ClipBoundaryPaperPoints musi miec liczby X i Y.";
                    return false;
                }

                points.Add(new Point2d(x, y));
            }

            if (points.Count < 3)
            {
                error = "BLAD: ClipBoundaryPaperPoints wymaga co najmniej 3 punktow.";
                return false;
            }

            return true;
        }

        private static bool TryReadStringArray(JToken token, out List<string> values, out string error)
        {
            values = new List<string>();
            error = null;

            if (token == null || token.Type == JTokenType.Null)
            {
                return true;
            }

            JArray array = token as JArray;
            if (array != null)
            {
                foreach (JToken item in array)
                {
                    string value = item?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
                }
                return true;
            }

            string text = token.ToString();
            if (string.IsNullOrWhiteSpace(text)) return true;

            values.AddRange(text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v)));
            return true;
        }

        private static bool EnsureLayer(Database db, Transaction tr, string layerName, bool createIfMissing, bool forceDefaultViewportProperties, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(layerName)) return true;

            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt == null)
            {
                error = "BLAD: Nie mozna odczytac tabeli warstw.";
                return false;
            }

            if (lt.Has(layerName))
            {
                if (forceDefaultViewportProperties)
                {
                    LayerTableRecord existing = tr.GetObject(lt[layerName], OpenMode.ForWrite) as LayerTableRecord;
                    if (existing != null)
                    {
                        existing.Color = Color.FromColorIndex(ColorMethod.ByAci, DefaultViewportLayerColor);
                        existing.IsPlottable = false;
                    }
                }
                return true;
            }

            if (forceDefaultViewportProperties)
            {
                createIfMissing = true;
            }

            if (!createIfMissing)
            {
                error = $"BLAD: Warstwa '{layerName}' nie istnieje. Podaj CreateLayerIfMissing=true albo wybierz istniejaca warstwe.";
                return false;
            }

            lt.UpgradeOpen();
            LayerTableRecord layer = new LayerTableRecord { Name = layerName };
            if (forceDefaultViewportProperties)
            {
                layer.Color = Color.FromColorIndex(ColorMethod.ByAci, DefaultViewportLayerColor);
                layer.IsPlottable = false;
            }
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
            return true;
        }

        private static string ResolveCreateLayerName(JObject args)
        {
            string layer = args["Layer"]?.ToString();
            return string.IsNullOrWhiteSpace(layer) ? DefaultViewportLayerName : layer;
        }

        private static bool IsDefaultViewportLayer(string layerName)
        {
            return string.Equals(layerName, DefaultViewportLayerName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryParseViewportScale(string input, out double scale)
        {
            scale = 0.0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string text = input.Trim();
            char separator = text.Contains(":") ? ':' : (text.Contains("_") ? '_' : (text.Contains("/") ? '/' : '\0'));
            if (separator != '\0')
            {
                string[] parts = text.Split(separator);
                if (parts.Length != 2) return false;
                if (!TryParseInvariant(parts[0], out double numerator)) return false;
                if (!TryParseInvariant(parts[1], out double denominator)) return false;
                if (numerator <= 0.0 || denominator <= 0.0) return false;
                scale = numerator / denominator;
                return scale > 0.0;
            }

            if (!TryParseInvariant(text, out double direct)) return false;
            if (direct <= 0.0) return false;
            scale = direct;
            return true;
        }

        private static bool TryReadModelWindow(JObject args, out double minX, out double minY, out double maxX, out double maxY, out string error)
        {
            minX = minY = maxX = maxY = 0.0;
            error = null;
            string[] names = { "ModelMinX", "ModelMinY", "ModelMaxX", "ModelMaxY" };
            foreach (string name in names)
            {
                if (!HasArg(args, name))
                {
                    error = "BLAD: Zakres modelu Window XY wymaga ModelMinX, ModelMinY, ModelMaxX, ModelMaxY.";
                    return false;
                }
            }

            if (!TryGetRequiredDouble(args, "ModelMinX", out minX, out error) ||
                !TryGetRequiredDouble(args, "ModelMinY", out minY, out error) ||
                !TryGetRequiredDouble(args, "ModelMaxX", out maxX, out error) ||
                !TryGetRequiredDouble(args, "ModelMaxY", out maxY, out error))
            {
                return false;
            }

            if (Math.Abs(maxX - minX) < 1e-9 || Math.Abs(maxY - minY) < 1e-9)
            {
                error = "BLAD: Zakres modelu musi miec niezerowa szerokosc i wysokosc.";
                return false;
            }

            return true;
        }

        private static bool HasModelWindow(JObject args)
        {
            return HasArg(args, "ModelMinX") || HasArg(args, "ModelMinY") || HasArg(args, "ModelMaxX") || HasArg(args, "ModelMaxY");
        }

        private static bool HasProtectedViewportChange(JObject args)
        {
            string[] protectedArgs =
            {
                "CenterPaperX", "CenterPaperY", "WidthPaper", "HeightPaper",
                "ModelMinX", "ModelMinY", "ModelMaxX", "ModelMaxY",
                "Scale", "AnnotationScale", "TwistAngle", "HiddenLinesRemoved",
                "Layer", "CreateLayerIfMissing", "NamedView",
                "FreezeLayers", "ThawLayers", "ThawAllLayers",
                "ClipBoundaryHandle", "ClipBoundaryPaperPoints", "RemoveNonRectClip"
            };

            return protectedArgs.Any(name => HasArg(args, name));
        }

        private static bool ValidateOptionalNumbers(JObject args, out string error)
        {
            error = null;
            string[] numberArgs =
            {
                "CenterPaperX", "CenterPaperY", "WidthPaper", "HeightPaper",
                "ModelMinX", "ModelMinY", "ModelMaxX", "ModelMaxY",
                "TwistAngle"
            };

            foreach (string name in numberArgs)
            {
                if (HasArg(args, name) && !TryParseInvariant(args[name].ToString(), out double _))
                {
                    error = $"BLAD: Parametr {name} musi byc liczba.";
                    return false;
                }
            }

            return true;
        }

        private static bool HasArg(JObject args, string name)
        {
            JToken token = args[name];
            return token != null && token.Type != JTokenType.Null;
        }

        private static bool TryGetRequiredDouble(JObject args, string name, out double value, out string error)
        {
            value = 0.0;
            error = null;
            if (!HasArg(args, name))
            {
                error = $"BLAD: Parametr {name} jest wymagany.";
                return false;
            }

            string text = args[name].ToString();
            if (!TryParseInvariant(text, out value))
            {
                error = $"BLAD: Parametr {name} musi byc liczba.";
                return false;
            }
            return true;
        }

        private static bool TryGetOptionalDouble(JObject args, string name, out double value)
        {
            value = 0.0;
            if (!HasArg(args, name)) return false;
            return TryParseInvariant(args[name].ToString(), out value);
        }

        private static bool TryParseInvariant(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string trimmed = text.Trim();
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            if (trimmed.IndexOf(',') >= 0 && trimmed.IndexOf('.') < 0)
            {
                string normalized = trimmed.Replace(',', '.');
                return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }

            return false;
        }

        private static bool TryGetBool(JObject args, string name, bool defaultValue)
        {
            if (!HasArg(args, name)) return defaultValue;
            try { return args[name].Value<bool>(); }
            catch
            {
                bool parsed;
                if (bool.TryParse(args[name].ToString(), out parsed)) return parsed;
                return defaultValue;
            }
        }

        private static bool TryGetInt(JObject args, string name, out int value)
        {
            value = 0;
            if (!HasArg(args, name)) return false;
            return int.TryParse(args[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string GetAnnotationScaleName(CadViewport vp)
        {
            try { return vp.AnnotationScale != null ? vp.AnnotationScale.Name : "(brak)"; }
            catch { return "(brak)"; }
        }

        private static string GetNonRectClipHandle(CadViewport vp)
        {
            try
            {
                return vp.NonRectClipEntityId.IsNull ? "(brak)" : vp.NonRectClipEntityId.Handle.ToString();
            }
            catch { return "(brak)"; }
        }

        private static string GetFrozenLayerNames(CadViewport vp, Transaction tr)
        {
            try
            {
                ObjectIdCollection ids = vp.GetFrozenLayers();
                if (ids == null || ids.Count == 0) return "(brak)";

                var names = new List<string>();
                foreach (ObjectId id in ids)
                {
                    LayerTableRecord layer = tr.GetObject(id, OpenMode.ForRead) as LayerTableRecord;
                    if (layer != null) names.Add(layer.Name);
                }

                return names.Count == 0 ? "(brak)" : string.Join(", ", names);
            }
            catch { return "(nie mozna odczytac)"; }
        }

        private static void TryUpdateDisplay(CadViewport vp)
        {
            try { vp.UpdateDisplay(); } catch { }
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public List<string> Examples => new List<string>
        {
            "{\"Action\":\"List\",\"LayoutName\":\"01\"}",
            "{\"Action\":\"Create\",\"LayoutName\":\"01\",\"CenterPaperX\":148.5,\"CenterPaperY\":105,\"WidthPaper\":180,\"HeightPaper\":120,\"ModelMinX\":0,\"ModelMinY\":0,\"ModelMaxX\":9000,\"ModelMaxY\":6000,\"Scale\":\"1:50\",\"AnnotationScale\":\"1:50\",\"Locked\":true}",
            "{\"Action\":\"Modify\",\"LayoutName\":\"01\",\"ViewportIndex\":1,\"Scale\":\"1:100\",\"OverwriteUnlocked\":true,\"Locked\":true}",
            "{\"Action\":\"Modify\",\"LayoutName\":\"01\",\"ViewportIndex\":1,\"NamedView\":\"Rzut parteru\",\"FreezeLayers\":[\"A-MEBLE\"],\"OverwriteUnlocked\":true,\"Locked\":true}",
            "{\"Action\":\"Create\",\"LayoutName\":\"01\",\"CenterPaperX\":148.5,\"CenterPaperY\":105,\"WidthPaper\":180,\"HeightPaper\":120,\"ClipBoundaryPaperPoints\":[{\"X\":60,\"Y\":45},{\"X\":230,\"Y\":45},{\"X\":220,\"Y\":160},{\"X\":60,\"Y\":150}],\"Locked\":true}",
            "{\"Action\":\"Delete\",\"LayoutName\":\"01\",\"ViewportHandle\":\"1A2B\"}"
        };
    }
}
