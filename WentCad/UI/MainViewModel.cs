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
        private string _status = "Gotowe";

        public ObservableCollection<FloorDef> Floors { get; } = new ObservableCollection<FloorDef>();
        public ObservableCollection<RoomDef> Rooms { get; } = new ObservableCollection<RoomDef>();
        public ObservableCollection<SystemDef> Systems { get; } = new ObservableCollection<SystemDef>();
        public ObservableCollection<DrukWidokiViewItem> DrukWidokiViews { get; } = new ObservableCollection<DrukWidokiViewItem>();

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
                _selectedRoom = value;
                OnPropertyChanged(nameof(SelectedRoom));
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
            DrukWidokiViews.Clear();
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
        }

        private void RefreshRooms()
        {
            Rooms.Clear();
            if (Project == null) return;
            var floorId = SelectedFloor?.FloorId;
            foreach (var room in Project.Rooms.Values
                .Where(r => string.IsNullOrWhiteSpace(floorId) || r.FloorId == floorId)
                .OrderBy(r => r.Number).ThenBy(r => r.Name))
            {
                Rooms.Add(room);
            }
        }

        private void EnsureMapping()
        {
            if (Project == null) return;
            if (Project.DetectionMapping == null) Project.DetectionMapping = new RoomDetectionMapping();
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
