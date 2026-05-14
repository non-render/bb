using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BedrockLauncher.Classes;
using BedrockLauncher.Core;
using BedrockLauncher.ViewModels;
using BedrockLauncher.UpdateProcessor.Enums;
using System.Diagnostics;

namespace BedrockLauncher.Handlers
{
    public class PathHandler
    {
        #region Strings

        public string UserDataFileName       { get => "user_profile.json"; }
        public string SettingsFileName       { get => "settings.json"; }
        public string WinStoreVersionsDBFileName  { get => "winstore_versions.json"; }
        public string CommunityVersionsDBFileName { get => "community_versions.json"; }
        public string AppDataFolderName      { get => ".minecraft_bedrock"; }
        public string InstallationsFolderName { get => "installations"; }
        public string PackageDataFolderName  { get => "packageData"; }
        public string IconCacheFolderName    { get => "icon_cache"; }

        #endregion

        #region Common Paths

        /// <summary>
        /// The root directory where all launcher data lives.
        ///
        /// Resolution order:
        ///   1. locations.txt  →  launcher_data_folder
        ///   2. Portable mode  →  &lt;exe dir&gt;\data
        ///   3. Fixed directory from settings
        ///   4. Built-in default  →  %APPDATA%\.minecraft_bedrock
        /// </summary>
        public string CurrentLocation
        {
            get
            {
                // locations.txt takes precedence when the user has set a custom value
                string fromConfig = GetValidatedFolderPath(
                    LocationsConfig.KEY_LAUNCHER_DATA_FOLDER,
                    fallback: null);

                // Only use the config value when it differs from the compiled default,
                // i.e. the user has actually customised it.
                bool userCustomised = !string.IsNullOrWhiteSpace(fromConfig) &&
                                      !string.Equals(
                                          fromConfig,
                                          DefaultLocation,
                                          StringComparison.OrdinalIgnoreCase);

                if (userCustomised)
                    return EnsureDirectory(fromConfig);

                // Fall back to the existing portable-mode / fixed-directory logic
                if (Properties.LauncherSettings.Default.PortableMode)
                    return ExecutableDataDirectory;

                return GetFixedPath();
            }
        }

