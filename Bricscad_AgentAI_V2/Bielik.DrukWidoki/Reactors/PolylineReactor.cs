using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;
using System;

namespace Bielik.DrukWidoki.Reactors
{
    public class PolylineReactor
    {
        private static bool _isRegistered = false;

        public static void Register()
        {
            if (!_isRegistered)
            {
                var db = HostApplicationServices.WorkingDatabase;
                db.ObjectModified += Db_ObjectModified;
                _isRegistered = true;
            }
        }

        private static void Db_ObjectModified(object sender, ObjectEventArgs e)
        {
            var poly = e.DBObject as Polyline;
            if (poly != null && poly.XData != null)
            {
                // Basic stub for detecting XData modifications
                // In full implementation, enqueue update to NOD on Application.Idle
            }
        }
    }
}
