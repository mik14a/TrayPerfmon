using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TrayPerfmon.Plugin.GpuUsage
{
    class GpuInfo
    {
        public string InstanceName { get; init; }
        public string Name { get; init; }

        public static List<GpuInfo> GetGpuAdapters() {
            var result = new List<GpuInfo>();
            var hr = CreateDXGIFactory1(ref IID_IDXGIFactory1, out var factory);
            if (hr != 0 || factory == IntPtr.Zero) return result;
            try {
                var vtbl = Marshal.ReadIntPtr(factory);
                var enumPtr = Marshal.ReadIntPtr(vtbl, 12 * IntPtr.Size);
                var enumAdapters1 = (EnumAdapters1Delegate)Marshal.GetDelegateForFunctionPointer(enumPtr, typeof(EnumAdapters1Delegate));

                var i = 0;
                while (true) {
                    var adapter = IntPtr.Zero;
                    hr = enumAdapters1(factory, (uint)i, out adapter);
                    if (hr != 0 || adapter == IntPtr.Zero) break;
                    try {
                        var aVtbl = Marshal.ReadIntPtr(adapter);
                        var getDescPtr = Marshal.ReadIntPtr(aVtbl, 12 * IntPtr.Size);
                        var getDesc1 = (GetDesc1Delegate)Marshal.GetDelegateForFunctionPointer(getDescPtr, typeof(GetDesc1Delegate));
                        hr = getDesc1(adapter, out var desc);
                        if (hr != 0) {
                            getDescPtr = Marshal.ReadIntPtr(aVtbl, 11 * IntPtr.Size);
                            getDesc1 = (GetDesc1Delegate)Marshal.GetDelegateForFunctionPointer(getDescPtr, typeof(GetDesc1Delegate));
                            hr = getDesc1(adapter, out desc);
                        }
                        if (hr == 0) {
                            var l = desc.AdapterLuid;
                            // Match GPU Engine fragment: luid_0xHHHHHHHH_0xLLLLLLLL_phys_0
                            var instance = string.Format("luid_0x{0:X8}_0x{1:X8}_phys_0", (uint)l.HighPart, l.LowPart);
                            var name = (desc.Description ?? "GPU").TrimEnd('\0', ' ');
                            if (string.IsNullOrWhiteSpace(name)) name = "GPU" + i;
                            result.Add(new GpuInfo { InstanceName = instance, Name = name });
                        }
                    } finally {
                        if (adapter != IntPtr.Zero) Marshal.Release(adapter);
                    }
                    ++i;
                }
            } finally {
                if (factory != IntPtr.Zero) Marshal.Release(factory);
            }
            return result;
        }

        [DllImport("dxgi.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

        static Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int EnumAdapters1Delegate(IntPtr factory, uint index, out IntPtr adapter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetDesc1Delegate(IntPtr adapter, out DXGI_ADAPTER_DESC1 desc);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DXGI_ADAPTER_DESC1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public ulong DedicatedVideoMemory;
            public ulong DedicatedSystemMemory;
            public ulong SharedSystemMemory;
            public LUID AdapterLuid;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
    }
}
