namespace Bloxstrap.Utility
{
    internal static class AvatarPresetRules
    {
        internal static bool Enabled => App.Settings.Prop.EnableAvatarPresetSwitcher;

        internal static long DefaultOutfitId => App.Settings.Prop.DefaultAvatarOutfitId;

        internal static bool HasRules =>
            Enabled &&
            (DefaultOutfitId > 0 || App.Settings.Prop.AvatarPresetRules.Any(rule => rule.PlaceId > 0 && rule.OutfitId > 0));

        internal static long? GetOutfitIdForPlace(long? placeId)
        {
            if (!Enabled)
                return null;

            if (placeId is not null)
            {
                var rule = App.Settings.Prop.AvatarPresetRules.FirstOrDefault(x => x.PlaceId == placeId.Value && x.OutfitId > 0);

                if (rule is not null)
                    return rule.OutfitId;
            }

            return DefaultOutfitId > 0 ? DefaultOutfitId : null;
        }
    }

    internal class AvatarPresetApplyResult
    {
        public long AppliedOutfitId { get; set; }

        public bool RestoreDefaultOnLeave { get; set; }
    }

    internal class AvatarPresetSwitcher
    {
        private const string LOG_IDENT = "AvatarPresetSwitcher";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly SemaphoreSlim _switchSemaphore = new(1, 1);
        private long _currentOutfitId;
        private bool _defaultRestored;

        internal AvatarPresetSwitcher(long currentOutfitId = 0)
        {
            _currentOutfitId = currentOutfitId;
            _defaultRestored = currentOutfitId == AvatarPresetRules.DefaultOutfitId;
        }

        internal static async Task<IReadOnlyList<AvatarOutfitModel>> GetSavedOutfits()
        {
            await App.Cookies.EnsureLoadedAsync();

            var user = await App.Cookies.GetAuthenticated();

            if (user is null || user.Id == 0)
                return Array.Empty<AvatarOutfitModel>();

            var outfits = new List<AvatarOutfitModel>();
            string paginationToken = "";

            do
            {
                string path = $"v2/avatar/users/{user.Id}/outfits?itemsPerPage=100";

                if (!String.IsNullOrEmpty(paginationToken))
                    path += $"&paginationToken={WebUtility.UrlEncode(paginationToken)}";

                var response = await Http.AuthGetJson<AvatarOutfitPageResponse>(UrlBuilder.BuildApiUrl("avatar", path));

                foreach (var outfit in response.Data)
                {
                    outfit.ListIndex = outfits.Count;
                    outfits.Add(outfit);
                }

                paginationToken = response.PaginationToken;
            }
            while (!String.IsNullOrEmpty(paginationToken));

            await EnrichOutfitList(outfits);

            return outfits
                .OrderByDescending(x => x.IsPersonalCreation)
                .ThenByDescending(x => x.Created ?? DateTime.MinValue)
                .ThenBy(x => x.ListIndex)
                .ToArray();
        }

