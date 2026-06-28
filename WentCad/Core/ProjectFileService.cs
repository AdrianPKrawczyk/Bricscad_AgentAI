using System;
using System.IO;
using Bricscad.ApplicationServices;
using Newtonsoft.Json;
using WentCad.Models;

namespace WentCad.Core
{
    public static class ProjectFileService
    {
        public static string GetProjectPath(Document doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Name) || !Path.IsPathRooted(doc.Name))
            {
                string fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WentCad");
                Directory.CreateDirectory(fallback);
                return Path.Combine(fallback, "Untitled.wentcad");
            }

            string dir = Path.GetDirectoryName(doc.Name);
            string name = Path.GetFileNameWithoutExtension(doc.Name);
            return Path.Combine(dir, name + ".wentcad");
        }

        public static WentCadProject LoadOrCreate(Document doc)
        {
            string path = GetProjectPath(doc);
            if (File.Exists(path))
            {
                var loaded = JsonConvert.DeserializeObject<WentCadProject>(File.ReadAllText(path));
                if (loaded != null)
                {
                    loaded.DwgPath = doc?.Name ?? loaded.DwgPath;
                    EnsureDefaults(loaded);
                    return loaded;
                }
            }

            var project = new WentCadProject
            {
                DwgPath = doc?.Name ?? "",
                ProjectName = Path.GetFileNameWithoutExtension(doc?.Name ?? "WentCad")
            };
            project.Systems.Add(new SystemDef { SystemId = "N1", Name = "Nawiew 1", Type = "SUPPLY", ColorIndex = 5 });
            project.Systems.Add(new SystemDef { SystemId = "W1", Name = "Wywiew 1", Type = "EXHAUST", ColorIndex = 1 });
            var floor = new FloorDef { FloorId = Guid.NewGuid().ToString(), Name = "Parter", Order = 0 };
            project.Floors[floor.FloorId] = floor;
            Save(doc, project);
            return project;
        }

        public static void Save(Document doc, WentCadProject project)
        {
            EnsureDefaults(project);
            project.UpdatedAt = DateTime.UtcNow;
            string path = GetProjectPath(doc);
            string json = JsonConvert.SerializeObject(project, Formatting.Indented);
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", true);
            }
            File.WriteAllText(path, json);
        }

        private static void EnsureDefaults(WentCadProject project)
        {
            if (project.Floors == null) project.Floors = new System.Collections.Generic.Dictionary<string, FloorDef>();
            if (project.Rooms == null) project.Rooms = new System.Collections.Generic.Dictionary<string, RoomDef>();
            if (project.Systems == null) project.Systems = new System.Collections.Generic.List<SystemDef>();
            if (project.DetectionMapping == null) project.DetectionMapping = new RoomDetectionMapping();
            if (project.Thermal == null) project.Thermal = new ThermalModel();
            if (project.Thermal.Walls == null) project.Thermal.Walls = new System.Collections.Generic.Dictionary<string, ThermalWallDef>();
            if (project.Thermal.Windows == null) project.Thermal.Windows = new System.Collections.Generic.Dictionary<string, ThermalWindowDef>();
            if (project.Thermal.HorizontalPartitions == null) project.Thermal.HorizontalPartitions = new System.Collections.Generic.Dictionary<string, ThermalHorizontalDef>();
            if (project.Thermal.Settings == null) project.Thermal.Settings = new ThermalSettings();
            foreach (var wall in project.Thermal.Walls.Values)
            {
                if (string.IsNullOrWhiteSpace(wall.Code)) wall.Code = wall.Kind == "EXTERNAL" ? "SZ" : wall.Kind == "INTERNAL" ? "SW" : "?";
            }
            foreach (var window in project.Thermal.Windows.Values)
            {
                if (string.IsNullOrWhiteSpace(window.OpeningKind)) window.OpeningKind = "WINDOW";
                if (string.IsNullOrWhiteSpace(window.Code)) window.Code = window.OpeningKind == "DOOR" ? "DRZ" : "OZ";
            }
            foreach (var partition in project.Thermal.HorizontalPartitions.Values)
            {
                if (string.IsNullOrWhiteSpace(partition.Code))
                {
                    partition.Code = partition.Kind == "FLOOR_GROUND" ? "PG" :
                        (partition.Kind == "ROOF" || partition.Kind == "FLOOR_EXTERIOR") ? "D" :
                        (partition.Kind == "CEILING_INTERIOR" || partition.Kind == "FLOOR_INTERIOR") ? "StW" : "?";
                }
            }
            if (project.Systems.Count == 0)
            {
                project.Systems.Add(new SystemDef { SystemId = "N1", Name = "Nawiew 1", Type = "SUPPLY", ColorIndex = 5 });
                project.Systems.Add(new SystemDef { SystemId = "W1", Name = "Wywiew 1", Type = "EXHAUST", ColorIndex = 1 });
            }
        }
    }
}
