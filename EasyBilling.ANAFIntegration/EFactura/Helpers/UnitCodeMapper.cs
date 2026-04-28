namespace EasyBilling.ANAFIntegration.EFactura.Helpers
{
    public static class UnitCodeMapper
    {
        public const string DefaultCode = "H87";

        public static string Map(string? unit)
        {
            return unit?.Trim().ToLowerInvariant() switch
            {
                "buc" or "bucata" or "bucati" => "H87",
                "ora" or "ore" => "HUR",
                "zi" or "zile" => "DAY",
                "luna" or "luni" => "MON",
                "elem" or "element" or "elemente" => "C62",
                "set" or "seturi" => "SET",
                "kg" or "kilogram" => "KGM",
                "m" or "metru" or "metri" => "MTR",
                "mp" or "m2" => "MTK",
                "l" or "litru" or "litri" => "LTR",
                _ => DefaultCode
            };
        }
    }
}
