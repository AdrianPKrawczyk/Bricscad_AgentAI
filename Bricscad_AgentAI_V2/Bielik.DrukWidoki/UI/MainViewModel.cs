using System.Collections.ObjectModel;
using System.ComponentModel;
using Bielik.DrukWidoki.Models;

namespace Bielik.DrukWidoki.UI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ViewItemViewModel> Views { get; set; } = new ObservableCollection<ViewItemViewModel>();

        private ViewItemViewModel _selectedView;
        public ViewItemViewModel SelectedView
        {
            get => _selectedView;
            set
            {
                _selectedView = value;
                OnPropertyChanged(nameof(SelectedView));
            }
        }

        public void LoadData(System.Collections.Generic.List<BielikViewDef> data)
        {
            Views.Clear();
            foreach (var item in data)
            {
                Views.Add(new ViewItemViewModel(item));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
