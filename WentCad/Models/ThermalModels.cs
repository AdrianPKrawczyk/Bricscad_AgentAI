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
