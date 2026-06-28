using System;
using System.Linq;
using WentCad.Models;

namespace WentCad.Core
{
    public static class ThermalCatalogService
    {
        public static void EnsureDefaults(WentCadProject project)
        {
            if (project?.Thermal == null) return;
            if (project.Thermal.Catalog == null) project.Thermal.Catalog = new ThermalCatalog();
            var catalog = project.Thermal.Catalog;
            if (catalog.Materials == null) catalog.Materials = new System.Collections.Generic.Dictionary<string, ThermalMaterialDef>();
            if (catalog.LayerSets == null) catalog.LayerSets = new System.Collections.Generic.Dictionary<string, ThermalLayerSetDef>();
            if (catalog.Constructions == null) catalog.Constructions = new System.Collections.Generic.Dictionary<string, ThermalConstructionDef>();
            if (catalog.OpeningStyles == null) catalog.OpeningStyles = new System.Collections.Generic.Dictionary<string, ThermalOpeningStyleDef>();

            AddMaterial(catalog, "mat-concrete-std", "Beton zwykly", "Mury i konstrukcja", 1.7, 2400, 1000);
            AddMaterial(catalog, "mat-concrete-light", "Beton lekki / gazobeton 600", "Mury i konstrukcja", 0.17, 600, 840);
            AddMaterial(catalog, "mat-brick-solid", "Cegla ceramiczna pelna", "Mury i konstrukcja", 0.77, 1800, 880);
            AddMaterial(catalog, "mat-brick-hollow", "Pustak ceramiczny", "Mury i konstrukcja", 0.22, 800, 1000);
            AddMaterial(catalog, "mat-eps-std", "Styropian EPS 040", "Izolacje", 0.04, 15, 1460);
            AddMaterial(catalog, "mat-eps-graphite", "Styropian grafitowy 031", "Izolacje", 0.031, 15, 1460);
            AddMaterial(catalog, "mat-wool-std", "Welna mineralna 035", "Izolacje", 0.035, 40, 1030);
            AddMaterial(catalog, "mat-pir", "Plyty PIR", "Izolacje", 0.022, 30, 1400);
            AddMaterial(catalog, "mat-plaster-gypsum", "Tynk gipsowy", "Wykonczenie", 0.4, 1000, 1000);
            AddMaterial(catalog, "mat-plaster-cement", "Tynk cementowo-wapienny", "Wykonczenie", 0.82, 1850, 840);
            AddMaterial(catalog, "mat-wood-soft", "Drewno iglaste", "Drewno", 0.13, 500, 2510);
            AddMaterial(catalog, "mat-air-gap", "Pustka powietrzna niewentylowana", "Inne", 0.08, 1.2, 1005);

            AddLayerSet(catalog, "ls-sz-standard", "Sciana zewn. standard", new[]
            {
                Layer("mat-plaster-gypsum", 0.015),
                Layer("mat-brick-hollow", 0.25),
                Layer("mat-eps-graphite", 0.15),
                Layer("mat-plaster-cement", 0.015)
            });
            AddLayerSet(catalog, "ls-sw-standard", "Sciana wewn. 12 cm", new[]
            {
                Layer("mat-plaster-gypsum", 0.015),
                Layer("mat-brick-hollow", 0.12),
                Layer("mat-plaster-gypsum", 0.015)
            });
            AddLayerSet(catalog, "ls-pg-standard", "Podloga na gruncie standard", new[]
            {
                Layer("mat-concrete-std", 0.10),
                Layer("mat-eps-std", 0.12),
                Layer("mat-concrete-std", 0.08)
            });
            AddLayerSet(catalog, "ls-stw-standard", "Strop wewn. zelbet", new[]
            {
                Layer("mat-plaster-gypsum", 0.015),
                Layer("mat-concrete-std", 0.20)
            });
            AddLayerSet(catalog, "ls-d-standard", "Dach/strop zewn. standard", new[]
            {
                Layer("mat-plaster-gypsum", 0.015),
                Layer("mat-concrete-std", 0.20),
                Layer("mat-wool-std", 0.25)
            });

            AddConstruction(catalog, "con-sz-standard", "SZ", "Sciana zewn. standard", "ls-sz-standard", "WALL", true, false, true, "BY_THICKNESS", 0.04, 0.08);
            AddConstruction(catalog, "con-sw-standard", "SW", "Sciana wewn. 12 cm", "ls-sw-standard", "WALL", false, false, true, "BY_THICKNESS", 0.03, 0.03);
            AddConstruction(catalog, "con-pg-standard", "PG", "Podloga na gruncie standard", "ls-pg-standard", "FLOOR", true, true, true, "ALL", 0.00, 0.00);
            AddConstruction(catalog, "con-stw-standard", "StW", "Strop wewn. zelbet", "ls-stw-standard", "FLOOR", false, false, true, "ALL", 0.00, 0.00);
            AddConstruction(catalog, "con-d-standard", "D", "Dach/strop zewn. standard", "ls-d-standard", "ROOF", true, false, true, "ALL", 0.00, 0.00);

            if (catalog.OpeningStyles.Count == 0)
            {
                AddOpeningStyle(catalog, "win-default", "Okno referencyjne", "WINDOW", 0.9, 0.5);
                AddOpeningStyle(catalog, "door-default", "Drzwi zewnetrzne referencyjne", "DOOR", 1.3, 0);
            }

            RecalculateUValues(project);
            ApplyReferences(project);
        }

