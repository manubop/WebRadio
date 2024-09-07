using System;

using ReactiveUI;

namespace WebRadio.DataModel
{
    public interface IStationModel
    {
        public string Name { get; }

        public string Url { get; }

        public string Description { get; }

        public string Api { get; }
    }

    public class StationModelBase : IStationModel
    {
        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Api { get; set; } = string.Empty;
    }

    public class StationModel : ReactiveObject, IStationModel
    {
        private string _append = string.Empty;

        public StationModel() { }

        public StationModel(IStationModel model)
        {
            Name = model.Name;
            Url = model.Url;
            Description = model.Description;
            Api = model.Api;
        }

        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Api { get; set; } = string.Empty;

        public bool IsValid() => IsValid(Name, Url);

        public static bool IsValid(string name, string url) => !string.IsNullOrEmpty(name) && Uri.IsWellFormedUriString(url, UriKind.Absolute);

        public static readonly StationModel Empty = new();

        public string Append
        {
            get => _append;
            set
            {
                _append = value;
                this.RaisePropertyChanged(nameof(Concat));
            }
        }

        public string Concat { get => Name + ' ' + Append; }
    }
}