        public string ExecutableLocation      { get => System.Reflection.Assembly.GetEntryAssembly().Location; }
        public string ExecutableDirectory     { get => Path.GetDirectoryName(ExecutableLocation); }
        public string ExecutableDataDirectory
        {
            get
            {
                string path = Path.Combine(ExecutableDirectory, "data");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>Built-in default data location (%APPDATA%\.minecraft_bedrock).</summary>
        public string DefaultLocation
        {
            get => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataFolderName);
        }

        /// <summary>
        /// Folder where downloaded Minecraft versions are kept.
        /// Reads versions_folder from locations.txt; falls back to CurrentLocation\versions.
        /// </summary>
        public string VersionsFolder
        {
            get
            {
                string custom = GetValidatedFolderPath(LocationsConfig.KEY_VERSIONS_FOLDER, fallback: null);
                return string.IsNullOrWhiteSpace(custom)
                    ? Path.Combine(CurrentLocation, "versions") + "\\"
                    : EnsureDirectory(custom) + "\\";
            }
        }

        /// <summary>
        /// Folder where custom launcher themes are stored.
        /// Reads themes_folder from locations.txt; falls back to CurrentLocation\themes.
        /// </summary>
        public string ThemesFolder
        {
            get
            {
                string custom = GetValidatedFolderPath(LocationsConfig.KEY_THEMES_FOLDER, fallback: null);
                return string.IsNullOrWhiteSpace(custom)
                    ? Path.Combine(CurrentLocation, "themes") + "\\"
                    : EnsureDirectory(custom) + "\\";
            }
        }

        #endregion

        #region Dynamic Paths

        private string GetFixedPath()
        {
            string FixedDirectory = string.Empty;
            if (Properties.LauncherSettings.Default.FixedDirectory == string.Empty)
            {
                FixedDirectory = DefaultLocation;
            }
            else FixedDirectory = Properties.LauncherSettings.Default.FixedDirectory;

            if (!Directory.Exists(FixedDirectory))
            {
                try
                {
                    Directory.CreateDirectory(FixedDirectory);
                }
                catch (DirectoryNotFoundException)
                {
                    Trace.WriteLine("Unable to Create Fixed Directory. Reverting to Fallback");
                    Properties.LauncherSettings.Default.FixedDirectory = string.Empty;
                    FixedDirectory = DefaultLocation;
                }
            }
            return FixedDirectory;
        }

        public string GetSettingsFilePath()
        {
            return Path.Combine(ExecutableDataDirectory, SettingsFileName);
        }
        public string GetCommunityVersionsDBFile()
        {
            return Path.Combine(CurrentLocation, CommunityVersionsDBFileName);
        }
        public string GetWinStoreVersionsDBFile()
        {
            return Path.Combine(CurrentLocation, WinStoreVersionsDBFileName);
        }
        public string GetProfilesFilePath()
        {
            return Path.Combine(CurrentLocation, UserDataFileName);
        }

        /// <summary>
        /// Returns the icon-cache folder path.
        /// Reads icon_cache_folder from locations.txt; falls back to CurrentLocation\icon_cache.
        /// </summary>
        public string GetCacheFolderPath()
        {
            string custom = GetValidatedFolderPath(LocationsConfig.KEY_ICON_CACHE_FOLDER, fallback: null);
            string cache_dir = string.IsNullOrWhiteSpace(custom)
                ? Path.Combine(CurrentLocation, IconCacheFolderName)
                : custom;

            if (!Directory.Exists(cache_dir)) Directory.CreateDirectory(cache_dir);
            return cache_dir;
        }

        public string GetProfilePath(string profileUUID)
        {
            if (string.IsNullOrEmpty(profileUUID)) return string.Empty;
            else if (!MainDataModel.Default.Config.profiles.ContainsKey(profileUUID)) return string.Empty;
            var profile = MainDataModel.Default.Config.profiles[profileUUID];

            string installationsRoot = GetInstallationsRoot();
            return Path.Combine(installationsRoot, profile.ProfilePath);
        }

        public string GetInstallationPath(string profileUUID, string installationDirectory)
        {
            string ProfilePath = GetProfilePath(profileUUID);
            if (string.IsNullOrEmpty(ProfilePath)) return string.Empty;
            string InstallationsPath = Path.Combine(ProfilePath, installationDirectory);

            string installationsRoot = GetInstallationsRoot();
            return Path.Combine(installationsRoot, InstallationsPath);
        }

        public string GetInstallationPackageDataPath(string profileUUID, string installationDirectory)
        {
            string ProfilePath = GetProfilePath(profileUUID);
            if (string.IsNullOrEmpty(ProfilePath)) return string.Empty;
            string InstallationsPath = Path.Combine(ProfilePath, installationDirectory);

            string installationsRoot = GetInstallationsRoot();
            return Path.Combine(installationsRoot, InstallationsPath, PackageDataFolderName);
        }

        /// <summary>
        /// Returns the root folder that holds all installation profiles.
        /// Reads installations_folder from locations.txt; falls back to CurrentLocation\installations.
        /// </summary>
        private string GetInstallationsRoot()
        {
            string custom = GetValidatedFolderPath(LocationsConfig.KEY_INSTALLATIONS_FOLDER, fallback: null);
            return string.IsNullOrWhiteSpace(custom)
                ? Path.Combine(CurrentLocation, InstallationsFolderName)
                : custom;
        }

        #endregion

        #region Image Cache

        public string GenerateIconCacheFileName(string extension)
        {
            string cache_dir = GetCacheFolderPath();
            string destFileName = string.Empty;

            while (destFileName == string.Empty || File.Exists(destFileName))
            {
                string cache_filename = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + extension;
                destFileName = Path.Combine(cache_dir, cache_filename);
            }

            return destFileName;
        }

        public bool RemoveImageFromIconCache(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex);
                return false;
            }
        }

        public string AddImageToIconCache(string sourceFilePath)
        {
            string destFileName = GenerateIconCacheFileName(Path.GetExtension(sourceFilePath));

            try
            {
                File.Copy(sourceFilePath, destFileName);
                return destFileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex);
                return string.Empty;
            }
        }

        #endregion

        #region LocationsConfig helpers

        /// <summary>
        /// Reads a folder path from LocationsConfig, validates it, and returns it.
        /// If the value is empty or the drive is unreachable a warning is shown
        /// (once per key per session) and <paramref name="fallback"/> is returned.
        /// </summary>
        private static readonly HashSet<string> _warnedKeys = new(StringComparer.OrdinalIgnoreCase);

        private static string GetValidatedFolderPath(string key, string? fallback)
        {
            if (!LocationsConfig.ValidatePath(key, isFile: false, out string resolved))
            {
                if (_warnedKeys.Add(key))
                    LocationsConfig.ShowPathError(key, resolved);

                return fallback ?? string.Empty;
            }

            return resolved;
        }

        /// <summary>Creates the directory if it does not exist and returns the path.</summary>
        private static string EnsureDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[PathHandler] Could not create directory '{path}': {ex.Message}");
            }
            return path;
        }

        #endregion
    }
}