        public static void RecalculateUValues(WentCadProject project)
        {
            var catalog = project?.Thermal?.Catalog;
            if (catalog?.Constructions == null) return;
            foreach (var construction in catalog.Constructions.Values)
            {
                construction.UValue = CalculateUValue(project, construction);
            }
        }

        public static double CalculateUValue(WentCadProject project, ThermalConstructionDef construction)
        {
            if (project?.Thermal?.Catalog == null || construction == null) return 0;
            if (!project.Thermal.Catalog.LayerSets.TryGetValue(construction.LayerSetId ?? "", out ThermalLayerSetDef layerSet)) return 0;
            if (layerSet?.Layers == null || layerSet.Layers.Count == 0) return 0;

            double rsi = 0.13;
            if (string.Equals(construction.ThermalType, "ROOF", StringComparison.OrdinalIgnoreCase)) rsi = 0.10;
            if (string.Equals(construction.ThermalType, "FLOOR", StringComparison.OrdinalIgnoreCase)) rsi = 0.17;
            double rse = construction.IsGroundContact ? 0 : construction.IsExternal ? 0.04 : rsi;

            double layersR = 0;
            foreach (var layer in layerSet.Layers)
            {
                if (layer == null) continue;
                if (!project.Thermal.Catalog.Materials.TryGetValue(layer.MaterialId ?? "", out ThermalMaterialDef material)) continue;
                if (material.ThermalConductivity <= 0 || layer.Thickness <= 0) continue;
                layersR += layer.Thickness / material.ThermalConductivity;
            }

            double totalR = rsi + layersR + rse;
            if (totalR <= 0) return 0;
            return Math.Round(1.0 / totalR, 3);
        }

        public static string GetLayerSetSummary(WentCadProject project, string layerSetId)
        {
            var catalog = project?.Thermal?.Catalog;
            if (catalog == null || !catalog.LayerSets.TryGetValue(layerSetId ?? "", out ThermalLayerSetDef layerSet)) return "";
            if (layerSet?.Layers == null) return "";
            return string.Join(" + ", layerSet.Layers.Select(l =>
            {
                catalog.Materials.TryGetValue(l.MaterialId ?? "", out ThermalMaterialDef mat);
                string name = mat?.Name ?? l.MaterialId;
                return $"{name} {l.Thickness:0.###} m";
            }));
        }

