using Bricscad.Windows;
using Teigha.Runtime;
using System.Windows.Forms.Integration;

[assembly: CommandClass(typeof(Bielik.DrukWidoki.UI.PaletteSetManager))]
namespace Bielik.DrukWidoki.UI
{
    public class PaletteSetManager
    {
        private static PaletteSet _paletteSet;
        private static MainView _mainView;

        public static MainView MainViewInstance => _mainView;

        [CommandMethod("DRUK_WIDOKI")]
        public static void ShowPalette()
        {
            if (_paletteSet == null)
            {
                _paletteSet = new PaletteSet("Bielik - Druk Widoki", new System.Guid("12345678-1234-1234-1234-123456789012"));
                _paletteSet.Style = PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowAutoHideButton;

                _mainView = new MainView();
                ElementHost host = new ElementHost();
                host.AutoSize = true;
                host.Dock = System.Windows.Forms.DockStyle.Fill;
                host.Child = _mainView;

                _paletteSet.Add("Widoki", host);
            }
            
            // Reload data from current DB
            var db = Teigha.DatabaseServices.HostApplicationServices.WorkingDatabase;
            var data = Core.NodManager.LoadViews(db);
            _mainView.ViewModel.LoadData(data);

            // Register Reactors
            Reactors.PolylineReactor.Register();

            _paletteSet.Visible = true;
        }
    }
}
