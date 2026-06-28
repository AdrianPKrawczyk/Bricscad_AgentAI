using System.Collections.ObjectModel;
using System.Linq;
using WentCad.Models;

namespace WentCad.UI
{
    public class BuildingFloorNode
    {
        public FloorDef Floor { get; set; }
        public ObservableCollection<BuildingRoomNode> Rooms { get; } = new ObservableCollection<BuildingRoomNode>();
        public string DisplayName => $"{Floor?.Name ?? "(bez kondygnacji)"} ({Rooms.Count} pom.)";
        public string Details => $"Rzedna {Floor?.Elevation:0.##} m | H netto {Floor?.HeightNet:0.##} m | H calk. {Floor?.HeightTotal:0.##} m";
    }

    public class BuildingRoomNode
    {
        public RoomDef Room { get; set; }
        public ObservableCollection<BuildingWallNode> Walls { get; } = new ObservableCollection<BuildingWallNode>();
        public ObservableCollection<BuildingHorizontalNode> HorizontalPartitions { get; } = new ObservableCollection<BuildingHorizontalNode>();
        public ObservableCollection<BuildingWindowNode> OrphanWindows { get; } = new ObservableCollection<BuildingWindowNode>();
        public ObservableCollection<object> Children { get; } = new ObservableCollection<object>();

        public string DisplayName => $"{Room?.Number} {Room?.Name}".Trim();
        public string Number => Room?.Number ?? "";
        public string Name => Room?.Name ?? "";
        public double Area => Room?.Area ?? 0;
        public double Height => Room?.Height ?? 0;
        public double Volume => Room?.Volume ?? 0;
        public double Supply => Room?.CalculatedSupply ?? 0;
        public double Exhaust => Room?.CalculatedExhaust ?? 0;
        public int ExternalWallCount => Walls.Count(w => w.Wall?.Kind == "EXTERNAL");
        public int InternalWallCount => Walls.Count(w => w.Wall?.Kind == "INTERNAL");
        public int UnresolvedWallCount => Walls.Count(w => w.Wall?.Kind == "UNRESOLVED");
        public double ExternalWallLength => Walls.Where(w => w.Wall?.Kind == "EXTERNAL").Sum(w => w.Wall.Length);
        public int WindowCount => Walls.Sum(w => w.Windows.Count) + OrphanWindows.Count;
        public double WindowArea => Walls.Sum(w => w.Windows.Sum(o => o.Window.Area)) + OrphanWindows.Sum(o => o.Window.Area);
        public string WattState => Walls.Count == 0 && HorizontalPartitions.Count == 0 && OrphanWindows.Count == 0 ? "Brak zeskanowanych przegrod." : "";
    }

    public class BuildingWallNode
    {
        public ThermalWallDef Wall { get; set; }
        public string AdjacentRoomLabel { get; set; } = "";
        public ObservableCollection<BuildingWindowNode> Windows { get; } = new ObservableCollection<BuildingWindowNode>();
        public ObservableCollection<BuildingWindowNode> Children => Windows;
        public string DisplayName => $"{Code} L={Wall?.Length:0.##} m";
        public string Code => string.IsNullOrWhiteSpace(Wall?.Code) ? ToWallCode(Wall?.Kind) : Wall.Code;
        public string Kind => Wall?.Kind ?? "";
        public string Construction => string.IsNullOrWhiteSpace(Wall?.ConstructionName) ? "Wybierz konstrukcje..." : Wall.ConstructionName;
        public double Length => Wall?.Length ?? 0;
        public double Height => Wall?.Height ?? 0;
        public double GrossArea => Wall?.GrossArea > 0 ? Wall.GrossArea : Length * Height;
        public double NetArea => System.Math.Max(0, GrossArea - Windows.Sum(o => o.Area));
        public double Thickness => Wall?.Thickness ?? 0;
        public double Azimuth => Wall?.Azimuth ?? 0;
        public double Confidence => Wall?.Confidence ?? 0;
        public string Message => Wall?.Message ?? "";

        private static string ToWallCode(string kind)
        {
            if (kind == "EXTERNAL") return "SZ";
            if (kind == "INTERNAL") return "SW";
            return "?";
        }
    }

    public class BuildingHorizontalNode
    {
        public ThermalHorizontalDef Partition { get; set; }
        public string DisplayName => $"{Code} A={Area:0.##} m2";
        public string Code => string.IsNullOrWhiteSpace(Partition?.Code) ? ToCode(Partition?.Kind) : Partition.Code;
        public string Kind => Partition?.Kind ?? "";
        public string Construction => string.IsNullOrWhiteSpace(Partition?.ConstructionName) ? "Wybierz konstrukcje..." : Partition.ConstructionName;
        public double Area => Partition?.Area ?? 0;
        public double Confidence => Partition?.Confidence ?? 0;
        public string Message => Partition?.Message ?? "";

        private static string ToCode(string kind)
        {
            if (kind == "FLOOR_GROUND") return "PG";
            if (kind == "ROOF" || kind == "FLOOR_EXTERIOR") return "D";
            if (kind == "CEILING_INTERIOR" || kind == "FLOOR_INTERIOR") return "StW";
            return "?";
        }
    }

    public class BuildingWindowNode
    {
        public ThermalWindowDef Window { get; set; }
        public string DisplayName => $"{Code} {Window?.Width:0.##} x {Window?.Height:0.##} m";
        public string Code => string.IsNullOrWhiteSpace(Window?.Code) ? (OpeningKind == "DOOR" ? "DRZ" : "OZ") : Window.Code;
        public string OpeningKind => Window?.OpeningKind ?? "WINDOW";
        public string BlockName => Window?.BlockName ?? "";
        public string Construction => string.IsNullOrWhiteSpace(Window?.ConstructionName) ? (OpeningKind == "DOOR" ? "Wybierz typ drzwi..." : "Wybierz styl okna...") : Window.ConstructionName;
        public double Width => Window?.Width ?? 0;
        public double Height => Window?.Height ?? 0;
        public double SillHeight => Window?.SillHeight ?? 0;
        public double Area => Window?.Area ?? 0;
        public double Confidence => Window?.Confidence ?? 0;
        public string Message => Window?.Message ?? "";
    }
}
