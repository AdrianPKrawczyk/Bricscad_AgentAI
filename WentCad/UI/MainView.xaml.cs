using System.Windows.Controls;
using System.Windows.Input;
using Bricscad.ApplicationServices;

namespace WentCad.UI
{
    public partial class MainView : UserControl
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainView()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public void LoadActiveDocument()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null) ViewModel.Load(doc);
        }

        private void AddFloor_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.AddFloor();

        private void DrawRegion_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.DrawRegionForSelectedFloor();

        private void PickRegion_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.PickRegionForSelectedFloor();

        private void Save_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.Save();

        private void ScanRooms_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.ScanRooms();

        private void ScanThermal_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.ScanThermalEnvelope();

        private void ScanThermalBuilding_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.ScanThermalBuilding();

        private void AddMaterial_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.AddMaterial();

        private void RemoveMaterial_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.RemoveMaterial();

        private void AddLayerSet_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.AddLayerSet();

        private void RemoveLayerSet_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.RemoveLayerSet();

        private void AddLayer_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.AddLayerToSelectedSet();

        private void RemoveLayer_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.RemoveSelectedLayer();

        private void AddConstruction_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.AddConstruction();

        private void RemoveConstruction_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.RemoveConstruction();

        private void AddOpeningStyle_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.AddOpeningStyle();

        private void RemoveOpeningStyle_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.RemoveOpeningStyle();

        private void Recalculate_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.RecalculateAndSync();

        private void ExportCsv_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.ExportCsv();

        private void ExportIfc_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.ExportIfc();

        private void PickBasePoint_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.PickBasePointForSelectedFloor();

        private void AssignDrukWidoki_Click(object sender, MouseButtonEventArgs e)
        {
            ViewModel.AssignSelectedDrukWidokiView(DrukWidokiList.SelectedItem as DrukWidokiViewItem);
        }

        private void BuildingStructure_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            ViewModel.SelectBuildingStructureNode(e.NewValue);
        }

        private void BuildingStructureRoom_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element)
            {
                ViewModel.SelectBuildingStructureNode(element.DataContext);
            }
        }
    }
}
