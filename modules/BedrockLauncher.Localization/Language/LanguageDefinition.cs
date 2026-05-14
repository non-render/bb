namespace BedrockLauncher.Localization.Language
{
    public class LanguageDefinition
    {
        public string Locale { get; set; }
        public string Name   { get; set; }

        public LanguageDefinition(string locale, string name)
        {
            Locale = locale;
            Name   = name;
        }

        public override string ToString() => Name;
    }
}
