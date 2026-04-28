namespace EasyBilling.Domain.Models
{
    public static class UnitOfMeasure
    {
        public const string Default = "buc";
        public const int MaxLength = 20;

        public const string Bucata = "buc";
        public const string Ora = "ore";
        public const string Zi = "zile";
        public const string Luna = "luni";
        public const string Element = "elem";
        public const string Set = "set";
        public const string Kilogram = "kg";
        public const string Metru = "m";
        public const string MetruPatrat = "mp";
        public const string Litru = "l";

        public static readonly IReadOnlyList<UnitOfMeasureOption> Supported = new List<UnitOfMeasureOption>
        {
            new(Bucata, "Bucată"),
            new(Ora, "Oră"),
            new(Zi, "Zi"),
            new(Luna, "Lună"),
            new(Element, "Element"),
            new(Set, "Set"),
            new(Kilogram, "Kilogram"),
            new(Metru, "Metru"),
            new(MetruPatrat, "Metru pătrat"),
            new(Litru, "Litru"),
        };

        public static string NormalizeOrDefault(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return Default;

            var trimmed = unit.Trim();
            return trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
        }
    }

    public record UnitOfMeasureOption(string Code, string Label);
}
