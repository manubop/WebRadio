using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using WebRadio.DataModel;

namespace WebRadio.Services
{
    public interface IStationService
    {
        public IEnumerable<IStationModel> Load();
        public void Store(IEnumerable<IStationModel> stations);
    }

    public class StationsService(string stationsFilename) : IStationService
    {
        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

        public IEnumerable<IStationModel> Load()
        {
            var text = File.ReadAllText(stationsFilename);

            return JsonSerializer.Deserialize<StationModelBase[]>(text) ?? [];
        }

        public void Store(IEnumerable<IStationModel> stations)
        {
            var text = JsonSerializer.Serialize(stations, _options);

            File.WriteAllText(stationsFilename, text);
        }
    }
}
