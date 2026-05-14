using System;
using System.IO;
using System.Text.Json;

namespace BedrockLauncher.Localization.Properties
{
    /// <summary>
    /// Lightweight settings store for the Localization module.
    /// Persists to lang_settings.json next to the executable.
    /// </summary>
    public class Settings
    {
        private static readonly string _filePath =
            Path.Combine(AppContext.BaseDirectory, "lang_settings.json");

        public static Settings Default { get; } = Load();

        public string Language { get; set; } = "en-US";

        private static Settings Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var s = JsonSerializer.Deserialize<Settings>(json);
                    if (s != null) return s;
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(_filePath, JsonSerializer.Serialize(this));
            }
            catch { }
        }
    }
}
