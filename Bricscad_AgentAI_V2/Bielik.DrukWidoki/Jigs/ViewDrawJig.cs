using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;
using Bricscad.EditorInput;

namespace Bielik.DrukWidoki.Jigs
{
    public class ViewDrawJig : EntityJig
    {
        private Point3d _startPt;
        private Point3d _endPt;
        private Polyline _poly;

        public ViewDrawJig(Point3d startPt) : base(new Polyline())
        {
            _startPt = startPt;
            _poly = Entity as Polyline;
            _poly.AddVertexAt(0, new Point2d(_startPt.X, _startPt.Y), 0, 0, 0);
            _poly.AddVertexAt(1, new Point2d(_startPt.X, _startPt.Y), 0, 0, 0);
            _poly.AddVertexAt(2, new Point2d(_startPt.X, _startPt.Y), 0, 0, 0);
            _poly.AddVertexAt(3, new Point2d(_startPt.X, _startPt.Y), 0, 0, 0);
            _poly.Closed = true;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions("\nWskaż przeciwległy narożnik: ");
            options.BasePoint = _startPt;
            options.UseBasePoint = true;

            var res = prompts.AcquirePoint(options);
            if (res.Status == PromptStatus.OK)
            {
                if (_endPt == res.Value) return SamplerStatus.NoChange;
                _endPt = res.Value;
                return SamplerStatus.OK;
            }
            return SamplerStatus.Cancel;
        }

        protected override bool Update()
        {
            _poly.SetPointAt(1, new Point2d(_endPt.X, _startPt.Y));
            _poly.SetPointAt(2, new Point2d(_endPt.X, _endPt.Y));
            _poly.SetPointAt(3, new Point2d(_startPt.X, _endPt.Y));
            return true;
        }

        public Polyline GetEntity() => _poly;
    }
}
