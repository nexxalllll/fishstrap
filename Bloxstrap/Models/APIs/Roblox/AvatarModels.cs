namespace Bloxstrap.Models.APIs.Roblox
{
    public class AvatarOutfitPageResponse
    {
        [JsonPropertyName("data")]
        public IEnumerable<AvatarOutfitModel> Data { get; set; } = Enumerable.Empty<AvatarOutfitModel>();

        [JsonPropertyName("paginationToken")]
        public string PaginationToken { get; set; } = "";
    }

    public class AvatarOutfitModel
    {
        [JsonIgnore]
        public int ListIndex { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("isEditable")]
        public bool IsEditable { get; set; }

        [JsonPropertyName("outfitType")]
        public string OutfitType { get; set; } = "";

        [JsonPropertyName("created")]
        public DateTime? Created { get; set; }

        [JsonIgnore]
        public bool IsPersonalCreation { get; set; }

        [JsonIgnore]
        public string DisplayName => Name;
    }

    public class AvatarOutfitDetails
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("assets")]
        public IEnumerable<AvatarAssetModel> Assets { get; set; } = Enumerable.Empty<AvatarAssetModel>();

        [JsonPropertyName("bodyColor3s")]
        public AvatarBodyColors3? BodyColor3s { get; set; }

        [JsonPropertyName("scale")]
        public AvatarScale? Scale { get; set; }

        [JsonPropertyName("playerAvatarType")]
        public string PlayerAvatarType { get; set; } = "";

        [JsonPropertyName("outfitType")]
        public string OutfitType { get; set; } = "";

        [JsonPropertyName("isEditable")]
        public bool IsEditable { get; set; }

        [JsonPropertyName("bundleId")]
        public long? BundleId { get; set; }

        [JsonPropertyName("inventoryType")]
        public string InventoryType { get; set; } = "";

        [JsonPropertyName("created")]
        public DateTime? Created { get; set; }
    }

    public class AvatarAssetModel
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("meta")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Meta { get; set; }
    }

    public class AvatarWearRequest
    {
        [JsonPropertyName("assets")]
        public IEnumerable<AvatarAssetModel> Assets { get; set; } = Enumerable.Empty<AvatarAssetModel>();
    }

    public class AvatarBodyColors3
    {
        [JsonPropertyName("headColor3")]
        public string HeadColor3 { get; set; } = "";

        [JsonPropertyName("torsoColor3")]
        public string TorsoColor3 { get; set; } = "";

        [JsonPropertyName("rightArmColor3")]
        public string RightArmColor3 { get; set; } = "";

        [JsonPropertyName("leftArmColor3")]
        public string LeftArmColor3 { get; set; } = "";

        [JsonPropertyName("rightLegColor3")]
        public string RightLegColor3 { get; set; } = "";

        [JsonPropertyName("leftLegColor3")]
        public string LeftLegColor3 { get; set; } = "";
    }

    public class AvatarScale
    {
        [JsonPropertyName("height")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Height { get; set; }

        [JsonPropertyName("width")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Width { get; set; }

        [JsonPropertyName("head")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Head { get; set; }

        [JsonPropertyName("depth")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Depth { get; set; }

        [JsonPropertyName("proportion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Proportion { get; set; }

        [JsonPropertyName("bodyType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? BodyType { get; set; }
    }
}
