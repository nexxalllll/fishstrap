using Bloxstrap.AppData;
using Bloxstrap.Integrations;

namespace Bloxstrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;
        
        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly WindowManipulation? WindowManipulation;

        public readonly DiscordRichPresence? RichPresence;

        private readonly CustomFontInAppRelauncher? _customFontInAppRelauncher;

        private readonly AvatarPresetWatcher? _avatarPresetWatcher;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";


            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                while (gameClientProcess.MainWindowHandle == IntPtr.Zero)
                    Thread.Sleep(100);

                _watcherData = new() { ProcessId = gameClientProcess.Id, Handle = gameClientProcess.MainWindowHandle.ToInt64() };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableWindowManipulation && _watcherData.Handle != 0)
                WindowManipulation = new(_watcherData.Handle, _watcherData.ProcessId);

            if (App.Settings.Prop.EnableActivityTracking || _watcherData.EnableInAppCustomFontRelaunch || _watcherData.EnableAvatarPresetWatcher)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                if (_watcherData.EnableInAppCustomFontRelaunch)
                {
                    _customFontInAppRelauncher = new(_watcherData.ProcessId, _watcherData.CustomFontActiveForLaunch);
                    ActivityWatcher.OnGameJoining += (_, _) => _customFontInAppRelauncher.HandleGameJoining(ActivityWatcher.Data);
                }

                if (_watcherData.EnableAvatarPresetWatcher)
                {
                    _avatarPresetWatcher = new(_watcherData.AvatarPresetAppliedOutfitId);
                    ActivityWatcher.OnGameJoining += (_, _) => _avatarPresetWatcher.HandleGameJoining(ActivityWatcher.Data);
                    ActivityWatcher.OnGameLeave += (_, _) => _avatarPresetWatcher.HandleGameLeave();
                }

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence && !App.State.Prop.WatcherRunning)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Running rpc");
                    RichPresence = new(ActivityWatcher);
                }
            }

            _notifyIcon = new(this);
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            ActivityWatcher?.Start();
            WindowManipulation?.Start();

            while (Utilities.GetProcessesSafe().Any(x => x.Id == _watcherData.ProcessId))
                await Task.Delay(1000);

            if (_customFontInAppRelauncher is not null)
                await _customFontInAppRelauncher.WaitForRelaunchAsync();

            if (_avatarPresetWatcher is not null)
            {
                await WaitForRobloxPlayerProcessesToExit();
                await _avatarPresetWatcher.RestoreDefault("Roblox close");
            }

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _notifyIcon?.Dispose();
            RichPresence?.Dispose();

            App.State.Prop.WatcherRunning = false;

            GC.SuppressFinalize(this);
        }

        private static async Task WaitForRobloxPlayerProcessesToExit()
        {
            const string LOG_IDENT = "Watcher::WaitForRobloxPlayerProcessesToExit";

            bool logged = false;

            while (IsAnyRobloxPlayerProcessRunning())
            {
                if (!logged)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Waiting for remaining Roblox player processes before restoring avatar preset");
                    logged = true;
                }

                await Task.Delay(1000);
            }
        }

        private static bool IsAnyRobloxPlayerProcessRunning()
        {
            string processName = Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName);

            return Utilities.GetProcessesSafe().Any(process =>
            {
                try
                {
                    return process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
