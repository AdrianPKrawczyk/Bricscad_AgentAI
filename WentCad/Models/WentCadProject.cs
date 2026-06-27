using System;
using System.Collections.Generic;

namespace WentCad.Models
{
    public class WentCadProject
    {
        public int SchemaVersion { get; set; } = 1;
        public Guid ProjectId { get; set; } = Guid.NewGuid();
        public string DwgPath { get; set; } = "";
        public string ProjectName { get; set; } = "WentCad";
        public Dictionary<string, FloorDef> Floors { get; set; } = new Dictionary<string, FloorDef>();
        public Dictionary<string, RoomDef> Rooms { get; set; } = new Dictionary<string, RoomDef>();
        public List<SystemDef> Systems { get; set; } = new List<SystemDef>();
        public RoomDetectionMapping DetectionMapping { get; set; } = new RoomDetectionMapping();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FloorDef
    {
        public string FloorId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Parter";
        public int Order { get; set; }
        public double Elevation { get; set; }
        public double HeightNet { get; set; } = 3.0;
        public double HeightTotal { get; set; } = 3.5;
        public PointDto BasePoint { get; set; } = new PointDto();
        public string BasePointDescription { get; set; } = "";
        public string DrukWidokiViewId { get; set; }
        public List<PointDto> Region { get; set; } = new List<PointDto>();
    }

    public class RoomDef
    {
        public string RoomId { get; set; } = Guid.NewGuid().ToString();
        public string FloorId { get; set; }
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string ActivityType { get; set; } = "CUSTOM";
        public string BoundaryHandle { get; set; } = "";
        public string TagHandle { get; set; } = "";
        public string SupplySystemId { get; set; } = "";
        public string ExhaustSystemId { get; set; } = "";
        public double Area { get; set; }
        public double Height { get; set; } = 3.0;
        public double Volume { get; set; }
        public int Occupants { get; set; } = 1;
        public double DosePerOccupant { get; set; } = 30;
        public bool IsTargetAchManual { get; set; }
        public double ManualTargetAch { get; set; }
        public double TargetAch { get; set; } = 1.5;
        public string CalculationMode { get; set; } = "AUTO_MAX";
        public double ManualSupply { get; set; }
        public double ManualExhaust { get; set; }
        public double CalculatedSupply { get; set; }
        public double CalculatedExhaust { get; set; }
        public double TransferIn { get; set; }
        public double TransferOut { get; set; }
        public double NetBalance { get; set; }
        public double RealAch { get; set; }
    }

    public class SystemDef
    {
        public string SystemId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "SUPPLY";
        public short ColorIndex { get; set; } = 5;
        public double TotalSupply { get; set; }
        public double TotalExhaust { get; set; }
    }
}