        public static void ApplyReferences(WentCadProject project)
        {
            var catalog = project?.Thermal?.Catalog;
            if (project?.Thermal == null || catalog == null) return;

            foreach (var wall in project.Thermal.Walls.Values)
            {
                if (string.IsNullOrWhiteSpace(wall.ConstructionId))
                {
                    var construction = FindDefaultConstruction(catalog, wall.Code);
                    if (construction != null) wall.ConstructionId = construction.ConstructionId;
                }
                if (!string.IsNullOrWhiteSpace(wall.ConstructionId) &&
                    catalog.Constructions.TryGetValue(wall.ConstructionId, out ThermalConstructionDef matched))
                {
                    wall.ConstructionName = matched.Name;
                }
            }

            foreach (var partition in project.Thermal.HorizontalPartitions.Values)
            {
                if (string.IsNullOrWhiteSpace(partition.ConstructionId))
                {
                    var construction = FindDefaultConstruction(catalog, partition.Code);
                    if (construction != null) partition.ConstructionId = construction.ConstructionId;
                }
                if (!string.IsNullOrWhiteSpace(partition.ConstructionId) &&
                    catalog.Constructions.TryGetValue(partition.ConstructionId, out ThermalConstructionDef matched))
                {
                    partition.ConstructionName = matched.Name;
                }
            }

            foreach (var opening in project.Thermal.Windows.Values)
            {
                if (string.IsNullOrWhiteSpace(opening.ConstructionId))
                {
                    var style = FindDefaultOpeningStyle(catalog, opening.OpeningKind);
                    if (style != null) opening.ConstructionId = style.StyleId;
                }
                if (!string.IsNullOrWhiteSpace(opening.ConstructionId) &&
                    catalog.OpeningStyles.TryGetValue(opening.ConstructionId, out ThermalOpeningStyleDef matched))
                {
                    opening.ConstructionName = matched.Name;
                }
            }
        }

        private static void AddMaterial(ThermalCatalog catalog, string id, string name, string category, double lambda, double density, double heat)
        {
            if (catalog.Materials.ContainsKey(id)) return;
            catalog.Materials[id] = new ThermalMaterialDef
            {
                MaterialId = id,
                Name = name,
                Category = category,
                ThermalConductivity = lambda,
                MassDensity = density,
                SpecificHeatCapacity = heat
            };
        }

        private static void AddOpeningStyle(ThermalCatalog catalog, string id, string name, string kind, double uValue, double gValue)
        {
            if (catalog.OpeningStyles.ContainsKey(id)) return;
            catalog.OpeningStyles[id] = new ThermalOpeningStyleDef
            {
                StyleId = id,
                Name = name,
                OpeningKind = kind,
                OverallUValue = uValue,
                SolarHeatGainCoefficient = gValue
            };
        }

        private static ThermalMaterialLayerDef Layer(string materialId, double thickness)
        {
            return new ThermalMaterialLayerDef
            {
                LayerId = Guid.NewGuid().ToString(),
                MaterialId = materialId,
                Thickness = thickness
            };
        }

        private static void AddLayerSet(ThermalCatalog catalog, string id, string name, ThermalMaterialLayerDef[] layers)
        {
            if (catalog.LayerSets.ContainsKey(id)) return;
            var layerSet = new ThermalLayerSetDef
            {
                LayerSetId = id,
                Name = name
            };
            if (layers != null) layerSet.Layers.AddRange(layers);
            catalog.LayerSets[id] = layerSet;
        }

        private static void AddConstruction(
            ThermalCatalog catalog,
            string id,
            string code,
            string name,
            string layerSetId,
            string thermalType,
            bool isExternal,
            bool isGroundContact,
            bool isDefault,
            string defaultAssignMode,
            double toleranceMinus,
            double tolerancePlus)
        {
            if (catalog.Constructions.ContainsKey(id)) return;
            catalog.Constructions[id] = new ThermalConstructionDef
            {
                ConstructionId = id,
                Code = code,
                Name = name,
                LayerSetId = layerSetId,
                PredefinedType = "STANDARD",
                ThermalType = thermalType,
                IsExternal = isExternal,
                IsGroundContact = isGroundContact,
                IsDefault = isDefault,
                DefaultAssignMode = defaultAssignMode,
                DefaultToleranceMinus = toleranceMinus,
                DefaultTolerancePlus = tolerancePlus
            };
        }

        private static ThermalConstructionDef FindDefaultConstruction(ThermalCatalog catalog, string code)
        {
            if (catalog?.Constructions == null || string.IsNullOrWhiteSpace(code)) return null;
            return catalog.Constructions.Values
                       .FirstOrDefault(c => c.IsDefault && string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                   ?? catalog.Constructions.Values
                       .FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        private static ThermalOpeningStyleDef FindDefaultOpeningStyle(ThermalCatalog catalog, string kind)
        {
            if (catalog?.OpeningStyles == null) return null;
            string normalized = string.Equals(kind, "DOOR", StringComparison.OrdinalIgnoreCase) ? "DOOR" : "WINDOW";
            return catalog.OpeningStyles.Values
                .FirstOrDefault(s => string.Equals(s.OpeningKind, normalized, StringComparison.OrdinalIgnoreCase));
        }
    }
}
