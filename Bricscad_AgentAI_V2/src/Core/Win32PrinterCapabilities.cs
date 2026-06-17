using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Bricscad_AgentAI_V2.Core
{
    public class Win32MediaInfo
    {
        public int Index { get; set; }
        public string FormName { get; set; }
        public double WidthMmHundredths { get; set; }
        public double HeightMmHundredths { get; set; }

        public double WidthMm => WidthMmHundredths / 100.0;
        public double HeightMm => HeightMmHundredths / 100.0;

        public bool IsUserFormat => !string.IsNullOrEmpty(FormName) &&
            FormName.StartsWith("User", StringComparison.OrdinalIgnoreCase);

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0,-16} {1,7:F2}x{2,7:F2}mm", FormName, WidthMm, HeightMm);
        }
    }

    public class Win32PrinterCapabilitiesResult
    {
        public bool QuerySucceeded { get; set; }
        public string ErrorMessage { get; set; }
        public string DeviceName { get; set; }
        public List<Win32MediaInfo> MediaList { get; set; } = new List<Win32MediaInfo>();

        public List<Win32MediaInfo> UserFormats =>
            MediaList.FindAll(m => m.IsUserFormat);

        public List<Win32MediaInfo> StandardFormats =>
            MediaList.FindAll(m => !m.IsUserFormat);
    }

    public static class Win32PrinterCapabilities
    {
        private const int DC_PAPERS = 2;
        private const int DC_PAPERSIZE = 3;
        private const int DC_FORMS = 80;

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DeviceCapabilities(
            string pDevice,
            string pPort,
            int fwCapability,
            IntPtr pOutput,
            IntPtr pDevMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(int uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const int GMEM_MOVEABLE = 0x0002;
        private const int GMEM_ZEROINIT = 0x0040;

        public static Win32PrinterCapabilitiesResult QueryMedia(string deviceName, string portName = null)
        {
            var result = new Win32PrinterCapabilitiesResult { DeviceName = deviceName };
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                result.ErrorMessage = "Device name is empty.";
                return result;
            }

            string resolvedDeviceName = ResolvePc3ToPrinterName(deviceName);
            string port = string.IsNullOrEmpty(portName) ? "FILE:" : portName;

            try
            {
                int numPapers = DeviceCapabilities(resolvedDeviceName, port, DC_PAPERS, IntPtr.Zero, IntPtr.Zero);
                if (numPapers < 0)
                {
                    int err = Marshal.GetLastWin32Error();
                    result.ErrorMessage = string.Format(
                        "DeviceCapabilities(DC_PAPERS) zwrocil blad Win32 {0} dla '{1}' (zrodlo: '{2}')",
                        err, deviceName, resolvedDeviceName);
                    return result;
                }
                if (numPapers == 0)
                {
                    result.QuerySucceeded = true;
                    result.ErrorMessage = "Brak mediow w driverze.";
                    return result;
                }

                IntPtr namesPtr = IntPtr.Zero;
                IntPtr sizesPtr = IntPtr.Zero;
                try
                {
                    namesPtr = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)((numPapers + 1) * 64));
                    if (namesPtr == IntPtr.Zero)
                    {
                        result.ErrorMessage = "GlobalAlloc dla nazw zwrocilo null.";
                        return result;
                    }
                    sizesPtr = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)(numPapers * 8));
                    if (sizesPtr == IntPtr.Zero)
                    {
                        result.ErrorMessage = "GlobalAlloc dla wymiarow zwrocilo null.";
                        return result;
                    }

                    int retNames = DeviceCapabilities(resolvedDeviceName, port, DC_PAPERS, namesPtr, IntPtr.Zero);
                    int retSizes = DeviceCapabilities(resolvedDeviceName, port, DC_PAPERSIZE, sizesPtr, IntPtr.Zero);

                    if (retNames < 0 || retSizes < 0)
                    {
                        result.ErrorMessage = string.Format(
                            "Blad DeviceCapabilities: names={0}, sizes={1}", retNames, retSizes);
                        return result;
                    }

                    for (int i = 0; i < numPapers; i++)
                    {
                        IntPtr namePtr = new IntPtr(namesPtr.ToInt64() + i * 64);
                        string name = Marshal.PtrToStringAuto(namePtr);

                        short widthHundredths = Marshal.ReadInt16(sizesPtr, i * 8);
                        short heightHundredths = Marshal.ReadInt16(sizesPtr, i * 8 + 2);

                        if (string.IsNullOrEmpty(name))
                        {
                            name = string.Format("User{0}", i + 1);
                        }

                        result.MediaList.Add(new Win32MediaInfo
                        {
                            Index = i,
                            FormName = name,
                            WidthMmHundredths = widthHundredths,
                            HeightMmHundredths = heightHundredths
                        });
                    }

                    result.QuerySucceeded = true;
                }
                finally
                {
                    if (namesPtr != IntPtr.Zero) GlobalFree(namesPtr);
                    if (sizesPtr != IntPtr.Zero) GlobalFree(sizesPtr);
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "Wyjatek Win32PrinterCapabilities: " + ex.Message;
            }

            return result;
        }

        private static string ResolvePc3ToPrinterName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return deviceName;
            if (!deviceName.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase)) return deviceName;

            string baseName = System.IO.Path.GetFileNameWithoutExtension(deviceName);
            string[] prefixes = { "DWG To PDF", "Print As PDF" };
            foreach (string p in prefixes)
            {
                if (baseName.Equals(p, StringComparison.OrdinalIgnoreCase)) return p;
            }
            return baseName;
        }
    }
}