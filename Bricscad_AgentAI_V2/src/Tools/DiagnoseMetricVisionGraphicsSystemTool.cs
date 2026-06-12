using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class DiagnoseMetricVisionGraphicsSystemTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "DiagnoseMetricVisionGraphicsSystem",
                    Description = "Diagnozuje backend BricsCAD GraphicsSystem dla Metric Vision etapami, bez domyslnego wykonywania pelnego snapshotu.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Stage", new ToolParameter { Type = "string", Enum = new List<string> { "GraphicsManager", "CreateOffscreenDevice", "OffscreenOnSize", "GetDbModel", "CreateViewFromCurrentSpace", "CreateDeviceView", "DeviceAddManagerViewOnly", "DeviceInsertManagerViewOnly", "DeviceAddDeviceViewOnly", "ViewAddCurrentSpaceToDeviceView", "DeviceViewSetViewport", "DeviceViewZoomWindow", "DeviceViewShow", "DeviceViewUpdate", "DeviceViewSnapshot", "SetViewportOnManagerView", "ShowManagerView", "DeviceUpdate", "Snapshot" }, Description = "Pojedynczy etap diagnostyczny. Domyslnie GraphicsManager. Snapshot jest najbardziej ryzykowny." } },
                            { "Resolution", new ToolParameter { Type = "integer", Description = "Rozdzielczosc testowego urzadzenia off-screen. Domyslnie 256." } },
                            { "MinX", new ToolParameter { Type = "number", Description = "Minimalna wspolrzedna X testowego okna CAD. Domyslnie 0." } },
                            { "MinY", new ToolParameter { Type = "number", Description = "Minimalna wspolrzedna Y testowego okna CAD. Domyslnie 0." } },
                            { "MaxX", new ToolParameter { Type = "number", Description = "Maksymalna wspolrzedna X testowego okna CAD. Domyslnie 5000." } },
                            { "MaxY", new ToolParameter { Type = "number", Description = "Maksymalna wspolrzedna Y testowego okna CAD. Domyslnie 5000." } }
                        },
                        Required = new List<string>()
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string stage = args["Stage"]?.ToString() ?? "GraphicsManager";
            int resolution = args["Resolution"]?.Value<int?>() ?? 256;
            if (resolution < 64) resolution = 64;
            if (resolution > 2048) resolution = 2048;
            MetricVisionBounds bounds = new MetricVisionBounds
            {
                MinX = args["MinX"]?.Value<double?>() ?? 0.0,
                MinY = args["MinY"]?.Value<double?>() ?? 0.0,
                MaxX = args["MaxX"]?.Value<double?>() ?? 5000.0,
                MaxY = args["MaxY"]?.Value<double?>() ?? 5000.0
            };

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                JObject result = RunStage(doc, stage, resolution, bounds);
                result["status"] = "success";
                result["stage"] = stage;
                result["elapsed_ms"] = sw.ElapsedMilliseconds;
                return result.ToString(Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    status = "error",
                    stage = stage,
                    elapsed_ms = sw.ElapsedMilliseconds,
                    error_type = ex.GetType().FullName,
                    message = ex.Message,
                    stack = ex.StackTrace
                }, Formatting.Indented);
            }
        }

        private static JObject RunStage(Document doc, string stage, int resolution, MetricVisionBounds bounds)
        {
            if (doc == null) throw new InvalidOperationException("Brak aktywnego dokumentu CAD.");
            Bricscad.GraphicsSystem.Manager manager = doc.GraphicsManager;
            if (manager == null) throw new InvalidOperationException("Document.GraphicsManager zwrocil null.");

            JObject info = new JObject
            {
                ["document_name"] = doc.Name,
                ["manager_type"] = manager.GetType().FullName,
                ["cad_bounds"] = JObject.FromObject(bounds)
            };

            if (stage.Equals("GraphicsManager", StringComparison.OrdinalIgnoreCase))
            {
                return info;
            }

            if (stage.Equals("GetDbModel", StringComparison.OrdinalIgnoreCase))
            {
                Teigha.GraphicsSystem.Model model = manager.GetDBModel();
                info["model_created"] = model != null;
                info["model_type"] = model?.GetType().FullName;
                return info;
            }

            using (Teigha.GraphicsSystem.Device device = manager.CreateAutoCADOffScreenDevice())
            {
                if (device == null) throw new InvalidOperationException("CreateAutoCADOffScreenDevice zwrocil null.");
                info["device_created"] = true;
                info["device_type"] = device.GetType().FullName;
                info["device_is_valid_initial"] = device.IsValid;
                info["device_num_views_initial"] = device.NumViews;

                if (stage.Equals("CreateOffscreenDevice", StringComparison.OrdinalIgnoreCase))
                {
                    AddDeviceSizeInfo(info, device, "initial");
                    return info;
                }

                device.BackgroundColor = Color.White;
                device.OnSize(new Size(resolution, resolution));
                info["onsize_resolution"] = resolution;
                AddDeviceSizeInfo(info, device, "after_onsize");

                if (stage.Equals("OffscreenOnSize", StringComparison.OrdinalIgnoreCase))
                {
                    return info;
                }

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                    info["current_space_id"] = doc.Database.CurrentSpaceId.ToString();
                    info["current_space_name"] = btr.Name;

                    if (stage.Equals("CreateDeviceView", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Teigha.GraphicsSystem.View deviceView = device.CreateView())
                        {
                            if (deviceView == null) throw new InvalidOperationException("device.CreateView() zwrocil null.");
                            info["device_view_created"] = true;
                            info["device_view_type"] = deviceView.GetType().FullName;
                            info["device_view_is_valid_initial"] = deviceView.IsValid;
                            tr.Commit();
                            return info;
                        }
                    }

                    if (IsDeviceViewPipelineStage(stage))
                    {
                        using (Teigha.GraphicsSystem.View deviceView = device.CreateView())
                        {
                            if (deviceView == null) throw new InvalidOperationException("device.CreateView() zwrocil null.");
                            info["device_view_created"] = true;
                            info["device_view_type"] = deviceView.GetType().FullName;
                            info["device_view_is_valid_initial"] = deviceView.IsValid;

                            if (!stage.Equals("DeviceAddDeviceViewOnly", StringComparison.OrdinalIgnoreCase))
                            {
                                Teigha.GraphicsSystem.Model model = manager.GetDBModel();
                                info["db_model_created"] = model != null;
                                info["view_add_current_space_result"] = deviceView.Add(btr, model);
                            }

                            device.Add(deviceView);
                            info["device_num_views_after_add"] = device.NumViews;

                            if (stage.Equals("DeviceAddDeviceViewOnly", StringComparison.OrdinalIgnoreCase) ||
                                stage.Equals("ViewAddCurrentSpaceToDeviceView", StringComparison.OrdinalIgnoreCase))
                            {
                                tr.Commit();
                                return info;
                            }

                            deviceView.Viewport = new Extents2d(0.0, 0.0, 1.0, 1.0);
                            info["device_view_viewport_set"] = true;
                            info["device_view_viewport"] = "0,0,1,1";

                            if (stage.Equals("DeviceViewSetViewport", StringComparison.OrdinalIgnoreCase))
                            {
                                tr.Commit();
                                return info;
                            }

                            deviceView.ZoomWindow(new Teigha.Geometry.Point2d(bounds.MinX, bounds.MinY), new Teigha.Geometry.Point2d(bounds.MaxX, bounds.MaxY));
                            info["device_view_zoom_window_called"] = true;
                            info["device_view_zoom_min"] = $"{bounds.MinX},{bounds.MinY}";
                            info["device_view_zoom_max"] = $"{bounds.MaxX},{bounds.MaxY}";

                            if (stage.Equals("DeviceViewZoomWindow", StringComparison.OrdinalIgnoreCase))
                            {
                                tr.Commit();
                                return info;
                            }

                            deviceView.Show();
                            info["device_view_show_called"] = true;

                            if (stage.Equals("DeviceViewShow", StringComparison.OrdinalIgnoreCase))
                            {
                                tr.Commit();
                                return info;
                            }

                            device.Update();
                            info["device_update_called"] = true;

                            if (stage.Equals("DeviceViewUpdate", StringComparison.OrdinalIgnoreCase))
                            {
                                tr.Commit();
                                return info;
                            }

                            using (Bitmap snapshot = device.GetSnapshot(new Rectangle(0, 0, resolution, resolution)))
                            {
                                info["snapshot_created"] = snapshot != null;
                                info["snapshot_width"] = snapshot?.Width;
                                info["snapshot_height"] = snapshot?.Height;
                            }

                            tr.Commit();
                            return info;
                        }
                    }

                    using (Teigha.GraphicsSystem.View view = manager.CreateAutoCADView(btr))
                    {
                        if (view == null) throw new InvalidOperationException("CreateAutoCADView(CurrentSpace) zwrocil null.");
                        info["view_created"] = true;
                        info["view_type"] = view.GetType().FullName;
                        info["view_is_valid_initial"] = view.IsValid;

                        if (stage.Equals("CreateViewFromCurrentSpace", StringComparison.OrdinalIgnoreCase))
                        {
                            tr.Commit();
                            return info;
                        }

                        if (stage.Equals("DeviceAddManagerViewOnly", StringComparison.OrdinalIgnoreCase))
                        {
                            device.Add(view);
                            info["device_num_views_after_add"] = device.NumViews;
                            tr.Commit();
                            return info;
                        }

                        if (stage.Equals("DeviceInsertManagerViewOnly", StringComparison.OrdinalIgnoreCase))
                        {
                            device.InsertView(0, view);
                            info["device_num_views_after_insert"] = device.NumViews;
                            tr.Commit();
                            return info;
                        }

                        view.Viewport = new Extents2d(0.0, 0.0, 1.0, 1.0);
                        info["viewport_set"] = true;
                        info["viewport"] = "0,0,1,1";

                        if (stage.Equals("SetViewportOnManagerView", StringComparison.OrdinalIgnoreCase))
                        {
                            tr.Commit();
                            return info;
                        }

                        device.Add(view);
                        info["device_num_views_after_add"] = device.NumViews;
                        view.Show();
                        info["view_show_called"] = true;

                        if (stage.Equals("ShowManagerView", StringComparison.OrdinalIgnoreCase))
                        {
                            tr.Commit();
                            return info;
                        }

                        if (!stage.Equals("Snapshot", StringComparison.OrdinalIgnoreCase) &&
                            !stage.Equals("DeviceUpdate", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException("Nieznany Stage: " + stage);
                        }

                        device.Update();
                        info["device_update_called"] = true;

                        if (stage.Equals("DeviceUpdate", StringComparison.OrdinalIgnoreCase))
                        {
                            tr.Commit();
                            return info;
                        }

                        using (Bitmap snapshot = device.GetSnapshot(new Rectangle(0, 0, resolution, resolution)))
                        {
                            info["snapshot_created"] = snapshot != null;
                            info["snapshot_width"] = snapshot?.Width;
                            info["snapshot_height"] = snapshot?.Height;
                        }
                    }

                    tr.Commit();
                }
            }

            return info;
        }

        private static bool IsDeviceViewPipelineStage(string stage)
        {
            return stage.Equals("DeviceAddDeviceViewOnly", StringComparison.OrdinalIgnoreCase) ||
                   stage.Equals("ViewAddCurrentSpaceToDeviceView", StringComparison.OrdinalIgnoreCase) ||
                   stage.Equals("DeviceViewSetViewport", StringComparison.OrdinalIgnoreCase) ||
                   stage.Equals("DeviceViewZoomWindow", StringComparison.OrdinalIgnoreCase) ||
                   stage.Equals("DeviceViewShow", StringComparison.OrdinalIgnoreCase) ||
                   stage.Equals("DeviceViewUpdate", StringComparison.OrdinalIgnoreCase) ||
                   stage.Equals("DeviceViewSnapshot", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddDeviceSizeInfo(JObject info, Teigha.GraphicsSystem.Device device, string suffix)
        {
            try
            {
                Size size = device.GetSize();
                info["device_size_" + suffix] = $"{size.Width}x{size.Height}";
            }
            catch (Exception ex)
            {
                info["device_size_" + suffix + "_error"] = ex.Message;
            }

            try
            {
                Rectangle rect = device.GetSizeRect();
                info["device_rect_" + suffix] = $"{rect.Left},{rect.Top},{rect.Width},{rect.Height}";
            }
            catch (Exception ex)
            {
                info["device_rect_" + suffix + "_error"] = ex.Message;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Stage\": \"GraphicsManager\", \"Resolution\": 256 }",
            "{ \"Stage\": \"CreateOffscreenDevice\", \"Resolution\": 256 }",
            "{ \"Stage\": \"Snapshot\", \"Resolution\": 256 }"
        };
    }
}
