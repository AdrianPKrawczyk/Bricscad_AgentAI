using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace WentCad.Jigs
{
    public class FloorRegionDrawJig : EntityJig
    {
        private readonly Point3d _startPoint;
        private Point3d _endPoint;
        private readonly Polyline _polyline;

        public FloorRegionDrawJig(Point3d startPoint) : base(new Polyline())
        {
            _startPoint = startPoint;
            _endPoint = startPoint;
            _polyline = (Polyline)Entity;
            _polyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
            _polyline.AddVertexAt(1, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
            _polyline.AddVertexAt(2, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
            _polyline.AddVertexAt(3, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
            _polyline.Closed = true;
            _polyline.ColorIndex = 3;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions("\nWskaz przeciwlegly naroznik zakresu kondygnacji: ")
            {
                BasePoint = _startPoint,
                UseBasePoint = true
            };

            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK) return SamplerStatus.Cancel;
            if (_endPoint == result.Value) return SamplerStatus.NoChange;
            _endPoint = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool Update()
        {
            _polyline.SetPointAt(1, new Point2d(_endPoint.X, _startPoint.Y));
            _polyline.SetPointAt(2, new Point2d(_endPoint.X, _endPoint.Y));
            _polyline.SetPointAt(3, new Point2d(_startPoint.X, _endPoint.Y));
            return true;
        }

        public Polyline GetEntity()
        {
            return _polyline;
        }
    }
}
