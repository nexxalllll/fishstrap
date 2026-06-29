using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32.UI.Accessibility;

namespace Bloxstrap.Integrations
{
    public class WindowManipulation
    {
        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL2 = 2;
        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;
        private const ushort VT_LPWSTR = 31;

        private static readonly Guid AppUserModelPropertyGuid = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        private static readonly PropertyKey AppUserModelId = new(AppUserModelPropertyGuid, 5);
        private static readonly PropertyKey AppUserModelRelaunchCommand = new(AppUserModelPropertyGuid, 2);
        private static readonly PropertyKey AppUserModelRelaunchIconResource = new(AppUserModelPropertyGuid, 3);
        private static readonly PropertyKey AppUserModelRelaunchDisplayName = new(AppUserModelPropertyGuid, 4);
        private static readonly Guid IPropertyStoreGuid = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongW", SetLastError = true)]
        private static extern int SetClassLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("shell32.dll")]
        private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore propertyStore);

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out uint cProps);
            int GetAt(uint iProp, out PropertyKey pkey);
            int GetValue(ref PropertyKey key, out PropVariant pv);
            int SetValue(ref PropertyKey key, ref PropVariant pv);
            int Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public uint PropertyId;

            public PropertyKey(Guid formatId, uint propertyId)
            {
                FormatId = formatId;
                PropertyId = propertyId;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public ushort VarType;
            public ushort Reserved1;
            public ushort Reserved2;
            public ushort Reserved3;
            public IntPtr Pointer;
            public int Reserved4;
        }

        private WINEVENTPROC? _setTitleHook;

        private HWND _hWnd;
        private uint _robloxPID;

        public WindowManipulation(long windowHandle, long robloxProcessId)
        {
            const string LOG_IDENT = "WindowManipulation";

            App.Logger.WriteLine(LOG_IDENT, $"Got window handle as {windowHandle}");
            _hWnd = (HWND)(IntPtr)windowHandle; // amazing
            _robloxPID = (uint)robloxProcessId;
        }

        public void Start()
        {
            if (App.Settings.Prop.FakeBorderlessFullscreen)
                FakeBorderless();

            // we check for changes in the function, so we can safely call it here
            ApplyWindowModifications();
        }

        private void FakeBorderless()
        {
            const string LOG_IDENT = "WindowManipulation::BorderlessFullscreen";
            App.Logger.WriteLine(LOG_IDENT, "Setting Roblox to borderless fullscreen");

            const int GWLSTYLE = -16;

            int style = PInvoke.GetWindowLong(_hWnd, (WINDOW_LONG_PTR_INDEX)GWLSTYLE);

            const int WS_CAPTION = 0x00C00000;
            const int WS_THICKFRAME = 0x00040000;
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            const int WS_SYSMENU = 0x00080000;

            style &= ~WS_CAPTION;
            style &= ~WS_THICKFRAME;
            style &= ~WS_MINIMIZEBOX;
            style &= ~WS_MAXIMIZEBOX;
            style &= ~WS_SYSMENU;

            Rectangle resolution = Screen.PrimaryScreen.Bounds;

            PInvoke.SetWindowLong((HWND)_hWnd, (WINDOW_LONG_PTR_INDEX)GWLSTYLE, style);

            // hack or else it'll still be exclusive
            PInvoke.SetWindowPos((HWND)_hWnd, (HWND)IntPtr.Zero, 0, 0, resolution.Width, resolution.Height + 1, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
        }

        private void ApplyWindowModifications()
        {
            const string LOG_IDENT = "WindowManipulation::ApplyWindowModifications";
            const int WINEVENT_OUTOFCONTEXT = 0x0;
            const int EVENT_OBJECT_NAMECHANGE = 0x800C;

            App.Logger.WriteLine(LOG_IDENT, "Applying window modifications");

            _setTitleHook = new(SetWindowTitleHook);

            // icon
            App.Logger.WriteLine(LOG_IDENT, "Setting Roblox icon");
            ApplyRobloxIcon();
            _ = RefreshRobloxIconAsync();

            // title
            App.Logger.WriteLine(LOG_IDENT, "Setting Roblox title");
            string robloxTitle = App.Settings.Prop.RobloxTitle;
            if (robloxTitle != "Roblox")
            {
                PInvoke.SetWindowText(_hWnd, robloxTitle);

                // because (Internal) exists Roblox will reset the title after couple of seconds
                App.Current.Dispatcher.Invoke(() => PInvoke.SetWinEventHook(EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE, null, _setTitleHook, _robloxPID, 0, WINEVENT_OUTOFCONTEXT));
            }
        }

        private async Task RefreshRobloxIconAsync()
        {
            if (App.Settings.Prop.RobloxIcon == RobloxIcon.IconDefault)
                return;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                ApplyRobloxIcon();

                await Task.Delay(TimeSpan.FromSeconds(4));
                ApplyRobloxIcon();
            }
            catch (Exception ex)
            {
                const string LOG_IDENT = "WindowManipulation::RefreshRobloxIconAsync";
                App.Logger.WriteLine(LOG_IDENT, "Failed to refresh Roblox icon");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private void ApplyRobloxIcon()
        {
            RobloxIcon robloxIcon = App.Settings.Prop.RobloxIcon;

            if (robloxIcon == RobloxIcon.IconDefault)
                return;

            using Icon baseIcon = robloxIcon.GetIcon();
            using Icon smallIcon = baseIcon.GetSized(SystemInformation.SmallIconSize.Width, SystemInformation.SmallIconSize.Height);
            using Icon largeIcon = baseIcon.GetSized(SystemInformation.IconSize.Width, SystemInformation.IconSize.Height);

            SetWindowIcon(ICON_SMALL, smallIcon);
            SetWindowIcon(ICON_SMALL2, smallIcon);
            SetWindowIcon(ICON_BIG, largeIcon);
            SetClassIcon(GCLP_HICONSM, smallIcon);
            SetClassIcon(GCLP_HICON, largeIcon);
            SetTaskbarIcon(robloxIcon, baseIcon);
        }

        private void SetWindowIcon(int iconSize, Icon icon)
        {
            IntPtr hIconCopy = PInvoke.CopyIcon((HICON)icon.Handle);

            if (hIconCopy == IntPtr.Zero)
                return;

            PInvoke.SendMessage(_hWnd, WM_SETICON, new WPARAM((nuint)iconSize), hIconCopy);
        }

        private void SetClassIcon(int iconIndex, Icon icon)
        {
            IntPtr hIconCopy = PInvoke.CopyIcon((HICON)icon.Handle);

            if (hIconCopy == IntPtr.Zero)
                return;

            SetClassLongPtr(_hWnd, iconIndex, hIconCopy);
        }

        private static IntPtr SetClassLongPtr(HWND hWnd, int nIndex, IntPtr dwNewLong)
        {
            IntPtr windowHandle = (IntPtr)hWnd;

            if (IntPtr.Size == 8)
                return SetClassLongPtr64(windowHandle, nIndex, dwNewLong);

            return (IntPtr)SetClassLongPtr32(windowHandle, nIndex, dwNewLong.ToInt32());
        }

        private void SetTaskbarIcon(RobloxIcon robloxIcon, Icon icon)
        {
            const string LOG_IDENT = "WindowManipulation::SetTaskbarIcon";

            try
            {
                string iconLocation = GetTaskbarIconLocation(robloxIcon, icon);
                string iconId = GetTaskbarIconId(robloxIcon, iconLocation);

                Guid propertyStoreGuid = IPropertyStoreGuid;
                int hr = SHGetPropertyStoreForWindow((IntPtr)_hWnd, ref propertyStoreGuid, out IPropertyStore propertyStore);

                if (hr != 0)
                    Marshal.ThrowExceptionForHR(hr);

                SetWindowProperty(propertyStore, AppUserModelRelaunchIconResource, $"{iconLocation},0");
                SetWindowProperty(propertyStore, AppUserModelRelaunchCommand, $"\"{Paths.Process}\" -player");
                SetWindowProperty(propertyStore, AppUserModelRelaunchDisplayName, "Roblox");
                SetWindowProperty(propertyStore, AppUserModelId, $"{App.ProjectName}.Roblox.{iconId}");

                hr = propertyStore.Commit();

                if (hr != 0)
                    Marshal.ThrowExceptionForHR(hr);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to set Roblox taskbar icon");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private static void SetWindowProperty(IPropertyStore propertyStore, PropertyKey propertyKey, string value)
        {
            PropVariant propVariant = new()
            {
                VarType = VT_LPWSTR,
                Pointer = Marshal.StringToCoTaskMemUni(value)
            };

            try
            {
                int hr = propertyStore.SetValue(ref propertyKey, ref propVariant);

                if (hr != 0)
                    Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(propVariant.Pointer);
            }
        }

        private static string GetTaskbarIconLocation(RobloxIcon robloxIcon, Icon icon)
        {
            string iconId = GetTaskbarIconId(robloxIcon);
            string iconLocation = Path.Combine(Paths.Base, $"{App.ProjectName}-Roblox-{iconId}.ico");

            using var stream = File.Create(iconLocation);
            icon.Save(stream);

            return iconLocation;
        }

        private static string GetTaskbarIconId(RobloxIcon robloxIcon, string? iconLocation = null)
        {
            if (robloxIcon != RobloxIcon.IconCustom)
                return robloxIcon.ToString();

            string customIconLocation = App.Settings.Prop.RobloxIconCustomLocation;

            if (!String.IsNullOrEmpty(customIconLocation) && File.Exists(customIconLocation))
                return $"{robloxIcon}-{MD5Hash.FromFile(customIconLocation)[..8]}";

            if (!String.IsNullOrEmpty(iconLocation) && File.Exists(iconLocation))
                return $"{robloxIcon}-{MD5Hash.FromFile(iconLocation)[..8]}";

            return robloxIcon.ToString();
        }

        private void SetWindowTitleHook(HWINEVENTHOOK hWinEventHook, uint iEvent, HWND hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            const string LOG_IDENT = "WindowManipulation::SetWindowTitleHook";
            string robloxTitle = App.Settings.Prop.RobloxTitle;
            string newRobloxTitle = robloxTitle;

            Span<char> titleBuffer = new char[256];
            PInvoke.GetWindowText(_hWnd, titleBuffer);

            newRobloxTitle = titleBuffer.TrimEnd('\0').ToString();

            if (newRobloxTitle != robloxTitle)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Setting Roblox title back to {robloxTitle}");
                PInvoke.SetWindowText(_hWnd, robloxTitle);
            }
        }
    }
}
