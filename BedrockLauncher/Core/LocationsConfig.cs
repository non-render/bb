using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace BedrockLauncher.Core
{
    /// <summary>
    /// Manages all configurable file and folder paths via a human-readable
    /// locations.txt file placed next to the launcher executable.
    ///
    /// On first launch the file is generated automatically with default values
    /// and inline comments. Missing entries are recreated on every launch.
    /// Invalid paths produce a clear warning and fall back to built-in defaults
    /// so the launcher never crashes due to a bad configuration value.
    /// </summary>
    public static class LocationsConfig
    {
        // ------------------------------------------------------------------ //
        //  Public key constants
        // ------------------------------------------------------------------ //

        /// <summary>Root folder where all launcher data is stored.</summary>
        public const string KEY_LAUNCHER_DATA_FOLDER = "launcher_data_folder";

        /// <summary>Folder where downloaded Minecraft versions are kept.</summary>
        public const string KEY_VERSIONS_FOLDER = "versions_folder";

        /// <summary>Folder that holds all installation profiles and their game data.</summary>
        public const string KEY_INSTALLATIONS_FOLDER = "installations_folder";

        /// <summary>Folder used as a cache for installation icon images.</summary>
        public const string KEY_ICON_CACHE_FOLDER = "icon_cache_folder";

        /// <summary>Folder where custom launcher themes are stored.</summary>
        public const string KEY_THEMES_FOLDER = "themes_folder";

        // ------------------------------------------------------------------ //
        //  Schema – key → (defaultValue, multiline comment)
        //  Order is preserved for the written file.
        // ------------------------------------------------------------------ //

        private static readonly List<SchemaEntry> Schema = new()
        {
            new(
                KEY_LAUNCHER_DATA_FOLDER,
                @"%APPDATA%\.minecraft_bedrock",
                new[]
                {
                    "Main launcher data folder.",
                    "All profiles, versions, and settings are stored here.",
                    "Change this to move your launcher data to a custom location.",
                    "Environment variables (e.g. %APPDATA%) are supported."
                }
            ),
            new(
                KEY_VERSIONS_FOLDER,
                "",
                new[]
                {
                    "Folder where downloaded Minecraft versions are stored.",
                    "Leave empty to use <launcher_data_folder>\\versions (recommended)."
                }
            ),
            new(
                KEY_INSTALLATIONS_FOLDER,
                "",
                new[]
                {
                    "Folder that holds installation profiles and their game data.",
                    "Leave empty to use <launcher_data_folder>\\installations (recommended)."
                }
            ),
            new(
                KEY_ICON_CACHE_FOLDER,
                "",
                new[]
                {
                    "Folder used to cache installation icon images.",
                    "Leave empty to use <launcher_data_folder>\\icon_cache (recommended)."
                }
            ),
            new(
                KEY_THEMES_FOLDER,
                "",
                new[]
                {
                    "Folder where custom launcher themes are stored.",
                    "Leave empty to use <launcher_data_folder>\\themes (recommended)."
                }
            ),
        };

        // ------------------------------------------------------------------ //
        //  State
        // ------------------------------------------------------------------ //

        private static readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        /// <summary>Full path to the locations.txt file (sits next to the EXE).</summary>
        public static string LocationsFilePath { get; } =
            Path.Combine(AppContext.BaseDirectory, "locations.txt");

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Load (and if necessary create) locations.txt.
        /// Call this once at application startup before accessing any path.
        /// </summary>
        public static void Load()
        {
            EnsureFileExists();
            ReadFile();
            EnsureAllEntriesPresent();
            _loaded = true;
        }

        /// <summary>
        /// Returns the resolved value for <paramref name="key"/>.
        /// Environment variables are expanded. If the entry is empty the
        /// schema default is returned; if that is also empty, string.Empty
        /// is returned so callers can detect "use built-in logic".
        /// </summary>
        public static string Get(string key)
        {
            if (!_loaded)
                Load();

            if (_values.TryGetValue(key, out string? raw) && !string.IsNullOrWhiteSpace(raw))
                return Environment.ExpandEnvironmentVariables(raw.Trim());

            string schemaDefault = GetSchemaDefault(key);
            if (!string.IsNullOrWhiteSpace(schemaDefault))
                return Environment.ExpandEnvironmentVariables(schemaDefault);

            return string.Empty;
        }

        /// <summary>
        /// Checks whether the path stored under <paramref name="key"/> exists
        /// on disk. Directories that do not yet exist are treated as valid so
        /// the launcher can create them later. Files must exist.
        /// Returns <c>true</c> if the value is empty (meaning: use the
        /// built-in default path – no validation needed here).
        /// </summary>
        public static bool ValidatePath(string key, bool isFile, out string resolvedPath)
        {
            resolvedPath = Get(key);

            if (string.IsNullOrWhiteSpace(resolvedPath))
                return true;

            if (isFile)
                return File.Exists(resolvedPath);

            // For directories: only fail if the path is rooted AND the root
            // drive does not exist (e.g. a removed USB drive).
            if (!Path.IsPathRooted(resolvedPath))
                return true;

            string? root = Path.GetPathRoot(resolvedPath);
            return root == null || Directory.Exists(root);
        }

        /// <summary>
        /// Shows a user-facing warning about a broken path entry and logs it.
        /// The launcher should continue with the built-in default after calling this.
        /// </summary>
        public static void ShowPathError(string key, string resolvedPath)
        {
            string message =
                $"Configuration warning in locations.txt:\n\n" +
                $"The path for  \"{key}\"  is invalid or inaccessible:\n" +
                $"    {resolvedPath}\n\n" +
                $"Please open  locations.txt  next to the launcher and correct the path.\n" +
                $"The launcher will continue using the built-in default for this entry.";

            System.Diagnostics.Trace.WriteLine(
                $"[LocationsConfig] Bad path for key '{key}': {resolvedPath}");

            MessageBox.Show(
                message,
                "Invalid Path in locations.txt",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // ------------------------------------------------------------------ //
        //  Private helpers
        // ------------------------------------------------------------------ //

        private static void EnsureFileExists()
        {
            if (!File.Exists(LocationsFilePath))
            {
                var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in Schema)
                    defaults[entry.Key] = entry.DefaultValue;

                WriteFile(defaults);
            }
        }

        private static void ReadFile()
        {
            _values.Clear();
            string[] lines;

            try
            {
                lines = File.ReadAllLines(LocationsFilePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[LocationsConfig] Failed to read locations.txt: {ex.Message}");
                return;
            }

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                // Skip blank lines and comment lines
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                int eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 1)
                    continue;

                string key   = trimmed[..eqIdx].Trim();
                string value = trimmed[(eqIdx + 1)..].Trim();

                if (!string.IsNullOrEmpty(key))
                    _values[key] = value;
            }
        }

        private static void EnsureAllEntriesPresent()
        {
            bool changed = false;
            foreach (var entry in Schema)
            {
                if (!_values.ContainsKey(entry.Key))
                {
                    _values[entry.Key] = entry.DefaultValue;
                    changed = true;
                    System.Diagnostics.Trace.WriteLine(
                        $"[LocationsConfig] Restored missing entry '{entry.Key}' to default.");
                }
            }

            if (changed)
                WriteFile(_values);
        }

        private static void WriteFile(Dictionary<string, string> values)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# ================================================================");
            sb.AppendLine("# BedrockLauncher  —  locations.txt");
            sb.AppendLine("# ================================================================");
            sb.AppendLine("# Edit this file to configure where the launcher stores its data.");
            sb.AppendLine("# Lines starting with # are comments and are ignored.");
            sb.AppendLine("# Environment variables such as %APPDATA% are supported.");
            sb.AppendLine("# Leave a value empty to use the built-in default for that entry.");
            sb.AppendLine("# Missing entries are recreated with their defaults on next launch.");
            sb.AppendLine("# Encoding: UTF-8");
            sb.AppendLine("# ================================================================");

            foreach (var entry in Schema)
            {
                sb.AppendLine();
                foreach (string commentLine in entry.Comments)
                    sb.Append("# ").AppendLine(commentLine);

                values.TryGetValue(entry.Key, out string? val);
                sb.Append(entry.Key).Append('=').AppendLine(val ?? string.Empty);
            }

            try
            {
                File.WriteAllText(
                    LocationsFilePath,
                    sb.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[LocationsConfig] Failed to write locations.txt: {ex.Message}");
            }
        }

        private static string GetSchemaDefault(string key)
        {
            foreach (var entry in Schema)
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                    return entry.DefaultValue;
            return string.Empty;
        }

        // ------------------------------------------------------------------ //
        //  Helper record
        // ------------------------------------------------------------------ //

        private sealed record SchemaEntry(string Key, string DefaultValue, string[] Comments);
    }
}
