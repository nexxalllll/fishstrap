namespace Bloxstrap.Enums
{
    public enum BootstrapperStyle
    {
        // These values are persisted in Settings.json. Keep existing values stable.
        VistaDialog = 0,
        LegacyDialog2008 = 1,
        LegacyDialog2011 = 2,
        ProgressDialog = 3,
        ClassicFluentDialog = 4,
        TwentyFiveDialog = 5,
        ByfronDialog = 6,
        [EnumName(StaticName = "Fishstrap")]
        FluentDialog = 7,
        FluentAeroDialog = 8,
        CustomDialog = 9,
        TerminalDialog = 10
    }
}
