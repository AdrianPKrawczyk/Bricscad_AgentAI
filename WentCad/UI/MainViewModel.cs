using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Bricscad.ApplicationServices;
using WentCad.Core;
using WentCad.Models;

namespace WentCad.UI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private Document _document;
        private WentCadProject _project;
        private FloorDef _selectedFloor;
        private RoomDef _selectedRoom;
        private BuildingRoomNode _selectedBuildingRoom;
        private string _status = "Gotowe";

        public ObservableCollection<FloorDef> Floors { get; } = new ObservableCollection<FloorDef>();
        public ObservableCollection<RoomDef> Rooms { get; } = new ObservableCollection<RoomDef>();
        public ObservableCollection<SystemDef> Systems { get; } = new ObservableCollection<SystemDef>();
        public ObservableCollection<ThermalWallDef> Walls { get; } = new ObservableCollection<ThermalWallDef>();
        public ObservableCollection<ThermalWindowDef> Windows { get; } = new ObservableCollection<ThermalWindowDef>();
        public ObservableCollection<ThermalHorizontalDef> HorizontalPartitions { get; } = new ObservableCollection<ThermalHorizontalDef>();
        public ObservableCollection<DrukWidokiViewItem> DrukWidokiViews { get; } = new ObservableCollection<DrukWidokiViewItem>();
        public ObservableCollection<BuildingFloorNode> BuildingStructure { get; } = new ObservableCollection<BuildingFloorNode>();

        public WentCadProject Project
        {
            get => _project;
            private set
            {
                _project = value;
                OnPropertyChanged(nameof(Project));
            }
        }

        public FloorDef SelectedFloor
        {
            get => _selectedFloor;
            set
            {
                if (ReferenceEquals(_selectedFloor, value) || _selectedFloor?.FloorId == value?.FloorId) return;
                _selectedFloor = value;
                RefreshRooms();
                OnPropertyChanged(nameof(SelectedFloor));
            }
        }

        public RoomDef SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                if (ReferenceEquals(_selectedRoom, value) || _selectedRoom?.RoomId == value?.RoomId) return;
                _selectedRoom = value;
                SelectBuildingRoomById(_selectedRoom?.RoomId);
                OnPropertyChanged(nameof(SelectedRoom));
            }
        }

        public BuildingRoomNode SelectedBuildingRoom
        {
            get => _selectedBuildingRoom;
            set
            {
                _selectedBuildingRoom = value;
                OnPropertyChanged(nameof(SelectedBuildingRoom));
            }
        }

        public string BoundaryLayer
        {
            get => Project?.DetectionMapping?.BoundaryLayer ?? "";
            set
            {
                EnsureMapping();
                Project.DetectionMapping.BoundaryLayer = value ?? "";
                OnPropertyChanged(nameof(BoundaryLayer));
            }
        }

        public string TagLayer
        {
            get => Project?.DetectionMapping?.TagLayer ?? "";
            set
            {
                EnsureMapping();
                Project.DetectionMapping.TagLayer = value ?? "";
                OnPropertyChanged(nameof(TagLayer));
            }
        }

        public string NumberAttribute
        {
            get => Project?.DetectionMapping?.NumberAttribute ?? "";
            set
            {
                EnsureMapping();
                Project.DetectionMapping.NumberAttribute = value ?? "";
                OnPropertyChanged(nameof(NumberAttribute));
            }
        }

        public string NameAttribute
        {
            get => Project?.DetectionMapping?.NameAttribute ?? "";
            set
            {
                EnsureMapping();
                Project.DetectionMapping.NameAttribute = value ?? "";
                OnPropertyChanged(nameof(NameAttribute));
            }
        }

        public string HeightAttribute
        {
            get => Project?.DetectionMapping?.HeightAttribute ?? "";
            set
            {
                EnsureMapping();
                Project.DetectionMapping.HeightAttribute = value ?? "";
                OnPropertyChanged(nameof(HeightAttribute));
            }
        }

        public string AreaAttribute
        {
            get => Project?.DetectionMapping?.AreaAttribute ?? "";
            set
            {
                EnsureMapping();
                Project.DetectionMapping.AreaAttribute = value ?? "";
                OnPropertyChanged(nameof(AreaAttribute));
            }
        }

        public string WallLayer
        {
            get => Project?.Thermal?.Settings?.WallLayer ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WallLayer = value ?? "";
                OnPropertyChanged(nameof(WallLayer));
            }
        }

        public string WindowLayer
        {
            get => Project?.Thermal?.Settings?.WindowLayer ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WindowLayer = value ?? "";
                OnPropertyChanged(nameof(WindowLayer));
            }
        }

        public string WindowBlockNamePattern
        {
            get => Project?.Thermal?.Settings?.WindowBlockNamePattern ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WindowBlockNamePattern = value ?? "";
                OnPropertyChanged(nameof(WindowBlockNamePattern));
            }
        }

        public string WindowLabelLayer
        {
            get => Project?.Thermal?.Settings?.WindowLabelLayer ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WindowLabelLayer = value ?? "";
                OnPropertyChanged(nameof(WindowLabelLayer));
            }
        }

        public string WindowLabelBlockNamePattern
        {
            get => Project?.Thermal?.Settings?.WindowLabelBlockNamePattern ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WindowLabelBlockNamePattern = value ?? "";
                OnPropertyChanged(nameof(WindowLabelBlockNamePattern));
            }
        }

        public string WindowWidthAttribute
        {
            get => Project?.Thermal?.Settings?.WindowWidthAttribute ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WindowWidthAttribute = value ?? "";
                OnPropertyChanged(nameof(WindowWidthAttribute));
            }
        }

        public string WindowHeightAttribute
        {
            get => Project?.Thermal?.Settings?.WindowHeightAttribute ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WindowHeightAttribute = value ?? "";
                OnPropertyChanged(nameof(WindowHeightAttribute));
            }
        }

        public string WindowSillAttribute
        {
            get => Project?.Thermal?.Settings?.WindowSillAttribute ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.WindowSillAttribute = value ?? "";
                OnPropertyChanged(nameof(WindowSillAttribute));
            }
        }

        public string DoorLayer
        {
            get => Project?.Thermal?.Settings?.DoorLayer ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.DoorLayer = value ?? "";
                OnPropertyChanged(nameof(DoorLayer));
            }
        }

        public string DoorBlockNamePattern
        {
            get => Project?.Thermal?.Settings?.DoorBlockNamePattern ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.DoorBlockNamePattern = value ?? "";
                OnPropertyChanged(nameof(DoorBlockNamePattern));
            }
        }

        public string DoorLabelLayer
        {
            get => Project?.Thermal?.Settings?.DoorLabelLayer ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.DoorLabelLayer = value ?? "";
                OnPropertyChanged(nameof(DoorLabelLayer));
            }
        }

        public string DoorLabelBlockNamePattern
        {
            get => Project?.Thermal?.Settings?.DoorLabelBlockNamePattern ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.DoorLabelBlockNamePattern = value ?? "";
                OnPropertyChanged(nameof(DoorLabelBlockNamePattern));
            }
        }

        public string DoorWidthAttribute
        {
            get => Project?.Thermal?.Settings?.DoorWidthAttribute ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.DoorWidthAttribute = value ?? "";
                OnPropertyChanged(nameof(DoorWidthAttribute));
            }
        }

        public string DoorHeightAttribute
        {
            get => Project?.Thermal?.Settings?.DoorHeightAttribute ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.DoorHeightAttribute = value ?? "";
                OnPropertyChanged(nameof(DoorHeightAttribute));
            }
        }

        public string DoorSillAttribute
        {
            get => Project?.Thermal?.Settings?.DoorSillAttribute ?? "";
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.DoorSillAttribute = value ?? "";
                OnPropertyChanged(nameof(DoorSillAttribute));
            }
        }

        public double MaxInteriorWallDistance
        {
            get => Project?.Thermal?.Settings?.MaxInteriorWallDistance ?? 0.6;
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.MaxInteriorWallDistance = value;
                OnPropertyChanged(nameof(MaxInteriorWallDistance));
            }
        }

        public double MaxExteriorConfirmDistance
        {
            get => Project?.Thermal?.Settings?.MaxExteriorConfirmDistance ?? 1.2;
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.MaxExteriorConfirmDistance = value;
                OnPropertyChanged(nameof(MaxExteriorConfirmDistance));
            }
        }

        public double MaxWindowSnapDistance
        {
            get => Project?.Thermal?.Settings?.MaxWindowSnapDistance ?? 1.0;
            set
            {
                EnsureThermal();
                Project.Thermal.Settings.MaxWindowSnapDistance = value;
                OnPropertyChanged(nameof(MaxWindowSnapDistance));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public void Load(Document document)
        {
            _document = document;
            Project = ProjectFileService.LoadOrCreate(document);
            RefreshCollections();
            Status = $"Projekt: {Project.ProjectName}";
        }

        public void Save()
        {
            if (_document == null || Project == null) return;
            BalanceEngine.Recalculate(Project);
            ProjectFileService.Save(_document, Project);
            NodManager.SaveProjectIndex(_document, Project);
            GeometryManager.WriteRoomXData(_document, Project);
            GeometryManager.WriteWindowXData(_document, Project);
            RefreshCollections();
            Status = "Zapisano .wentcad, NOD i XData.";
        }

        public void AddFloor()
        {
            if (Project == null) return;
            var floor = new FloorDef
            {
                Name = $"Kondygnacja {Project.Floors.Count + 1}",
                Order = Project.Floors.Count,
                HeightNet = 3.0,
                HeightTotal = 3.5
            };
            Project.Floors[floor.FloorId] = floor;
            RefreshCollections();
            SelectedFloor = floor;
            Save();
        }

        public void DrawRegionForSelectedFloor()
        {
            if (SelectedFloor == null)
            {
                Status = "Wybierz kondygnacje.";
                return;
            }

            _document.SendStringToExecute("WENTCAD_DRAW_FLOOR_REGION ", true, false, false);
        }

        public void PickRegionForSelectedFloor()
        {
            if (SelectedFloor == null)
            {
                Status = "Wybierz kondygnacje.";
                return;
            }

            _document.SendStringToExecute("WENTCAD_PICK_FLOOR_REGION ", true, false, false);
        }

        public void PickBasePointForSelectedFloor()
        {
            if (SelectedFloor == null)
            {
                Status = "Wybierz kondygnacje.";
                return;
            }

            _document.SendStringToExecute("WENTCAD_PICK_BASE_POINT ", true, false, false);
        }

        public void AssignSelectedDrukWidokiView(DrukWidokiViewItem view)
        {
            if (SelectedFloor == null || view == null) return;
            SelectedFloor.DrukWidokiViewId = view.ViewId;
            SelectedFloor.Region.Clear();
            Save();
            Status = $"Przypisano zakres DrukWidoki: {view.DisplayName}";
        }

        public void SelectBuildingStructureNode(object node)
        {
            if (node is BuildingRoomNode roomNode)
            {
                SelectedBuildingRoom = roomNode;
                SetSelectedRoomFromStructure(roomNode.Room);
            }
            else if (node is BuildingWallNode wallNode)
            {
                SelectBuildingRoomById(wallNode.Wall?.RoomId);
            }
            else if (node is BuildingWindowNode windowNode)
            {
                SelectBuildingRoomById(windowNode.Window?.RoomId);
            }
            else if (node is BuildingHorizontalNode horizontalNode)
            {
                SelectBuildingRoomById(horizontalNode.Partition?.RoomId);
            }
        }

        public void ScanRooms()
        {
            if (_document == null || Project == null || SelectedFloor == null)
            {
                Status = "Wybierz kondygnacje przed skanowaniem.";
                return;
            }

            var result = RoomScanner.ScanActiveFloor(_document, Project, SelectedFloor);
            RefreshCollections();
            Status = $"Skan: obrysy {result.Boundaries}, metki {result.Tags}, dopasowane {result.Matched}, bez metki {result.UnmatchedBoundaries}.";
            if (result.Messages.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, result.Messages), "WentCad - uwagi skanowania");
            }
        }

        public void ScanThermalEnvelope()
        {
            if (_document == null || Project == null || SelectedFloor == null)
            {
                Status = "Wybierz kondygnacje przed skanowaniem przegród.";
                return;
            }

            var result = ThermalScanner.ScanActiveFloor(_document, Project, SelectedFloor);
            RefreshCollections();
            Status = $"WATT: sciany {result.Walls} (zewn. {result.ExternalWalls}, wewn. {result.InternalWalls}, nierozw. {result.UnresolvedWalls}), okna {result.WindowsAssigned}/{result.WindowCandidates}.";
            if (result.Messages.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, result.Messages), "WentCad - uwagi WATT");
            }
        }

        public void ScanThermalBuilding()
        {
            if (_document == null || Project == null || Project.Floors.Count == 0)
            {
                Status = "Brak projektu albo kondygnacji do skanowania WATT.";
                return;
            }

            int walls = 0;
            int external = 0;
            int internalWalls = 0;
            int unresolved = 0;
            int openings = 0;
            int candidates = 0;
            var messages = new System.Collections.Generic.List<string>();
            var selectedFloorId = SelectedFloor?.FloorId;

            foreach (var floor in Project.Floors.Values.OrderBy(f => f.Order).ThenBy(f => f.Name))
            {
                var result = ThermalScanner.ScanActiveFloor(_document, Project, floor);
                walls += result.Walls;
                external += result.ExternalWalls;
                internalWalls += result.InternalWalls;
                unresolved += result.UnresolvedWalls;
                openings += result.WindowsAssigned;
                candidates += result.WindowCandidates;
                foreach (var message in result.Messages)
                {
                    messages.Add($"{floor.Name}: {message}");
                }
            }

            RefreshCollections();
            if (!string.IsNullOrWhiteSpace(selectedFloorId))
            {
                SelectedFloor = Floors.FirstOrDefault(f => f.FloorId == selectedFloorId) ?? SelectedFloor;
            }
            Status = $"WATT caly budynek: sciany {walls} (zewn. {external}, wewn. {internalWalls}, nierozw. {unresolved}), otwory {openings}/{candidates}.";
            if (messages.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, messages.Take(30)) + (messages.Count > 30 ? Environment.NewLine + $"... oraz {messages.Count - 30} kolejnych." : ""), "WentCad - uwagi WATT");
            }
        }

        public void RecalculateAndSync()
        {
            if (Project == null) return;
            BalanceEngine.Recalculate(Project);
            Save();
            GeometryManager.ColorRoomsBySystem(_document, Project);
            Status = "Przeliczono bilans i odswiezono kolory systemow.";
        }

        public void ExportCsv()
        {
            if (_document == null || Project == null) return;
            string basePath = ProjectFileService.GetProjectPath(_document);
            string csvPath = Path.ChangeExtension(basePath, ".rooms.csv");
            var sb = new StringBuilder();
            sb.AppendLine("FloorId;Number;Name;Area;Height;Volume;SupplySystemId;ExhaustSystemId;CalculatedSupply;CalculatedExhaust;RealAch;NetBalance");
            foreach (var room in Project.Rooms.Values.OrderBy(r => r.FloorId).ThenBy(r => r.Number))
            {
                sb.AppendLine(string.Join(";",
                    room.FloorId,
                    Escape(room.Number),
                    Escape(room.Name),
                    room.Area.ToString("0.##"),
                    room.Height.ToString("0.##"),
                    room.Volume.ToString("0.##"),
                    room.SupplySystemId,
                    room.ExhaustSystemId,
                    room.CalculatedSupply.ToString("0.##"),
                    room.CalculatedExhaust.ToString("0.##"),
                    room.RealAch.ToString("0.##"),
                    room.NetBalance.ToString("0.##")));
            }
            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
            Status = $"Wyeksportowano CSV: {csvPath}";
        }

        public void ExportIfc()
        {
            if (_document == null || Project == null) return;
            try
            {
                Save();
                string ifcPath = IfcExportService.ExportSpaces(_document, Project);
                Status = $"Wyeksportowano IFC: {ifcPath}";
            }
            catch (Exception ex)
            {
                Status = $"Blad eksportu IFC: {ex.Message}";
                MessageBox.Show(ex.Message, "WentCad - eksport IFC");
            }
        }

        private void RefreshCollections()
        {
            Floors.Clear();
            Rooms.Clear();
            Systems.Clear();
            Walls.Clear();
            Windows.Clear();
            HorizontalPartitions.Clear();
            DrukWidokiViews.Clear();
            BuildingStructure.Clear();
            if (Project == null) return;

            foreach (var floor in Project.Floors.Values.OrderBy(f => f.Order).ThenBy(f => f.Name)) Floors.Add(floor);
            foreach (var system in Project.Systems.OrderBy(s => s.Type).ThenBy(s => s.SystemId)) Systems.Add(system);
            if (_document != null)
            {
                foreach (var view in NodManager.LoadDrukWidokiViews(_document.Database).OrderBy(v => v.Name))
                {
                    DrukWidokiViews.Add(new DrukWidokiViewItem { ViewId = view.ViewId, Name = view.Name, Type = view.Type });
                }
            }

            SelectedFloor = Floors.FirstOrDefault(f => SelectedFloor != null && f.FloorId == SelectedFloor.FloorId) ?? Floors.FirstOrDefault();
            RefreshRooms();
            OnPropertyChanged(nameof(BoundaryLayer));
            OnPropertyChanged(nameof(TagLayer));
            OnPropertyChanged(nameof(NumberAttribute));
            OnPropertyChanged(nameof(NameAttribute));
            OnPropertyChanged(nameof(HeightAttribute));
            OnPropertyChanged(nameof(AreaAttribute));
            OnPropertyChanged(nameof(WallLayer));
            OnPropertyChanged(nameof(WindowLayer));
            OnPropertyChanged(nameof(WindowBlockNamePattern));
            OnPropertyChanged(nameof(WindowLabelLayer));
            OnPropertyChanged(nameof(WindowLabelBlockNamePattern));
            OnPropertyChanged(nameof(WindowWidthAttribute));
            OnPropertyChanged(nameof(WindowHeightAttribute));
            OnPropertyChanged(nameof(WindowSillAttribute));
            OnPropertyChanged(nameof(DoorLayer));
            OnPropertyChanged(nameof(DoorBlockNamePattern));
            OnPropertyChanged(nameof(DoorLabelLayer));
            OnPropertyChanged(nameof(DoorLabelBlockNamePattern));
            OnPropertyChanged(nameof(DoorWidthAttribute));
            OnPropertyChanged(nameof(DoorHeightAttribute));
            OnPropertyChanged(nameof(DoorSillAttribute));
            OnPropertyChanged(nameof(MaxInteriorWallDistance));
            OnPropertyChanged(nameof(MaxExteriorConfirmDistance));
            OnPropertyChanged(nameof(MaxWindowSnapDistance));
        }

        private void RefreshRooms()
        {
            Rooms.Clear();
            Walls.Clear();
            Windows.Clear();
            HorizontalPartitions.Clear();
            if (Project == null) return;
            var floorId = SelectedFloor?.FloorId;
            foreach (var room in Project.Rooms.Values
                .Where(r => string.IsNullOrWhiteSpace(floorId) || r.FloorId == floorId)
                .OrderBy(r => r.Number).ThenBy(r => r.Name))
            {
                Rooms.Add(room);
            }
            if (Project.Thermal?.Walls != null)
            {
                foreach (var wall in Project.Thermal.Walls.Values
                    .Where(w => string.IsNullOrWhiteSpace(floorId) || w.FloorId == floorId)
                    .OrderBy(w => w.RoomId).ThenBy(w => w.WallId))
                {
                    Walls.Add(wall);
                }
            }
            if (Project.Thermal?.Windows != null)
            {
                foreach (var window in Project.Thermal.Windows.Values
                    .Where(w => string.IsNullOrWhiteSpace(floorId) || w.FloorId == floorId)
                    .OrderBy(w => w.RoomId).ThenBy(w => w.WindowId))
                {
                    Windows.Add(window);
                }
            }
            if (Project.Thermal?.HorizontalPartitions != null)
            {
                foreach (var partition in Project.Thermal.HorizontalPartitions.Values
                    .Where(p => string.IsNullOrWhiteSpace(floorId) || p.FloorId == floorId)
                    .OrderBy(p => p.RoomId).ThenBy(p => p.Code).ThenBy(p => p.PartitionId))
                {
                    HorizontalPartitions.Add(partition);
                }
            }
            RefreshBuildingStructure();
        }

        private void RefreshBuildingStructure()
        {
            var selectedRoomId = SelectedBuildingRoom?.Room?.RoomId ?? SelectedRoom?.RoomId;
            BuildingStructure.Clear();
            SelectedBuildingRoom = null;
            if (Project == null) return;

            var allRooms = Project.Rooms.Values.ToList();
            var allWalls = Project.Thermal?.Walls?.Values.ToList() ?? new System.Collections.Generic.List<ThermalWallDef>();
            var allWindows = Project.Thermal?.Windows?.Values.ToList() ?? new System.Collections.Generic.List<ThermalWindowDef>();
            var allHorizontal = Project.Thermal?.HorizontalPartitions?.Values.ToList() ?? new System.Collections.Generic.List<ThermalHorizontalDef>();

            foreach (var floor in Project.Floors.Values.OrderBy(f => f.Order).ThenBy(f => f.Name))
            {
                var floorNode = new BuildingFloorNode { Floor = floor };
                foreach (var room in allRooms.Where(r => r.FloorId == floor.FloorId).OrderBy(r => r.Number).ThenBy(r => r.Name))
                {
                    var roomNode = new BuildingRoomNode { Room = room };
                    foreach (var wall in allWalls.Where(w => w.RoomId == room.RoomId).OrderBy(w => w.Kind).ThenBy(w => w.Azimuth).ThenBy(w => w.WallId))
                    {
                        var adjacentRoom = allRooms.FirstOrDefault(r => r.RoomId == wall.AdjacentRoomId);
                        var wallNode = new BuildingWallNode
                        {
                            Wall = wall,
                            AdjacentRoomLabel = adjacentRoom == null ? "" : $"{adjacentRoom.Number} {adjacentRoom.Name}".Trim()
                        };
                        foreach (var window in allWindows.Where(w => w.WallId == wall.WallId).OrderBy(w => w.Placement).ThenBy(w => w.WindowId))
                        {
                            wallNode.Windows.Add(new BuildingWindowNode { Window = window });
                        }
                        roomNode.Walls.Add(wallNode);
                        roomNode.Children.Add(wallNode);
                    }

                    foreach (var partition in allHorizontal.Where(p => p.RoomId == room.RoomId).OrderBy(p => p.Code).ThenBy(p => p.PartitionId))
                    {
                        var partitionNode = new BuildingHorizontalNode { Partition = partition };
                        roomNode.HorizontalPartitions.Add(partitionNode);
                        roomNode.Children.Add(partitionNode);
                    }

                    foreach (var window in allWindows
                        .Where(w => w.RoomId == room.RoomId && string.IsNullOrWhiteSpace(w.WallId))
                        .OrderBy(w => w.WindowId))
                    {
                        var windowNode = new BuildingWindowNode { Window = window };
                        roomNode.OrphanWindows.Add(windowNode);
                        roomNode.Children.Add(windowNode);
                    }

                    floorNode.Rooms.Add(roomNode);
                    if (room.RoomId == selectedRoomId) SelectedBuildingRoom = roomNode;
                }
                BuildingStructure.Add(floorNode);
            }

            if (SelectedBuildingRoom == null && SelectedRoom != null)
            {
                SelectBuildingRoomById(SelectedRoom.RoomId);
            }

            if (SelectedBuildingRoom == null)
            {
                var floorId = SelectedFloor?.FloorId;
                SelectedBuildingRoom = BuildingStructure
                    .Where(f => string.IsNullOrWhiteSpace(floorId) || f.Floor?.FloorId == floorId)
                    .SelectMany(f => f.Rooms)
                    .FirstOrDefault();
            }
        }

        private void SelectBuildingRoomById(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return;
            var roomNode = BuildingStructure.SelectMany(f => f.Rooms).FirstOrDefault(r => r.Room?.RoomId == roomId);
            if (roomNode != null && !ReferenceEquals(SelectedBuildingRoom, roomNode))
            {
                SelectedBuildingRoom = roomNode;
                SetSelectedRoomFromStructure(roomNode.Room);
            }
        }

        private void SetSelectedRoomFromStructure(RoomDef room)
        {
            if (room == null) return;
            if (ReferenceEquals(_selectedRoom, room) || _selectedRoom?.RoomId == room.RoomId) return;
            _selectedRoom = room;
            OnPropertyChanged(nameof(SelectedRoom));
        }

        private void EnsureMapping()
        {
            if (Project == null) return;
            if (Project.DetectionMapping == null) Project.DetectionMapping = new RoomDetectionMapping();
        }

        private void EnsureThermal()
        {
            if (Project == null) return;
            if (Project.Thermal == null) Project.Thermal = new ThermalModel();
            if (Project.Thermal.Walls == null) Project.Thermal.Walls = new System.Collections.Generic.Dictionary<string, ThermalWallDef>();
            if (Project.Thermal.Windows == null) Project.Thermal.Windows = new System.Collections.Generic.Dictionary<string, ThermalWindowDef>();
            if (Project.Thermal.HorizontalPartitions == null) Project.Thermal.HorizontalPartitions = new System.Collections.Generic.Dictionary<string, ThermalHorizontalDef>();
            if (Project.Thermal.Settings == null) Project.Thermal.Settings = new ThermalSettings();
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace(";", ",").Replace(Environment.NewLine, " ");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
