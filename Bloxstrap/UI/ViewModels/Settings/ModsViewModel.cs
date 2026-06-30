using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;

using Microsoft.Win32;

using Windows.Win32;
using Windows.Win32.UI.Shell;
using Windows.Win32.Foundation;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Models.SettingTasks;
using Bloxstrap.AppData;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ModsViewModel : NotifyPropertyChangedViewModel
    {
        private string _avatarOutfitLoadStatus = "Load saved outfits to populate the dropdowns.";
        private AvatarPresetRule? _selectedAvatarPresetRule;

        private void OpenModsFolder() => Process.Start("explorer.exe", Paths.Modifications);

        private readonly Dictionary<string, byte[]> FontHeaders = new()
        {
            { "ttf", new byte[4] { 0x00, 0x01, 0x00, 0x00 } },
            { "otf", new byte[4] { 0x4F, 0x54, 0x54, 0x4F } },
            { "ttc", new byte[4] { 0x74, 0x74, 0x63, 0x66 } } 
        };

        private void ManageCustomFont()
        {
            if (!String.IsNullOrEmpty(TextFontTask.NewState))
            {
                TextFontTask.NewState = "";
            }
            else
            {
                var dialog = new OpenFileDialog
                {
                    Filter = $"{Strings.Menu_FontFiles}|*.ttf;*.otf;*.ttc"
                };

                if (dialog.ShowDialog() != true)
                    return;

                string type = dialog.FileName.Substring(dialog.FileName.Length-3, 3).ToLowerInvariant();

                if (!FontHeaders.ContainsKey(type) 
                    || !FontHeaders.Any(x => File.ReadAllBytes(dialog.FileName).Take(4).SequenceEqual(x.Value)))
                {
                    Frontend.ShowMessageBox(Strings.Menu_Mods_Misc_CustomFont_Invalid, MessageBoxImage.Error);
                    return;
                }

                TextFontTask.NewState = dialog.FileName;
            }

            OnPropertyChanged(nameof(ChooseCustomFontVisibility));
            OnPropertyChanged(nameof(DeleteCustomFontVisibility));
        }

        public ICommand OpenModsFolderCommand => new RelayCommand(OpenModsFolder);

        public Visibility ChooseCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DeleteCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ManageCustomFontCommand => new RelayCommand(ManageCustomFont);

        public ICommand AddAvatarPresetRuleCommand => new RelayCommand(AddAvatarPresetRule);

        public ICommand DeleteAvatarPresetRuleCommand => new RelayCommand(DeleteAvatarPresetRule);

        public ICommand LoadAvatarOutfitsCommand => new AsyncRelayCommand(LoadAvatarOutfits);

        public string CustomFontPlaceIdsText
        {
            get => String.Join(
                Environment.NewLine,
                App.Settings.Prop.CustomFontPlaceIds
                    .Select(placeId => placeId.ToString())
                    .Concat(App.Settings.Prop.CustomFontExcludedPlaceIds.Select(placeId => $"!{placeId}"))
            );
            set
            {
                var includedPlaceIds = new List<long>();
                var excludedPlaceIds = new List<long>();

                foreach (Match match in Regex.Matches(value ?? String.Empty, @"!?\d+"))
                {
                    bool excluded = match.Value.StartsWith('!');
                    string placeIdText = excluded ? match.Value[1..] : match.Value;

                    if (!long.TryParse(placeIdText, out long placeId) || placeId <= 0)
                        continue;

                    if (excluded)
                        excludedPlaceIds.Add(placeId);
                    else
                        includedPlaceIds.Add(placeId);
                }

                App.Settings.Prop.CustomFontExcludedPlaceIds = excludedPlaceIds.Distinct().ToList();
                App.Settings.Prop.CustomFontPlaceIds = includedPlaceIds
                    .Where(placeId => !App.Settings.Prop.CustomFontExcludedPlaceIds.Contains(placeId))
                    .Distinct()
                    .ToList();
            }
        }

        public bool AvatarPresetSwitcherEnabled
        {
            get => App.Settings.Prop.EnableAvatarPresetSwitcher;
            set => App.Settings.Prop.EnableAvatarPresetSwitcher = value;
        }

        public bool RestoreAvatarPresetOnLeave
        {
            get => App.Settings.Prop.RestoreAvatarPresetOnLeave;
            set => App.Settings.Prop.RestoreAvatarPresetOnLeave = value;
        }

        public string DefaultAvatarOutfitIdText
        {
            get => App.Settings.Prop.DefaultAvatarOutfitId > 0 ? App.Settings.Prop.DefaultAvatarOutfitId.ToString() : "";
            set
            {
                App.Settings.Prop.DefaultAvatarOutfitId = ParsePositiveLong(value);
                OnPropertyChanged(nameof(DefaultAvatarOutfitIdText));
                OnPropertyChanged(nameof(DefaultAvatarOutfitId));
            }
        }

        public long DefaultAvatarOutfitId
        {
            get => App.Settings.Prop.DefaultAvatarOutfitId;
            set
            {
                App.Settings.Prop.DefaultAvatarOutfitId = value;
                OnPropertyChanged(nameof(DefaultAvatarOutfitId));
                OnPropertyChanged(nameof(DefaultAvatarOutfitIdText));
            }
        }

        public ObservableCollection<AvatarPresetRule> AvatarPresetRules => App.Settings.Prop.AvatarPresetRules;

        public ObservableCollection<AvatarOutfitModel> AvatarOutfits { get; } = new();

        public string AvatarOutfitLoadStatus
        {
            get => _avatarOutfitLoadStatus;
            set
            {
                _avatarOutfitLoadStatus = value;
                OnPropertyChanged(nameof(AvatarOutfitLoadStatus));
            }
        }

        public AvatarPresetRule? SelectedAvatarPresetRule
        {
            get => _selectedAvatarPresetRule;
            set
            {
                _selectedAvatarPresetRule = value;

                if (_selectedAvatarPresetRule is not null)
                    UpdateAvatarPresetRuleOutfitName(_selectedAvatarPresetRule);

                OnPropertyChanged(nameof(SelectedAvatarPresetRule));
                OnPropertyChanged(nameof(IsAvatarPresetRuleSelected));
                OnPropertyChanged(nameof(SelectedAvatarPresetRulePlaceIdText));
                OnPropertyChanged(nameof(SelectedAvatarPresetRuleOutfitIdText));
                OnPropertyChanged(nameof(SelectedAvatarPresetRuleOutfitId));
            }
        }

        public int SelectedAvatarPresetRuleIndex { get; set; } = -1;

        public bool IsAvatarPresetRuleSelected => SelectedAvatarPresetRule is not null;

        public string SelectedAvatarPresetRulePlaceIdText
        {
            get => SelectedAvatarPresetRule?.PlaceId > 0 ? SelectedAvatarPresetRule.PlaceId.ToString() : "";
            set
            {
                if (SelectedAvatarPresetRule is null)
                    return;

                SelectedAvatarPresetRule.PlaceId = ParsePositiveLong(value);
                OnPropertyChanged(nameof(SelectedAvatarPresetRulePlaceIdText));
            }
        }

        public string SelectedAvatarPresetRuleOutfitIdText
        {
            get => SelectedAvatarPresetRule?.OutfitId > 0 ? SelectedAvatarPresetRule.OutfitId.ToString() : "";
            set
            {
                if (SelectedAvatarPresetRule is null)
                    return;

                SelectedAvatarPresetRule.OutfitId = ParsePositiveLong(value);
                OnPropertyChanged(nameof(SelectedAvatarPresetRuleOutfitIdText));
                OnPropertyChanged(nameof(SelectedAvatarPresetRuleOutfitId));
            }
        }

        public long SelectedAvatarPresetRuleOutfitId
        {
            get => SelectedAvatarPresetRule?.OutfitId ?? 0;
            set
            {
                if (SelectedAvatarPresetRule is null)
                    return;

                SelectedAvatarPresetRule.OutfitId = value;
                UpdateAvatarPresetRuleOutfitName(SelectedAvatarPresetRule);
                OnPropertyChanged(nameof(SelectedAvatarPresetRuleOutfitId));
                OnPropertyChanged(nameof(SelectedAvatarPresetRuleOutfitIdText));
            }
        }

        public ICommand OpenCompatSettingsCommand => new RelayCommand(OpenCompatSettings);

        public ModPresetTask OldAvatarBackgroundTask { get; } = new("OldAvatarBackground", @"ExtraContent\places\Mobile.rbxl", "OldAvatarBackground.rbxl");

        public ModPresetTask OldCharacterSoundsTask { get; } = new("OldCharacterSounds", new()
        {
            { @"content\sounds\action_footsteps_plastic.mp3", "Sounds.OldWalk.mp3"  },
            { @"content\sounds\action_jump.mp3",              "Sounds.OldJump.mp3"  },
            { @"content\sounds\action_get_up.mp3",            "Sounds.OldGetUp.mp3" },
            { @"content\sounds\action_falling.mp3",           "Sounds.Empty.mp3"    },
            { @"content\sounds\action_jump_land.mp3",         "Sounds.Empty.mp3"    },
            { @"content\sounds\action_swim.mp3",              "Sounds.Empty.mp3"    },
            { @"content\sounds\impact_water.mp3",             "Sounds.Empty.mp3"    }
        });

        public EmojiModPresetTask EmojiFontTask { get; } = new();

        public EnumModPresetTask<Enums.CursorType> CursorTypeTask { get; } = new("CursorType", new()
        {
            {
                Enums.CursorType.From2006, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.From2006.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.From2006.ArrowFarCursor.png" }
                }
            },
            {
                Enums.CursorType.From2013, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.From2013.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.From2013.ArrowFarCursor.png" }
                }
            }
        });

        public FontModPresetTask TextFontTask { get; } = new();

        private void OpenCompatSettings()
        {
            string path = new RobloxPlayerData().ExecutablePath;

            if (File.Exists(path))
                PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, "Compatibility");
            else
                Frontend.ShowMessageBox(Strings.Common_RobloxNotInstalled, MessageBoxImage.Error);

        }

        private void AddAvatarPresetRule()
        {
            AvatarPresetRules.Add(new AvatarPresetRule());
            SelectedAvatarPresetRule = AvatarPresetRules.Last();
            SelectedAvatarPresetRuleIndex = AvatarPresetRules.Count - 1;
            OnPropertyChanged(nameof(SelectedAvatarPresetRuleIndex));
            OnPropertyChanged(nameof(IsAvatarPresetRuleSelected));
        }

        private void DeleteAvatarPresetRule()
        {
            if (SelectedAvatarPresetRule is null)
                return;

            int removedIndex = AvatarPresetRules.IndexOf(SelectedAvatarPresetRule);
            AvatarPresetRules.Remove(SelectedAvatarPresetRule);

            if (AvatarPresetRules.Count == 0)
            {
                SelectedAvatarPresetRule = null;
                SelectedAvatarPresetRuleIndex = -1;
            }
            else
            {
                SelectedAvatarPresetRuleIndex = Math.Clamp(removedIndex, 0, AvatarPresetRules.Count - 1);
                SelectedAvatarPresetRule = AvatarPresetRules[SelectedAvatarPresetRuleIndex];
            }

            OnPropertyChanged(nameof(SelectedAvatarPresetRuleIndex));
            OnPropertyChanged(nameof(IsAvatarPresetRuleSelected));
        }

        private async Task LoadAvatarOutfits()
        {
            try
            {
                AvatarOutfitLoadStatus = "Loading saved outfits...";

                if (!App.Settings.Prop.AllowCookieAccess)
                {
                    AvatarOutfitLoadStatus = "Enable Account Access on the Bootstrapper page first.";
                    return;
                }

                var outfits = await AvatarPresetSwitcher.GetSavedOutfits();

                AvatarOutfits.Clear();

                foreach (var outfit in outfits)
                    AvatarOutfits.Add(outfit);

                foreach (var rule in AvatarPresetRules)
                    UpdateAvatarPresetRuleOutfitName(rule);

                AvatarOutfitLoadStatus = outfits.Count == 0
                    ? "No saved outfits were found for the signed-in account."
                    : $"Loaded {outfits.Count} saved outfits. Personal creations are listed first.";
            }
            catch (Exception ex)
            {
                AvatarOutfitLoadStatus = "Saved outfits could not be loaded.";
                App.Logger.WriteLine("ModsViewModel::LoadAvatarOutfits", "Failed to load avatar outfits");
                App.Logger.WriteException("ModsViewModel::LoadAvatarOutfits", ex);
            }
        }

        private static long ParsePositiveLong(string? value)
        {
            if (long.TryParse(value, out long number) && number > 0)
                return number;

            return 0;
        }

        private void UpdateAvatarPresetRuleOutfitName(AvatarPresetRule rule)
        {
            var outfit = AvatarOutfits.FirstOrDefault(x => x.Id == rule.OutfitId);

            if (outfit is null || rule.OutfitName == outfit.Name)
                return;

            rule.OutfitName = outfit.Name;
        }
    }
}
