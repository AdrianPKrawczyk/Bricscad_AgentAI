using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    internal static class SheetSetComHelpers
    {
        public const int CustomSheetSetPropertyFlag = 1;
        public const int CustomSheetPropertyFlag = 2;

        public sealed class CustomPropertyInfo
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public int Flags { get; set; }
        }

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

        public static bool TryInsertComponent(object parent, object component)
        {
            if (parent == null || component == null) return false;

            return TryInvoke(parent, "InsertComponent", component, new DispatchWrapper(null))
                || TryInvoke(parent, "InsertComponent", component, new UnknownWrapper(null))
                || TryInvoke(parent, "InsertComponent", component, new VariantWrapper(null))
                || TryInvoke(parent, "InsertComponent", component, null)
                || TryInvoke(parent, "InsertComponent", component)
                || TryInvoke(parent, "InsertComponent", null, component)
                || TryInvoke(parent, "InsertComponent", Type.Missing, component)
                || TryInvoke(parent, "InsertComponent", component, Type.Missing)
                || TryInvoke(parent, "AddComponent", component)
                || TryInvoke(parent, "AppendComponent", component)
                || TryInvoke(parent, "Add", component);
        }

        public static bool TryInsertComponentTyped(object parent, object component, out string error)
        {
            error = null;
            if (parent == null || component == null)
            {
                error = "Parent/component is null.";
                return false;
            }

            try
            {
                Assembly interop = LoadInteropAssembly();
                object typedParent = GetTypedComObject(interop, parent, GetContainerDispatchInterfaceName(parent));
                object typedComponent = GetTypedComObject(interop, component, "BricscadSmInterop.AcSmComponent");
                Type parentInterface = interop.GetType(GetContainerDispatchInterfaceName(parent), true);

                parentInterface.GetMethod("InsertComponent").Invoke(typedParent, new[] { typedComponent, null });
                return true;
            }
            catch (Exception ex)
            {
                error = Unwrap(ex).Message;
                return false;
            }
        }

        public static bool TryImportSheetTyped(object parent, string sourceDwg, string sourceLayout, out object sheet, out string error)
        {
            sheet = null;
            error = null;

            if (parent == null)
            {
                error = "Parent is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceDwg) || string.IsNullOrWhiteSpace(sourceLayout))
            {
                error = "SourceDwgPath/SourceLayoutName are required.";
                return false;
            }

            string dispatchError;
            if (TryImportSheetDispatch(parent, sourceDwg, sourceLayout, out sheet, out dispatchError))
                return true;

            try
            {
                Assembly interop = LoadInteropAssembly();
                object typedParent = GetTypedComObject(interop, parent, GetContainerDispatchInterfaceName(parent));
                Type parentInterface = interop.GetType(GetContainerDispatchInterfaceName(parent), true);

                Type layoutClass = interop.GetType("BricscadSmInterop.AcSmAcDbLayoutReferenceClass", true);
                object layoutReference = Activator.CreateInstance(layoutClass);
                object typedLayoutReference = GetTypedComObject(interop, layoutReference, "BricscadSmInterop.AcSmAcDbLayoutReference");
                Type layoutInterface = interop.GetType("BricscadSmInterop.IAcSmAcDbLayoutReference", true);

                layoutInterface.GetMethod("SetFileName").Invoke(typedLayoutReference, new object[] { sourceDwg });
                layoutInterface.GetMethod("SetName").Invoke(typedLayoutReference, new object[] { sourceLayout });

                sheet = parentInterface.GetMethod("ImportSheet").Invoke(typedParent, new[] { typedLayoutReference });
                return sheet != null;
            }
            catch (Exception ex)
            {
                error = $"Dispatch: {dispatchError}; Typed: {Unwrap(ex).Message}";
                return false;
            }
        }

        private static bool TryImportSheetDispatch(object parent, string sourceDwg, string sourceLayout, out object sheet, out string error)
        {
            sheet = null;
            error = null;

            try
            {
                object layoutReference = CreateComObject(
                    "BricscadSm.AcSmAcDbLayoutReference",
                    "BricscadSm.AcSmAcDbLayoutReference.23.0",
                    "BricscadSm.AcSmAcDbLayoutReference.22.0");

                if (layoutReference == null)
                {
                    error = "Nie znaleziono ProgID BricscadSm.AcSmAcDbLayoutReference.";
                    return false;
                }

                TryInvoke(layoutReference, "InitNew", parent);
                TryInvoke(layoutReference, "SetOwner", parent);

                if (!TryInvoke(layoutReference, "SetFileName", sourceDwg))
                {
                    error = "Nie udalo sie ustawic FileName na AcSmAcDbLayoutReference.";
                    return false;
                }

                if (!TryInvoke(layoutReference, "SetName", sourceLayout))
                {
                    error = "Nie udalo sie ustawic Name na AcSmAcDbLayoutReference.";
                    return false;
                }

                sheet = TryInvokeForResult(parent, "ImportSheet", out error, layoutReference)
                    ?? TryInvokeForResult(parent, "ImportSheet", out error, new DispatchWrapper(layoutReference))
                    ?? TryInvokeForResult(parent, "ImportSheet", out error, new UnknownWrapper(layoutReference))
                    ?? TryInvokeForResult(parent, "ImportSheet", out error, new VariantWrapper(layoutReference));

                return sheet != null;
            }
            catch (Exception ex)
            {
                error = Unwrap(ex).Message;
                return false;
            }
        }

        public static bool TryCreateStandaloneSubset(string name, string description, out object subset)
        {
            subset = null;
            Type subsetType = Type.GetTypeFromProgID("BricscadSm.AcSmSubset")
                              ?? Type.GetTypeFromProgID("BricscadSm.AcSmSubset.23.0")
                              ?? Type.GetTypeFromProgID("BricscadSm.AcSmSubset.22.0");

            if (subsetType == null) return false;

            try
            {
                subset = Activator.CreateInstance(subsetType);
            }
            catch
            {
                subset = null;
                return false;
            }

            if (subset == null) return false;

            TryInvoke(subset, "SetName", name);
            TryInvoke(subset, "SetDesc", description ?? name);
            return true;
        }

        public static bool TryListCustomProperties(object owner, int? requiredFlag, out CustomPropertyInfo[] properties, out string error)
        {
            properties = new CustomPropertyInfo[0];
            error = null;

            if (!TryGetCustomPropertyBag(owner, out object bag, out error))
                return false;

            object enumerator = TryInvokeForResult(bag, "GetPropertyEnumerator", out error);
            if (enumerator == null)
                return true;

            TryInvoke(enumerator, "Reset");

            var result = new System.Collections.Generic.List<CustomPropertyInfo>();
            for (int i = 0; i < 1000; i++)
            {
                if (!TryReadNextCustomProperty(enumerator, out string name, out object valueObject, out error))
                    break;

                if (string.IsNullOrWhiteSpace(name) || valueObject == null)
                    break;

                CustomPropertyInfo info = BuildCustomPropertyInfo(name, valueObject);
                if (!requiredFlag.HasValue || (info.Flags & requiredFlag.Value) == requiredFlag.Value)
                    result.Add(info);
            }

            properties = result.ToArray();
            if (properties.Length == 0 && TryListCustomPropertiesTyped(enumerator, requiredFlag, out CustomPropertyInfo[] typedProperties, out string typedError))
            {
                properties = typedProperties;
                error = typedError;
            }

            return true;
        }

        private static bool TryListCustomPropertiesTyped(object enumerator, int? requiredFlag, out CustomPropertyInfo[] properties, out string error)
        {
            properties = new CustomPropertyInfo[0];
            error = null;

            try
            {
                Assembly interop = LoadInteropAssembly();
                Type enumInterface = interop.GetType("BricscadSmInterop.IAcSmEnumProperty", true);
                object typedEnumerator = GetTypedComObject(interop, enumerator, "BricscadSmInterop.IAcSmEnumProperty");

                enumInterface.InvokeMember(
                    "Reset",
                    BindingFlags.InvokeMethod,
                    null,
                    typedEnumerator,
                    new object[0],
                    CultureInfo.InvariantCulture);

                var result = new System.Collections.Generic.List<CustomPropertyInfo>();
                for (int i = 0; i < 1000; i++)
                {
                    object[] nextArgs = { null, null };
                    ParameterModifier modifier = new ParameterModifier(2);
                    modifier[0] = true;
                    modifier[1] = true;

                    enumInterface.InvokeMember(
                        "Next",
                        BindingFlags.InvokeMethod,
                        null,
                        typedEnumerator,
                        nextArgs,
                        new[] { modifier },
                        CultureInfo.InvariantCulture,
                        null);

                    string name = nextArgs[0]?.ToString();
                    object valueObject = nextArgs[1];
                    if (string.IsNullOrWhiteSpace(name) || valueObject == null)
                        break;

                    CustomPropertyInfo info = BuildCustomPropertyInfo(name, valueObject);
                    if (!requiredFlag.HasValue || (info.Flags & requiredFlag.Value) == requiredFlag.Value)
                        result.Add(info);
                }

                properties = result.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                error = Unwrap(ex).Message;
                return false;
            }
        }

        private static bool TryReadNextCustomProperty(object enumerator, out string name, out object valueObject, out string error)
        {
            name = null;
            valueObject = null;
            error = null;

            try
            {
                dynamic dynEnumerator = enumerator;
                string dynamicName = null;
                object dynamicValue = null;
                dynEnumerator.Next(out dynamicName, out dynamicValue);

                if (!string.IsNullOrWhiteSpace(dynamicName) && dynamicValue != null)
                {
                    name = dynamicName;
                    valueObject = dynamicValue;
                    return true;
                }
            }
            catch
            {
                // Some BricsCAD COM builds do not expose the out parameters cleanly through dynamic.
            }

            object[] nextArgs = { null, null };
            try
            {
                ParameterModifier modifier = new ParameterModifier(2);
                modifier[0] = true;
                modifier[1] = true;

                enumerator.GetType().InvokeMember(
                    "Next",
                    BindingFlags.InvokeMethod,
                    null,
                    enumerator,
                    nextArgs,
                    new[] { modifier },
                    CultureInfo.InvariantCulture,
                    null);
            }
            catch (Exception ex)
            {
                error = Unwrap(ex).Message;
                return false;
            }

            name = nextArgs[0]?.ToString();
            valueObject = nextArgs[1];
            return !string.IsNullOrWhiteSpace(name) && valueObject != null;
        }

        public static bool TryGetCustomProperty(object owner, string name, out CustomPropertyInfo property, out string error)
        {
            property = null;
            error = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "PropertyName is required.";
                return false;
            }

            if (!TryGetCustomPropertyBag(owner, out object bag, out error))
                return false;

            object valueObject = TryInvokeForResult(bag, "GetProperty", out error, name);
            if (valueObject == null)
                return false;

            property = BuildCustomPropertyInfo(name, valueObject);
            return true;
        }

        public static bool TrySetCustomProperty(object owner, string name, string value, int flags, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "PropertyName is required.";
                return false;
            }

            if (!TryGetCustomPropertyBag(owner, out object bag, out error))
                return false;

            object propertyValue = TryInvokeForResult(bag, "GetProperty", out _, name)
                ?? CreateCustomPropertyValue(bag);

            if (propertyValue == null)
            {
                error = "Nie udalo sie utworzyc AcSmCustomPropertyValue.";
                return false;
            }

            if (!TryInvoke(propertyValue, "SetValue", value ?? string.Empty))
            {
                error = "Nie udalo sie ustawic wartosci custom property.";
                return false;
            }

            if (!TrySetCustomPropertyFlags(propertyValue, flags))
            {
                error = "Nie udalo sie ustawic flag custom property.";
                return false;
            }

            if (TryInvoke(bag, "SetProperty", name, propertyValue) ||
                TryInvoke(bag, "SetProperty", name, new DispatchWrapper(propertyValue)) ||
                TryInvoke(bag, "SetProperty", name, new UnknownWrapper(propertyValue)) ||
                TryInvoke(bag, "SetProperty", name, new VariantWrapper(propertyValue)))
            {
                return true;
            }

            error = "COM odrzucil SetProperty(name, value).";
            return false;
        }

        public static void TryUpdateSheetCustomProps(object sheetSet)
        {
            TryInvoke(sheetSet, "UpdateSheetCustomProps");
        }

        public static string DescribeCustomPropertyBag(object owner, string[] knownNames)
        {
            var sb = new StringBuilder();

            if (!TryGetCustomPropertyBag(owner, out object bag, out string error))
            {
                sb.AppendLine($"  Bag: BRAK ({error})");
                return sb.ToString();
            }

            sb.AppendLine($"  Bag: {DescribeComObject(bag)}");

            if (TryListCustomProperties(owner, null, out CustomPropertyInfo[] properties, out error))
            {
                sb.AppendLine($"  Enumerator count: {properties.Length}");
                foreach (CustomPropertyInfo property in properties)
                    sb.AppendLine($"    enum: {property.Name} = {property.Value} (Flags={property.Flags})");
            }
            else
            {
                sb.AppendLine($"  Enumerator BLAD: {error}");
            }

            if (knownNames != null)
            {
                foreach (string name in knownNames)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (TryGetCustomProperty(owner, name, out CustomPropertyInfo property, out error))
                    {
                        sb.AppendLine($"    direct {name}: {property.Value} (Flags={property.Flags})");
                    }
                    else
                    {
                        sb.AppendLine($"    direct {name}: BRAK ({error ?? "null"})");
                    }
                }
            }

            return sb.ToString();
        }

        public static string DescribeComObject(object value)
        {
            if (value == null) return "<null>";

            var sb = new StringBuilder();
            sb.Append(value.GetType().FullName);

            string comTypeName;
            if (TryGetString(value, "GetTypeName", out comTypeName))
            {
                sb.Append(" / ");
                sb.Append(comTypeName);
            }

            string name;
            if (TryGetString(value, "GetName", out name))
            {
                sb.Append(" / Name=");
                sb.Append(name);
            }

            return sb.ToString();
        }

        public static string InvokeForDiagnostics(string label, object target, string methodName, params object[] args)
        {
            if (target == null)
                return $"{label}: TARGET NULL";

            try
            {
                object result = target.GetType().InvokeMember(
                    methodName,
                    BindingFlags.InvokeMethod,
                    null,
                    target,
                    args,
                    CultureInfo.InvariantCulture);

                return $"{label}: OK -> {DescribeComObject(result)}";
            }
            catch (MissingMethodException ex)
            {
                return $"{label}: MissingMethodException: {ex.Message}";
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                return $"{label}: TargetInvocationException/{inner.GetType().Name}: {inner.Message}";
            }
            catch (Exception ex)
            {
                return $"{label}: {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static string InvokeTypedInsertForDiagnostics(string label, object parent, object component)
        {
            string error;
            return TryInsertComponentTyped(parent, component, out error)
                ? $"{label}: OK"
                : $"{label}: {error}";
        }

        public static string InvokeTypedImportForDiagnostics(string label, object parent, string sourceDwg, string sourceLayout)
        {
            object sheet;
            string error;
            return TryImportSheetTyped(parent, sourceDwg, sourceLayout, out sheet, out error)
                ? $"{label}: OK -> {DescribeComObject(sheet)}"
                : $"{label}: {error}";
        }

        public static string InvokeDispatchImportForDiagnostics(string label, object parent, string sourceDwg, string sourceLayout)
        {
            object sheet;
            string error;
            return TryImportSheetDispatch(parent, sourceDwg, sourceLayout, out sheet, out error)
                ? $"{label}: OK -> {DescribeComObject(sheet)}"
                : $"{label}: {error}";
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

        private static object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null) return null;

            return target.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                target,
                args,
                CultureInfo.InvariantCulture);
        }

        private static object TryInvokeForResult(object target, string methodName, out string error, params object[] args)
        {
            error = null;
            try
            {
                return Invoke(target, methodName, args);
            }
            catch (Exception ex)
            {
                error = Unwrap(ex).Message;
                return null;
            }
        }

        private static object CreateComObject(params string[] progIds)
        {
            foreach (string progId in progIds)
            {
                Type type = Type.GetTypeFromProgID(progId);
                if (type == null) continue;

                try
                {
                    return Activator.CreateInstance(type);
                }
                catch
                {
                    // Try the next ProgID variant.
                }
            }

            return null;
        }

        private static bool TryGetCustomPropertyBag(object owner, out object bag, out string error)
        {
            bag = null;
            error = null;

            if (owner == null)
            {
                error = "Owner is null.";
                return false;
            }

            bag = TryInvokeForResult(owner, "GetCustomPropertyBag", out error);
            if (bag != null) return true;

            error = "Nie udalo sie pobrac GetCustomPropertyBag().";
            return false;
        }

        private static object CreateCustomPropertyValue(object owner)
        {
            object value = CreateComObject(
                "BricscadSm.AcSmCustomPropertyValue",
                "BricscadSm.AcSmCustomPropertyValue.23.0",
                "BricscadSm.AcSmCustomPropertyValue.22.0");

            if (value == null)
            {
                try
                {
                    Assembly interop = LoadInteropAssembly();
                    Type valueClass = interop.GetType("BricscadSmInterop.AcSmCustomPropertyValueClass", false);
                    if (valueClass != null)
                        value = Activator.CreateInstance(valueClass);
                }
                catch
                {
                    value = null;
                }
            }

            if (value != null)
            {
                TryInvoke(value, "InitNew", owner);
                TryInvoke(value, "SetOwner", owner);
            }

            return value;
        }

        private static CustomPropertyInfo BuildCustomPropertyInfo(string name, object propertyValue)
        {
            string error;
            object rawValue = TryInvokeForResult(propertyValue, "GetValue", out error);
            object rawFlags = TryInvokeForResult(propertyValue, "GetFlags", out error);

            int flags = 0;
            if (rawFlags != null)
            {
                try { flags = Convert.ToInt32(rawFlags, CultureInfo.InvariantCulture); }
                catch { flags = 0; }
            }

            return new CustomPropertyInfo
            {
                Name = name,
                Value = rawValue?.ToString() ?? string.Empty,
                Flags = flags
            };
        }

        private static bool TrySetCustomPropertyFlags(object propertyValue, int flags)
        {
            if (TryInvoke(propertyValue, "SetFlags", flags))
                return true;

            try
            {
                Assembly interop = LoadInteropAssembly();
                Type flagsType = interop.GetType("BricscadSmInterop.PropertyFlags", false);
                if (flagsType != null)
                {
                    object enumValue = Enum.ToObject(flagsType, flags);
                    if (TryInvoke(propertyValue, "SetFlags", enumValue))
                        return true;
                }
            }
            catch
            {
                // Keep the late-bound path authoritative; this is only a fallback.
            }

            return false;
        }

        private static Assembly LoadInteropAssembly()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(baseDir, "BricscadSm.Interop.dll");
            if (File.Exists(localPath))
                return Assembly.LoadFrom(localPath);

            string projectLibPath = Path.Combine(baseDir, "lib", "BricscadSm.Interop.dll");
            if (File.Exists(projectLibPath))
                return Assembly.LoadFrom(projectLibPath);

            string tmpPath = @"C:\tmp\BricscadSm.Interop.dll";
            if (File.Exists(tmpPath))
                return Assembly.LoadFrom(tmpPath);

            throw new FileNotFoundException("Nie znaleziono BricscadSm.Interop.dll.");
        }

        private static object GetTypedComObject(Assembly interop, object comObject, string interfaceName)
        {
            Type interfaceType = interop.GetType(interfaceName, true);
            IntPtr unknown = Marshal.GetIUnknownForObject(comObject);
            try
            {
                return Marshal.GetTypedObjectForIUnknown(unknown, interfaceType);
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        private static string GetContainerDispatchInterfaceName(object container)
        {
            string typeName;
            if (TryGetString(container, "GetTypeName", out typeName) &&
                string.Equals(typeName, "AcSmSubset", StringComparison.OrdinalIgnoreCase))
            {
                return "BricscadSmInterop.IAcSmSubset";
            }

            return "BricscadSmInterop.IAcSmSheetSet";
        }

        private static Exception Unwrap(Exception ex)
        {
            if (ex is TargetInvocationException targetEx && targetEx.InnerException != null)
                return targetEx.InnerException;

            return ex;
        }

        private static bool TryGetString(object target, string methodName, out string value)
        {
            value = null;
            if (target == null) return false;

            try
            {
                object result = target.GetType().InvokeMember(
                    methodName,
                    BindingFlags.InvokeMethod,
                    null,
                    target,
                    new object[0],
                    CultureInfo.InvariantCulture);
                value = result?.ToString();
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }
    }
}
