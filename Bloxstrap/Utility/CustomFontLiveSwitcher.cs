namespace Bloxstrap.Utility
{
    internal static class CustomFontRules
    {
        internal static bool HasRules =>
            App.Settings.Prop.CustomFontPlaceIds.Count != 0 ||
            App.Settings.Prop.CustomFontExcludedPlaceIds.Count != 0;

        internal static bool ShouldApply(long? placeId)
        {
            if (!File.Exists(Paths.CustomFont))
                return false;

            var includedPlaceIds = App.Settings.Prop.CustomFontPlaceIds;
            var excludedPlaceIds = App.Settings.Prop.CustomFontExcludedPlaceIds;

            if (!HasRules)
                return true;

            if (placeId is null)
                return false;

            return !excludedPlaceIds.Contains(placeId.Value) &&
                (includedPlaceIds.Count == 0 || includedPlaceIds.Contains(placeId.Value));
        }
    }

    internal sealed class CustomFontVersionRestorer
    {
        private const string LOG_IDENT = "CustomFontVersionRestorer";

        private readonly string _versionFontsFolder;
        private readonly string _versionFamiliesFolder;
        private readonly string _cacheFamiliesFolder;

        internal CustomFontVersionRestorer(string versionDirectory)
        {
            _versionFontsFolder = Path.Combine(versionDirectory, "content\\fonts");
            _versionFamiliesFolder = Path.Combine(_versionFontsFolder, "families");
            _cacheFamiliesFolder = Path.Combine(
                Paths.Base,
                "CustomFontCache",
                App.RobloxState.Prop.Player.VersionGuid,
                "families"
            );
        }

        internal void RestoreNormalFont()
        {
            try
            {
                if (Directory.Exists(_cacheFamiliesFolder))
                {
                    foreach (string originalFile in Directory.GetFiles(_cacheFamiliesFolder, "*.json"))
                    {
                        string versionFile = Path.Combine(_versionFamiliesFolder, Path.GetFileName(originalFile));
                        Filesystem.AssertReadOnly(versionFile);
                        File.Copy(originalFile, versionFile, true);
                    }
                }

                string versionCustomFont = Path.Combine(_versionFontsFolder, "CustomFont.ttf");

                if (File.Exists(versionCustomFont))
                {
                    Filesystem.AssertReadOnly(versionCustomFont);
                    File.Delete(versionCustomFont);
                }

                App.Logger.WriteLine(LOG_IDENT, "Restored normal font files");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to restore normal font files");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }

    internal sealed class CustomFontInAppRelauncher
    {
        private const string LOG_IDENT = "CustomFontInAppRelauncher";

        private readonly int _processId;
        private readonly bool _customFontActiveForLaunch;

        private int _relaunchStarted;
        private Task? _relaunchTask;

        internal CustomFontInAppRelauncher(int processId, bool customFontActiveForLaunch)
        {
            _processId = processId;
            _customFontActiveForLaunch = customFontActiveForLaunch;
        }

        internal void HandleGameJoining(ActivityData data)
        {
            if (data.IsTeleport)
            {
                App.Logger.WriteLine(
                    LOG_IDENT,
                    $"In-experience teleport to PlaceId {data.PlaceId}; retaining the font loaded for the original experience"
                );
                return;
            }

            bool shouldApply = CustomFontRules.ShouldApply(data.PlaceId);
            App.Logger.WriteLine(
                LOG_IDENT,
                $"In-app join PlaceId is {data.PlaceId}; loaded custom font: {_customFontActiveForLaunch}; required custom font: {shouldApply}"
            );

            if (shouldApply == _customFontActiveForLaunch)
                return;

            if (Interlocked.Exchange(ref _relaunchStarted, 1) != 0)
                return;

            string deeplink = data.GetInviteDeeplink(false, true);
            _relaunchTask = RelaunchAsync(deeplink);
        }

        internal async Task WaitForRelaunchAsync()
        {
            Task? relaunchTask = _relaunchTask;

            if (relaunchTask is not null)
                await relaunchTask;
        }

        private async Task RelaunchAsync(string deeplink)
        {
            try
            {
                App.Logger.WriteLine(LOG_IDENT, $"Reopening Roblox with the required font state ({deeplink})");

                using (var process = Process.GetProcessById(_processId))
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();

                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                        try
                        {
                            await process.WaitForExitAsync(timeout.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            if (!process.HasExited)
                            {
                                App.Logger.WriteLine(LOG_IDENT, "Roblox did not close in time; closing it forcefully");
                                process.Kill();
                                await process.WaitForExitAsync();
                            }
                        }
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    UseShellExecute = false
                };

                startInfo.ArgumentList.Add(deeplink);
                startInfo.ArgumentList.Add("-quiet");
                Process.Start(startInfo);

                App.Logger.WriteLine(LOG_IDENT, "Started the internal Roblox relaunch");
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _relaunchStarted, 0);
                App.Logger.WriteLine(LOG_IDENT, "Failed to reopen Roblox with the required font state");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
