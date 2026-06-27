using System;
using System.Linq;
using WentCad.Models;

namespace WentCad.Core
{
    public static class BalanceEngine
    {
        public static void Recalculate(WentCadProject project)
        {
            foreach (var room in project.Rooms.Values)
            {
                RecalculateRoom(room);
            }

            foreach (var system in project.Systems)
            {
                system.TotalSupply = project.Rooms.Values
                    .Where(r => string.Equals(r.SupplySystemId, system.SystemId, StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.CalculatedSupply);
                system.TotalExhaust = project.Rooms.Values
                    .Where(r => string.Equals(r.ExhaustSystemId, system.SystemId, StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.CalculatedExhaust);
            }
        }

        public static void RecalculateRoom(RoomDef room)
        {
            room.Volume = Math.Round(room.Area * room.Height, 2);
            double ach = room.IsTargetAchManual ? room.ManualTargetAch : DefaultAch(room.ActivityType);
            room.TargetAch = ach;

            double vHygienic = Math.Max(0, room.Occupants) * Math.Max(0, room.DosePerOccupant);
            double vAch = ach * room.Volume;
            double supply;

            switch ((room.CalculationMode ?? "AUTO_MAX").ToUpperInvariant())
            {
                case "MANUAL":
                    supply = room.ManualSupply;
                    break;
                case "HYGIENIC_ONLY":
                    supply = vHygienic;
                    break;
                case "ACH_ONLY":
                    supply = vAch;
                    break;
                default:
                    supply = Math.Max(vHygienic, Math.Max(vAch, room.ManualSupply));
                    break;
            }

            room.CalculatedSupply = Math.Ceiling(Math.Max(0, supply));
            room.CalculatedExhaust = Math.Ceiling(Math.Max(0, room.ManualExhaust > 0 ? room.ManualExhaust : room.CalculatedSupply + room.TransferIn - room.TransferOut));
            room.NetBalance = (room.CalculatedSupply + room.TransferIn) - (room.CalculatedExhaust + room.TransferOut);
            double dominant = Math.Max(room.CalculatedSupply + room.TransferIn, room.CalculatedExhaust + room.TransferOut);
            room.RealAch = room.Volume > 0 ? Math.Round(dominant / room.Volume, 2) : 0;
        }

        private static double DefaultAch(string activityType)
        {
            switch (activityType ?? "")
            {
                case "Gastronomia: Kuchnia": return 22.5;
                case "Gastronomia: Zmywalnia": return 10.0;
                case "Natryski": return 5.0;
                case "Umywalnia": return 2.0;
                case "Komunikacja / Korytarz": return 1.5;
                case "Pomieszczenie socjalne": return 2.0;
                default: return 1.5;
            }
        }
    }
}
