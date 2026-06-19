using System;
using System.Collections.Generic;

namespace Bielik.DrukWidoki.Models
{
    public class PointDTO
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class BielikViewDef
    {
        public Guid ViewId { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Nowy Widok";
        public string Type { get; set; } = "Rzut";
        
        public List<PointDTO> Geometry { get; set; } = new List<PointDTO>();
        
        public Dictionary<string, string> CustomVariables { get; set; } = new Dictionary<string, string>();
    }
}
