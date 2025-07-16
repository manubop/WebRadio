using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WebRadio
{
    public class AppState
    {
        public int SelectedIndex { get; set; }

        public bool IsItemPlaying { get; set; }

        public int LastPlayedIndex { get; set; }

        public float Volume { get; set; } = 1f;

        public void Save(string filename)
        {
            var text = JsonSerializer.Serialize(this);

            File.WriteAllText(filename, text);
        }

        public static AppState? Load(string filename)
        {
            try
            {
                var text = File.ReadAllText(filename);

                return JsonSerializer.Deserialize<AppState>(text);
            }
            catch (Exception ex)
            {
                App.logger.LogWarning("Could not read state: {Exception}", ex.Message);

                return null;
            }
        }
    }
}
