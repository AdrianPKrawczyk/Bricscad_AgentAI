using System;
using System.Globalization;
using System.Reflection;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    internal static class SheetSetComHelpers
    {
        public static bool TryLockDatabase(object manager, object database)
        {
            return TryInvoke(manager, "LockDatabase", database, true)
                || TryInvoke(manager, "LockDb", database, true)
                || TryInvoke(manager, "LockDb", database)
                || TryInvoke(database, "LockDatabase", database, true)
                || TryInvoke(database, "LockDb", database, true)
                || TryInvoke(database, "LockDb", database)
                || TryInvoke(database, "LockDatabase", true)
                || TryInvoke(database, "LockDb", true)
                || TryInvoke(database, "Lock", true);
        }

        public static void TryUnlockDatabase(object manager, object database)
        {
            if (database == null) return;

            if (TryInvoke(manager, "UnlockDatabase", database, true)) return;
            if (TryInvoke(manager, "UnlockDb", database, true)) return;
            if (TryInvoke(manager, "UnlockDb", database)) return;
            if (TryInvoke(database, "UnlockDatabase", database, true)) return;
            if (TryInvoke(database, "UnlockDb", database, true)) return;
            if (TryInvoke(database, "UnlockDb", database)) return;
            if (TryInvoke(database, "UnlockDatabase", true)) return;
            if (TryInvoke(database, "UnlockDb", true)) return;
            TryInvoke(database, "Unlock", true);
        }

        public static void TrySaveDatabase(object manager, object database, string path)
        {
            if (database == null) return;

            if (TryInvoke(database, "Save")) return;
            if (!string.IsNullOrWhiteSpace(path) && TryInvoke(database, "SaveAs", path)) return;
            if (TryInvoke(database, "SaveDatabase")) return;
            if (TryInvoke(manager, "SaveDatabase", database)) return;
            if (TryInvoke(database, "Commit")) return;
            TryInvoke(database, "CommitChanges");
        }

        public static void TryCloseDatabase(object manager, object database)
        {
            if (database == null) return;

            if (TryInvoke(manager, "CloseDatabase", database)) return;
            TryInvoke(database, "Close");
        }

        private static bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null) return false;

            try
            {
                target.GetType().InvokeMember(
                    methodName,
                    BindingFlags.InvokeMethod,
                    null,
                    target,
                    args,
                    CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
