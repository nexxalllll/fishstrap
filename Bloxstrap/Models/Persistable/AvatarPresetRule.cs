using System.ComponentModel;

namespace Bloxstrap.Models.Persistable
{
    public class AvatarPresetRule : INotifyPropertyChanged
    {
        private long _placeId;
        private long _outfitId;
        private string _outfitName = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public long PlaceId
        {
            get => _placeId;
            set
            {
                _placeId = value;
                OnPropertyChanged(nameof(PlaceId));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public long OutfitId
        {
            get => _outfitId;
            set
            {
                _outfitId = value;
                OnPropertyChanged(nameof(OutfitId));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string OutfitName
        {
            get => _outfitName;
            set
            {
                _outfitName = value;
                OnPropertyChanged(nameof(OutfitName));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        [JsonIgnore]
        public string DisplayName => PlaceId > 0
            ? $"{PlaceId} -> {(!String.IsNullOrWhiteSpace(OutfitName) ? OutfitName : "saved outfit")}"
            : "New avatar rule";

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
