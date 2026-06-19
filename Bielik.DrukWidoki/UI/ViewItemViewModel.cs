using System.ComponentModel;
using System.Linq;
using Bielik.DrukWidoki.Models;
using Bielik.DrukWidoki.Core;
using Bricscad.ApplicationServices;
using System;

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
                    SaveToDb();
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
                    SaveToDb();
                }
            }
        }

        public string Dimensions
        {
            get
            {
                if (_model.Geometry == null || _model.Geometry.Count == 0) return "Brak";
                double minX = _model.Geometry.Min(p => p.X);
                double maxX = _model.Geometry.Max(p => p.X);
                double minY = _model.Geometry.Min(p => p.Y);
                double maxY = _model.Geometry.Max(p => p.Y);
                double width = Math.Round(maxX - minX, 2);
                double height = Math.Round(maxY - minY, 2);
                return $"{width} x {height}";
            }
        }

        public string Position
        {
            get
            {
                if (_model.Geometry == null || _model.Geometry.Count == 0) return "Brak";
                double minX = Math.Round(_model.Geometry.Min(p => p.X), 2);
                double minY = Math.Round(_model.Geometry.Min(p => p.Y), 2);
                return $"({minX}, {minY})";
            }
        }

        private void SaveToDb()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (doc.LockDocument())
                    {
                        NodManager.SaveView(doc.Database, _model);
                    }
                }
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
