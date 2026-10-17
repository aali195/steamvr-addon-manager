using System.Runtime.InteropServices;

namespace SteamVrAddonManager.Services;

/// <summary>
/// Publishes the presets as taskbar Jump List "Tasks" so they can be launched from the pinned
/// app's right-click menu. Nothing is kept resident - the shell stores the list. Uses the process's
/// implicit AppUserModelID (no explicit AUMID is set), so no identity is created unless the user
/// opts in.
/// </summary>
internal static class TaskbarJumpList
{
    // Leave room for the fixed "Open" / "More" entries within the shell's ~10-task limit.
    private const int MaxPresetTasks = 8;
    private const int VtLpwstr = 31;

    private static readonly PROPERTYKEY TitleKey = new()
    {
        fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
        pid = 2,
    };

    public static bool Refresh(IReadOnlyList<string> presets)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        try
        {
            var destinations = (ICustomDestinationList)new CDestinationList();
            var iid = typeof(IObjectArray).GUID;
            destinations.BeginList(out _, ref iid, out _);

            var tasks = (IObjectCollection)new CEnumerableObjectCollection();
            AddTask(tasks, "Open SteamVR Addon Manager", exePath, arguments: null);

            foreach (var preset in presets.Take(MaxPresetTasks))
                AddTask(tasks, $"Apply: {preset}", exePath, $"--apply \"{preset}\"");

            if (presets.Count > MaxPresetTasks)
                AddTask(tasks, "More presets...", exePath, arguments: null);

            destinations.AddUserTasks((IObjectArray)tasks);
            destinations.CommitList();
            return true;
        }
        catch
        {
            // Shell integration is best-effort; never break the app over it.
            return false;
        }
    }

    public static bool Clear()
    {
        try
        {
            var destinations = (ICustomDestinationList)new CDestinationList();
            destinations.DeleteList(null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AddTask(IObjectCollection tasks, string title, string exePath, string? arguments)
    {
        var link = (IShellLinkW)new CShellLink();
        link.SetPath(exePath);
        link.SetIconLocation(exePath, 0);

        if (!string.IsNullOrEmpty(arguments))
            link.SetArguments(arguments);

        var store = (IPropertyStore)link;
        var value = new PROPVARIANT { vt = VtLpwstr, pointer = Marshal.StringToCoTaskMemUni(title) };
        try
        {
            var titleKey = TitleKey;
            store.SetValue(ref titleKey, ref value);
            store.Commit();
        }
        finally
        {
            Marshal.FreeCoTaskMem(value.pointer);
        }

        tasks.AddObject(link);
    }

    // --- COM interop ---------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointer;
    }

    [ComImport, Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6"), ClassInterface(ClassInterfaceType.None)]
    private class CDestinationList { }

    [ComImport, Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A"), ClassInterface(ClassInterfaceType.None)]
    private class CEnumerableObjectCollection { }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
    private class CShellLink { }

    [ComImport, Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void BeginList(out uint pcMinSlots, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, IObjectArray poa);
        void AppendKnownCategory(int category);
        void AddUserTasks(IObjectArray poa);
        void CommitList();
        void GetRemovedDestinations(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string? pszAppID);
        void AbortList();
    }

    [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectArray
    {
        void GetCount(out uint cObjects);
        void GetAt(uint iIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    }

    [ComImport, Guid("5632B1A4-E38A-400A-928A-D4CD63230295"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectCollection
    {
        // IObjectArray
        void GetCount(out uint cObjects);
        void GetAt(uint iIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        // IObjectCollection
        void AddObject([MarshalAs(UnmanagedType.Interface)] object pUnk);
        void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray poaSource);
        void RemoveObjectAt(uint uiIndex);
        void Clear();
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(IntPtr pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(IntPtr pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory(IntPtr pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments(IntPtr pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(IntPtr pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
