namespace Bloxstrap.Models
{
    internal class WatcherData
    {
        public int ProcessId { get; set; }

        public string? LogFile { get; set; }

        public List<int>? AutoclosePids { get; set; }

        public long Handle { get; set; }

        public bool EnableInAppCustomFontRelaunch { get; set; }

        public bool CustomFontActiveForLaunch { get; set; }

        public bool EnableAvatarPresetWatcher { get; set; }

        public long AvatarPresetAppliedOutfitId { get; set; }
    }
}
