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

        private void Recalculate_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.RecalculateAndSync();

        private void ExportCsv_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.ExportCsv();

        private void ExportIfc_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.ExportIfc();

        private void PickBasePoint_Click(object sender, System.Windows.RoutedEventArgs e) => ViewModel.PickBasePointForSelectedFloor();

        private void AssignDrukWidoki_Click(object sender, MouseButtonEventArgs e)
        {
            ViewModel.AssignSelectedDrukWidokiView(DrukWidokiList.SelectedItem as DrukWidokiViewItem);
        }
    }
}
