using System;
using System.Collections.Generic;

namespace WentCad.Models
{
    public class ThermalModel
    {
        public Dictionary<string, ThermalWallDef> Walls { get; set; } = new Dictionary<string, ThermalWallDef>();
        public Dictionary<string, ThermalWindowDef> Windows { get; set; } = new Dictionary<string, ThermalWindowDef>();
        public Dictionary<string, ThermalHorizontalDef> HorizontalPartitions { get; set; } = new Dictionary<string, ThermalHorizontalDef>();
        public ThermalSettings Settings { get; set; } = new ThermalSettings();
        public ThermalCatalog Catalog { get; set; } = new ThermalCatalog();
    }

    public class ThermalCatalog
    {
        public Dictionary<string, ThermalMaterialDef> Materials { get; set; } = new Dictionary<string, ThermalMaterialDef>();
        public Dictionary<string, ThermalLayerSetDef> LayerSets { get; set; } = new Dictionary<string, ThermalLayerSetDef>();
        public Dictionary<string, ThermalConstructionDef> Constructions { get; set; } = new Dictionary<string, ThermalConstructionDef>();
        public Dictionary<string, ThermalOpeningStyleDef> OpeningStyles { get; set; } = new Dictionary<string, ThermalOpeningStyleDef>();
    }

    public class ThermalMaterialDef
    {
        public string MaterialId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public double ThermalConductivity { get; set; }
        public double MassDensity { get; set; }
        public double SpecificHeatCapacity { get; set; }
    }

    public class ThermalMaterialLayerDef
    {
        public string LayerId { get; set; } = Guid.NewGuid().ToString();
        public string MaterialId { get; set; } = "";
        public double Thickness { get; set; }
    }

    public class ThermalLayerSetDef
    {
        public string LayerSetId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public List<ThermalMaterialLayerDef> Layers { get; set; } = new List<ThermalMaterialLayerDef>();
    }

    public class ThermalConstructionDef
    {
        public string ConstructionId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Code { get; set; } = "SZ";
        public string LayerSetId { get; set; } = "";
        public string PredefinedType { get; set; } = "STANDARD";
        public bool IsExternal { get; set; } = true;
        public string ThermalType { get; set; } = "WALL";
        public bool IsGroundContact { get; set; }
        public bool IsDefault { get; set; }
        public string DefaultAssignMode { get; set; } = "ALL";
        public double DefaultTolerancePlus { get; set; } = 0.05;
        public double DefaultToleranceMinus { get; set; } = 0.04;
        public double UValue { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? ConstructionId : $"{Code} - {Name}";
    }

    public class ThermalOpeningStyleDef
    {
        public string StyleId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string OpeningKind { get; set; } = "WINDOW";
        public double OverallUValue { get; set; } = 1.1;
        public double SolarHeatGainCoefficient { get; set; } = 0.5;

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? StyleId : $"{OpeningKind} - {Name}";
    }

    public class ThermalSettings
    {
        public string WallLayer { get; set; } = "";
        public string WindowLayer { get; set; } = "";
        public string WindowBlockNamePattern { get; set; } = "";
        public string WindowLabelLayer { get; set; } = "";
        public string WindowLabelBlockNamePattern { get; set; } = "";
        public string WindowWidthAttribute { get; set; } = "WIDTH";
        public string WindowHeightAttribute { get; set; } = "HEIGHT";
        public string WindowSillAttribute { get; set; } = "SILL";
        public string DoorLayer { get; set; } = "";
        public string DoorBlockNamePattern { get; set; } = "";
        public string DoorLabelLayer { get; set; } = "";
        public string DoorLabelBlockNamePattern { get; set; } = "";
        public string DoorWidthAttribute { get; set; } = "WIDTH";
        public string DoorHeightAttribute { get; set; } = "HEIGHT";
        public string DoorSillAttribute { get; set; } = "SILL";
        public double MaxInteriorWallDistance { get; set; } = 0.6;
        public double MaxExteriorConfirmDistance { get; set; } = 1.2;
        public double MaxWindowSnapDistance { get; set; } = 1.0;
        public double DefaultWindowWidth { get; set; } = 1.2;
        public double DefaultWindowHeight { get; set; } = 1.5;
        public double DefaultWindowSillHeight { get; set; } = 0.9;
        public double DefaultDoorWidth { get; set; } = 0.9;
        public double DefaultDoorHeight { get; set; } = 2.0;
        public double DefaultDoorSillHeight { get; set; } = 0.0;
    }

    public class ThermalWallDef
    {
        public string WallId { get; set; } = Guid.NewGuid().ToString();
        public string FloorId { get; set; } = "";
        public string RoomId { get; set; } = "";
        public string Kind { get; set; } = "UNRESOLVED";
        public string Code { get; set; } = "";
        public string ConstructionId { get; set; } = "";
        public string ConstructionName { get; set; } = "";
        public PointDto P1 { get; set; } = new PointDto();
        public PointDto P2 { get; set; } = new PointDto();
        public double Length { get; set; }
        public double Height { get; set; }
        public double GrossArea { get; set; }
        public double Thickness { get; set; }
        public double Azimuth { get; set; }
        public string AdjacentRoomId { get; set; } = "";
        public string SourceRoomBoundaryHandle { get; set; } = "";
        public double Confidence { get; set; }
        public string Message { get; set; } = "";
    }

    public class ThermalWindowDef
    {
        public string WindowId { get; set; } = Guid.NewGuid().ToString();
        public string FloorId { get; set; } = "";
        public string RoomId { get; set; } = "";
        public string WallId { get; set; } = "";
        public string BlockHandle { get; set; } = "";
        public string LabelHandle { get; set; } = "";
        public string BlockName { get; set; } = "";
        public string OpeningKind { get; set; } = "WINDOW";
        public string Code { get; set; } = "OZ";
        public string ConstructionId { get; set; } = "";
        public string ConstructionName { get; set; } = "";
        public double Width { get; set; }
        public double Height { get; set; }
        public double SillHeight { get; set; }
        public double Area { get; set; }
        public double Placement { get; set; }
        public double Confidence { get; set; }
        public string Message { get; set; } = "";
    }

    public class ThermalHorizontalDef
    {
        public string PartitionId { get; set; } = Guid.NewGuid().ToString();
        public string FloorId { get; set; } = "";
        public string RoomId { get; set; } = "";
        public string Kind { get; set; } = "FLOOR_INTERIOR";
        public string Code { get; set; } = "StW";
        public string ConstructionId { get; set; } = "";
        public string ConstructionName { get; set; } = "";
        public double Area { get; set; }
        public double Confidence { get; set; } = 0.8;
        public string Message { get; set; } = "";
    }
}