        private static async Task EnrichOutfitList(List<AvatarOutfitModel> outfits)
        {
            const int ThreadLimit = 6;

            using var semaphore = new SemaphoreSlim(ThreadLimit);

            var tasks = outfits.Select(async outfit =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var details = await Http.AuthGetJson<AvatarOutfitDetails>(
                        UrlBuilder.BuildApiUrl("avatar", $"v3/outfits/{outfit.Id}/details")
                    );

                    if (!String.IsNullOrEmpty(details.Name))
                        outfit.Name = details.Name;

                    if (!String.IsNullOrEmpty(details.OutfitType))
                        outfit.OutfitType = details.OutfitType;

                    outfit.IsEditable = details.IsEditable;
                    outfit.Created ??= details.Created;
                    outfit.IsPersonalCreation = IsPersonalCreation(outfit, details);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to load details for outfit {outfit.Id}");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    outfit.IsPersonalCreation = outfit.IsEditable && !LooksLikeBundle(outfit.OutfitType);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private static bool IsPersonalCreation(AvatarOutfitModel outfit, AvatarOutfitDetails details)
        {
            if (details.BundleId is not null && details.BundleId > 0)
                return false;

            if (LooksLikeBundle(details.OutfitType) || LooksLikeBundle(details.InventoryType) || LooksLikeBundle(outfit.OutfitType))
                return false;

            return details.IsEditable || outfit.IsEditable;
        }

        private static bool LooksLikeBundle(string value) =>
            !String.IsNullOrEmpty(value) &&
            value.Contains("bundle", StringComparison.OrdinalIgnoreCase);

        internal async Task<AvatarPresetApplyResult?> ApplyForPlace(long? placeId, string reason)
        {
            long? outfitId = AvatarPresetRules.GetOutfitIdForPlace(placeId);

            if (outfitId is null)
                return null;

            if (!await ApplyOutfit(outfitId.Value, reason))
                return null;

            return new AvatarPresetApplyResult
            {
                AppliedOutfitId = outfitId.Value,
                RestoreDefaultOnLeave =
                    App.Settings.Prop.RestoreAvatarPresetOnLeave &&
                    AvatarPresetRules.DefaultOutfitId > 0 &&
                    outfitId.Value != AvatarPresetRules.DefaultOutfitId
            };
        }

        internal async Task<bool> RestoreDefault(string reason)
        {
            long defaultOutfitId = AvatarPresetRules.DefaultOutfitId;

            if (!App.Settings.Prop.RestoreAvatarPresetOnLeave || defaultOutfitId <= 0 || _defaultRestored)
                return false;

            return await ApplyOutfit(defaultOutfitId, reason, true);
        }

        internal async Task<bool> ApplyOutfit(long outfitId, string reason, bool restoringDefault = false)
        {
            if (outfitId <= 0)
                return false;

            await _switchSemaphore.WaitAsync();

            try
            {
                if (_currentOutfitId == outfitId)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Outfit {outfitId} is already active for this Fishstrap session");
                    return true;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Applying outfit {outfitId} ({reason})");

                await App.Cookies.EnsureLoadedAsync();

                if (!App.Cookies.Loaded)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Cookie access is not available; avatar preset was not applied");
                    return false;
                }

                var outfit = await Http.AuthGetJson<AvatarOutfitDetails>(
                    UrlBuilder.BuildApiUrl("avatar", $"v3/outfits/{outfitId}/details")
                );

                await SetPlayerAvatarType(outfit.PlayerAvatarType);

                if (outfit.Scale is not null)
                    await PostJson(UrlBuilder.BuildApiUrl("avatar", "v1/avatar/set-scales"), outfit.Scale);

                if (outfit.BodyColor3s is not null)
                    await PostJson(UrlBuilder.BuildApiUrl("avatar", "v2/avatar/set-body-colors"), outfit.BodyColor3s);

                await PostJson(
                    UrlBuilder.BuildApiUrl("avatar", "v2/avatar/set-wearing-assets"),
                    new AvatarWearRequest
                    {
                        Assets = outfit.Assets
                            .Where(x => x.Id > 0)
                            .Select(x => new AvatarAssetModel { Id = x.Id, Meta = x.Meta })
                            .ToArray()
                    }
                );

                _currentOutfitId = outfitId;
                _defaultRestored = restoringDefault || outfitId == AvatarPresetRules.DefaultOutfitId;

                App.Logger.WriteLine(LOG_IDENT, $"Applied outfit {outfitId}");

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply outfit {outfitId}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
            finally
            {
                _switchSemaphore.Release();
            }
        }

        private static async Task SetPlayerAvatarType(string playerAvatarType)
        {
            int playerAvatarTypeId = playerAvatarType switch
            {
                "R6" => 1,
                "R15" => 3,
                _ => 0
            };

            if (playerAvatarTypeId == 0)
                return;

            await PostJson(
                UrlBuilder.BuildApiUrl("avatar", "v1/avatar/set-player-avatar-type"),
                new { playerAvatarType = playerAvatarTypeId }
            );
        }

        private static async Task PostJson(Uri uri, object body)
        {
            using var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await App.Cookies.AuthPost(uri, content);
            response.EnsureSuccessStatusCode();
        }
    }

    internal class AvatarPresetWatcher
    {
        private const string LOG_IDENT = "AvatarPresetWatcher";

        private readonly AvatarPresetSwitcher _switcher;
        private readonly long _defaultOutfitId;
        private readonly SemaphoreSlim _restoreSemaphore = new(1, 1);

        internal AvatarPresetWatcher(long currentOutfitId)
        {
            _switcher = new(currentOutfitId);
            _defaultOutfitId = AvatarPresetRules.DefaultOutfitId;
        }

        internal void HandleGameJoining(ActivityData data)
        {
            if (!AvatarPresetRules.HasRules)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _switcher.ApplyForPlace(data.PlaceId, $"game join PlaceId {data.PlaceId}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to apply avatar preset for PlaceId {data.PlaceId}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            });
        }

        internal void HandleGameLeave()
        {
            _ = RestoreDefault("game leave");
        }

        internal async Task RestoreDefault(string reason)
        {
            if (_defaultOutfitId <= 0)
                return;

            await _restoreSemaphore.WaitAsync();

            try
            {
                await _switcher.RestoreDefault(reason);
            }
            finally
            {
                _restoreSemaphore.Release();
            }
        }
    }
}
