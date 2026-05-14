using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace BedrockLauncher.Localization.Language
{
    public static class LanguageManager
    {
        private static readonly List<LanguageDefinition> _available = new()
        {
            new LanguageDefinition("en-US", "English (United States)"),
        };

        public static void Init()
        {
            string saved = Properties.Settings.Default.Language;
            if (!string.IsNullOrWhiteSpace(saved))
                SetLanguage(saved);
        }

        public static object GetResource(string key)
        {
            try
            {
                if (Application.Current != null)
                {
                    object res = Application.Current.TryFindResource(key);
                    if (res != null) return res;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LanguageManager] GetResource('{key}') failed: {ex.Message}");
            }
            return key;
        }

        public static void SetLanguage(string locale)
        {
            try
            {
                Properties.Settings.Default.Language = locale;

                var dict = new LanguageDictionary
                {
                    Source = new Uri(
                        $"/BedrockLauncher.Localization;component/Resources/lang/lang.{locale}.xaml",
                        UriKind.Relative)
                };

                var merged = Application.Current?.Resources?.MergedDictionaries;
                if (merged == null) return;

                for (int i = 0; i < merged.Count; i++)
                {
                    if (merged[i] is LanguageDictionary)
                    {
                        merged[i] = dict;
                        return;
                    }
                }
                merged.Add(dict);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LanguageManager] SetLanguage('{locale}') failed: {ex.Message}");
            }
        }

        public static List<LanguageDefinition> GetResourceDictonaries()
        {
            return _available;
        }
    }
}
