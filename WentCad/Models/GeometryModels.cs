using System.Collections.Generic;

namespace WentCad.Models
{
    public class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class RoomDetectionMapping
    {
        public string BoundaryLayer { get; set; } = "";
        public string TagLayer { get; set; } = "";
        public string NumberAttribute { get; set; } = "NR";
        public string NameAttribute { get; set; } = "NAZWA";
        public string HeightAttribute { get; set; } = "";
        public string AreaAttribute { get; set; } = "";
    }

    public class DrukWidokiViewDef
    {
        public string ViewId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public List<PointDto> Geometry { get; set; } = new List<PointDto>();
        public Dictionary<string, string> CustomVariables { get; set; } = new Dictionary<string, string>();
    }
}
