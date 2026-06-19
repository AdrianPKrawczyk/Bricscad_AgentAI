using System.ComponentModel;
using Bielik.DrukWidoki.Models;

namespace Bielik.DrukWidoki.UI
{
    public class ViewItemViewModel : INotifyPropertyChanged
    {
        private BielikViewDef _model;

        public BielikViewDef Model => _model;

        public ViewItemViewModel(BielikViewDef model)
        {
            _model = model;
        }

        public string Name
        {
            get => _model.Name;
            set
            {
                if (_model.Name != value)
                {
                    _model.Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Type
        {
            get => _model.Type;
            set
            {
                if (_model.Type != value)
                {
                    _model.Type = value;
                    OnPropertyChanged(nameof(Type));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
