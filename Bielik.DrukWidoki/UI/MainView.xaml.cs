using System.Windows;
using System.Windows.Controls;

namespace Bielik.DrukWidoki.UI
{
    public partial class MainView : UserControl
    {
        public MainViewModel ViewModel { get; }

        public MainView()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                doc.SendStringToExecute("BIELIK_DRAW_VIEW ", true, false, false);
            }
        }

        private void BtnShow_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement Zoom
        }

        private void BtnRebuild_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement Rebuild
        }
    }
}
