using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasyBilling.ANAFIntegration.EFactura.Helpers;

public static class LocationHelper
{
    private static ILogger _logger = NullLoggerFactory.Instance.CreateLogger("LocationHelper");

    public static void SetLogger(ILogger logger)
    {
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger("LocationHelper");
        _logger.LogDebug("LocationHelper logger initialized");
    }

    public static List<CountyDto> GetCounties()
    {
        _logger.LogDebug("Retrieving all counties from Romania - Total count: {CountyCount}", Counties.Count);
        var result = Counties.OrderBy(c => c.Name).ToList();
        _logger.LogDebug("Counties retrieved and sorted - Returning {CountyCount} counties", result.Count);
        return result;
    }

    public static List<CityDto> GetCities(string? countyCode = null)
    {
        _logger.LogDebug("Retrieving cities from Romania{Filter}",
            string.IsNullOrEmpty(countyCode) ? "" : $" for county code: {countyCode}");

        try
        {
            var cities = Cities.AsEnumerable();

            if (!string.IsNullOrEmpty(countyCode))
            {
                _logger.LogDebug("Applying county code filter: {CountyCode}", countyCode);
                cities = cities.Where(c => c.CountyCode.Equals(countyCode, StringComparison.OrdinalIgnoreCase));
            }

            var result = cities.OrderBy(c => c.CountyCode).ThenBy(c => c.Name).ToList();
            _logger.LogDebug("Cities retrieved and sorted - Returning {CityCount} cities{Filter}",
                result.Count, string.IsNullOrEmpty(countyCode) ? "" : $" for county {countyCode}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cities for county code: {CountyCode}", countyCode);
            throw;
        }
    }

    public static string NormalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            _logger.LogDebug("NormalizeCity called with empty city name");
            return String.Empty;
        }

        _logger.LogDebug("Normalizing city name: {OriginalCity}", city);

        try
        {
            var clean = city.Trim();

            if (clean.Contains("Sector", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(clean, @"Sector\s*(\d)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var normalized = $"SECTOR{match.Groups[1].Value}";
                    _logger.LogDebug("City normalized (Sector format): {OriginalCity} => {NormalizedCity}", city, normalized);
                    return normalized;
                }
            }

            var prefixes = new[] { "Mun.", "Municipiul", "Oraș", "Oras", "Com.", "Comuna", "Sat" };

            foreach (var prefix in prefixes)
            {
                if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[prefix.Length..].Trim();
                    _logger.LogDebug("Removed prefix {Prefix}: {City}", prefix, clean);
                }
            }

            foreach (var prefix in prefixes)
            {
                var pattern = $@"\s*{Regex.Escape(prefix)}\.?\s*\S*";
                var beforeClean = clean;
                clean = Regex.Replace(clean, pattern, "", RegexOptions.IgnoreCase).Trim();
                if (beforeClean != clean)
                {
                    _logger.LogDebug("Removed pattern {Pattern}: {Before} => {After}", prefix, beforeClean, clean);
                }
            }

            _logger.LogDebug("City normalized: {OriginalCity} => {NormalizedCity}", city, clean);
            return clean;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing city name: {City}", city);
            throw;
        }
    }

    public static string GetCountyCode(string? countyName)
    {
        if (string.IsNullOrWhiteSpace(countyName))
        {
            _logger.LogDebug("GetCountyCode called with empty county name, returning default: RO-B");
            return "RO-B";
        }

        _logger.LogDebug("Converting county name to ISO code: {CountyName}", countyName);

        try
        {
            var clean = countyName.Trim().ToUpper();

            if (clean.StartsWith("RO-"))
            {
                _logger.LogDebug("County name already in ISO format: {CountyCode}", clean);
                return clean;
            }

            clean = clean
                .Replace("MUNICIPIUL", "")
                .Replace("JUDEȚULUI", "")
                .Replace("JUDETUL", "")
                .Trim();

            if (CountyNameToCode.TryGetValue(clean, out var code))
            {
                _logger.LogDebug("County code found: {CountyName} => {CountyCode}", countyName, code);
                return code;
            }

            var normalized = RemoveDiacritics(clean);
            if (CountyNameToCode.TryGetValue(normalized, out code))
            {
                _logger.LogDebug("County code found (after removing diacritics): {CountyName} => {CountyCode}", countyName, code);
                return code;
            }

            _logger.LogWarning("County code not found for: {CountyName}, returning default: RO-B", countyName);
            return "RO-B";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting county name to ISO code: {CountyName}", countyName);
            throw;
        }
    }

    public static string? GetCountyName(string? countyCode)
    {
        if (string.IsNullOrWhiteSpace(countyCode))
        {
            _logger.LogDebug("GetCountyName called with empty county code");
            return null;
        }

        _logger.LogDebug("Converting county ISO code to name: {CountyCode}", countyCode);

        try
        {
            var county = Counties.FirstOrDefault(c =>
                c.Code.Equals(countyCode, StringComparison.OrdinalIgnoreCase));

            if (county == null)
            {
                _logger.LogWarning("County code not found: {CountyCode}", countyCode);
                return null;
            }

            _logger.LogDebug("County name found: {CountyCode} => {CountyName}", countyCode, county.Name);
            return county.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting county ISO code to name: {CountyCode}", countyCode);
            throw;
        }
    }

    private static string RemoveDiacritics(string text)
    {
        return text
            .Replace("Ă", "A").Replace("Â", "A").Replace("Î", "I")
            .Replace("Ș", "S").Replace("Ț", "T")
            .Replace("ă", "a").Replace("â", "a").Replace("î", "i")
            .Replace("ș", "s").Replace("ț", "t");
    }

    #region Static Data

    private static readonly Dictionary<string, string> CountyNameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ALBA", "RO-AB" },
        { "ARAD", "RO-AR" },
        { "ARGEȘ", "RO-AG" },
        { "ARGES", "RO-AG" },
        { "BACĂU", "RO-BC" },
        { "BACAU", "RO-BC" },
        { "BIHOR", "RO-BH" },
        { "BISTRIȚA-NĂSĂUD", "RO-BN" },
        { "BISTRITA-NASAUD", "RO-BN" },
        { "BISTRITA NASAUD", "RO-BN" },
        { "BOTOȘANI", "RO-BT" },
        { "BOTOSANI", "RO-BT" },
        { "BRĂILA", "RO-BR" },
        { "BRAILA", "RO-BR" },
        { "BRAȘOV", "RO-BV" },
        { "BRASOV", "RO-BV" },
        { "BUCUREȘTI", "RO-B" },
        { "BUCURESTI", "RO-B" },
        { "BUCHAREST", "RO-B" },
        { "BUZĂU", "RO-BZ" },
        { "BUZAU", "RO-BZ" },
        { "CĂLĂRAȘI", "RO-CL" },
        { "CALARASI", "RO-CL" },
        { "CARAȘ-SEVERIN", "RO-CS" },
        { "CARAS-SEVERIN", "RO-CS" },
        { "CARAS SEVERIN", "RO-CS" },
        { "CLUJ", "RO-CJ" },
        { "CONSTANȚA", "RO-CT" },
        { "CONSTANTA", "RO-CT" },
        { "COVASNA", "RO-CV" },
        { "DÂMBOVIȚA", "RO-DB" },
        { "DAMBOVITA", "RO-DB" },
        { "DOLJ", "RO-DJ" },
        { "GALAȚI", "RO-GL" },
        { "GALATI", "RO-GL" },
        { "GIURGIU", "RO-GR" },
        { "GORJ", "RO-GJ" },
        { "HARGHITA", "RO-HR" },
        { "HUNEDOARA", "RO-HD" },
        { "IALOMIȚA", "RO-IL" },
        { "IALOMITA", "RO-IL" },
        { "IAȘI", "RO-IS" },
        { "IASI", "RO-IS" },
        { "ILFOV", "RO-IF" },
        { "MARAMUREȘ", "RO-MM" },
        { "MARAMURES", "RO-MM" },
        { "MEHEDINȚI", "RO-MH" },
        { "MEHEDINTI", "RO-MH" },
        { "MUREȘ", "RO-MS" },
        { "MURES", "RO-MS" },
        { "NEAMȚ", "RO-NT" },
        { "NEAMT", "RO-NT" },
        { "OLT", "RO-OT" },
        { "PRAHOVA", "RO-PH" },
        { "SĂLAJ", "RO-SJ" },
        { "SALAJ", "RO-SJ" },
        { "SATU MARE", "RO-SM" },
        { "SATU-MARE", "RO-SM" },
        { "SIBIU", "RO-SB" },
        { "SUCEAVA", "RO-SV" },
        { "TELEORMAN", "RO-TR" },
        { "TIMIȘ", "RO-TM" },
        { "TIMIS", "RO-TM" },
        { "TULCEA", "RO-TL" },
        { "VÂLCEA", "RO-VL" },
        { "VALCEA", "RO-VL" },
        { "VASLUI", "RO-VS" },
        { "VRANCEA", "RO-VN" }
    };

    private static readonly List<CountyDto> Counties = new()
    {
        new("RO-AB", "Alba"),
        new("RO-AR", "Arad"),
        new("RO-AG", "Argeș"),
        new("RO-BC", "Bacău"),
        new("RO-BH", "Bihor"),
        new("RO-BN", "Bistrița-Năsăud"),
        new("RO-BT", "Botoșani"),
        new("RO-BR", "Brăila"),
        new("RO-BV", "Brașov"),
        new("RO-B", "București"),
        new("RO-BZ", "Buzău"),
        new("RO-CL", "Călărași"),
        new("RO-CS", "Caraș-Severin"),
        new("RO-CJ", "Cluj"),
        new("RO-CT", "Constanța"),
        new("RO-CV", "Covasna"),
        new("RO-DB", "Dâmbovița"),
        new("RO-DJ", "Dolj"),
        new("RO-GL", "Galați"),
        new("RO-GR", "Giurgiu"),
        new("RO-GJ", "Gorj"),
        new("RO-HR", "Harghita"),
        new("RO-HD", "Hunedoara"),
        new("RO-IL", "Ialomița"),
        new("RO-IS", "Iași"),
        new("RO-IF", "Ilfov"),
        new("RO-MM", "Maramureș"),
        new("RO-MH", "Mehedinți"),
        new("RO-MS", "Mureș"),
        new("RO-NT", "Neamț"),
        new("RO-OT", "Olt"),
        new("RO-PH", "Prahova"),
        new("RO-SJ", "Sălaj"),
        new("RO-SM", "Satu Mare"),
        new("RO-SB", "Sibiu"),
        new("RO-SV", "Suceava"),
        new("RO-TR", "Teleorman"),
        new("RO-TM", "Timiș"),
        new("RO-TL", "Tulcea"),
        new("RO-VL", "Vâlcea"),
        new("RO-VS", "Vaslui"),
        new("RO-VN", "Vrancea")
    };

    private static readonly List<CityDto> Cities = new()
    {
        // Alba
        new("Alba Iulia", "RO-AB"),
        new("Aiud", "RO-AB"),
        new("Blaj", "RO-AB"),
        new("Sebeș", "RO-AB"),
        new("Cugir", "RO-AB"),
        new("Ocna Mureș", "RO-AB"),
        new("Zlatna", "RO-AB"),
        new("Câmpeni", "RO-AB"),
        new("Teiuș", "RO-AB"),
        new("Abrud", "RO-AB"),

        // Arad
        new("Arad", "RO-AR"),
        new("Ineu", "RO-AR"),
        new("Lipova", "RO-AR"),
        new("Pecica", "RO-AR"),
        new("Chișineu-Criș", "RO-AR"),
        new("Curtici", "RO-AR"),
        new("Sântana", "RO-AR"),
        new("Nădlac", "RO-AR"),
        new("Pâncota", "RO-AR"),
        new("Sebiș", "RO-AR"),

        // Argeș
        new("Pitești", "RO-AG"),
        new("Câmpulung", "RO-AG"),
        new("Curtea de Argeș", "RO-AG"),
        new("Mioveni", "RO-AG"),
        new("Costești", "RO-AG"),
        new("Topoloveni", "RO-AG"),
        new("Ștefănești", "RO-AG"),

        // Bacău
        new("Bacău", "RO-BC"),
        new("Onești", "RO-BC"),
        new("Moinești", "RO-BC"),
        new("Comănești", "RO-BC"),
        new("Buhuși", "RO-BC"),
        new("Dărmănești", "RO-BC"),
        new("Târgu Ocna", "RO-BC"),
        new("Slănic-Moldova", "RO-BC"),

        // Bihor
        new("Oradea", "RO-BH"),
        new("Salonta", "RO-BH"),
        new("Marghita", "RO-BH"),
        new("Beiuș", "RO-BH"),
        new("Aleșd", "RO-BH"),
        new("Ștei", "RO-BH"),
        new("Valea lui Mihai", "RO-BH"),
        new("Nucet", "RO-BH"),
        new("Săcueni", "RO-BH"),
        new("Vașcău", "RO-BH"),

        // Bistrița-Năsăud
        new("Bistrița", "RO-BN"),
        new("Năsăud", "RO-BN"),
        new("Beclean", "RO-BN"),
        new("Sângeorz-Băi", "RO-BN"),

        // Botoșani
        new("Botoșani", "RO-BT"),
        new("Dorohoi", "RO-BT"),
        new("Darabani", "RO-BT"),
        new("Săveni", "RO-BT"),
        new("Flămânzi", "RO-BT"),
        new("Ștefănești", "RO-BT"),
        new("Bucecea", "RO-BT"),

        // Brăila
        new("Brăila", "RO-BR"),
        new("Ianca", "RO-BR"),
        new("Însurăței", "RO-BR"),
        new("Făurei", "RO-BR"),

        // Brașov
        new("Brașov", "RO-BV"),
        new("Făgăraș", "RO-BV"),
        new("Săcele", "RO-BV"),
        new("Codlea", "RO-BV"),
        new("Zărnești", "RO-BV"),
        new("Râșnov", "RO-BV"),
        new("Victoria", "RO-BV"),
        new("Rupea", "RO-BV"),
        new("Ghimbav", "RO-BV"),
        new("Predeal", "RO-BV"),

        // București
        new("București", "RO-B"),
        new("Sector 1", "RO-B"),
        new("Sector 2", "RO-B"),
        new("Sector 3", "RO-B"),
        new("Sector 4", "RO-B"),
        new("Sector 5", "RO-B"),
        new("Sector 6", "RO-B"),

        // Buzău
        new("Buzău", "RO-BZ"),
        new("Râmnicu Sărat", "RO-BZ"),
        new("Nehoiu", "RO-BZ"),
        new("Pogoanele", "RO-BZ"),
        new("Pătârlagele", "RO-BZ"),

        // Călărași
        new("Călărași", "RO-CL"),
        new("Oltenița", "RO-CL"),
        new("Budești", "RO-CL"),
        new("Fundulea", "RO-CL"),
        new("Lehliu Gară", "RO-CL"),

        // Caraș-Severin
        new("Reșița", "RO-CS"),
        new("Caransebeș", "RO-CS"),
        new("Oțelu Roșu", "RO-CS"),
        new("Moldova Nouă", "RO-CS"),
        new("Bocșa", "RO-CS"),
        new("Oravița", "RO-CS"),
        new("Anina", "RO-CS"),
        new("Băile Herculane", "RO-CS"),

        // Cluj
        new("Cluj-Napoca", "RO-CJ"),
        new("Turda", "RO-CJ"),
        new("Dej", "RO-CJ"),
        new("Câmpia Turzii", "RO-CJ"),
        new("Gherla", "RO-CJ"),
        new("Huedin", "RO-CJ"),

        // Constanța
        new("Constanța", "RO-CT"),
        new("Mangalia", "RO-CT"),
        new("Medgidia", "RO-CT"),
        new("Năvodari", "RO-CT"),
        new("Cernavodă", "RO-CT"),
        new("Ovidiu", "RO-CT"),
        new("Murfatlar", "RO-CT"),
        new("Eforie", "RO-CT"),
        new("Techirghiol", "RO-CT"),
        new("Hârșova", "RO-CT"),
        new("Băneasa", "RO-CT"),
        new("Negru Vodă", "RO-CT"),

        // Covasna
        new("Sfântu Gheorghe", "RO-CV"),
        new("Târgu Secuiesc", "RO-CV"),
        new("Covasna", "RO-CV"),
        new("Baraolt", "RO-CV"),
        new("Întorsura Buzăului", "RO-CV"),

        // Dâmbovița
        new("Târgoviște", "RO-DB"),
        new("Moreni", "RO-DB"),
        new("Pucioasa", "RO-DB"),
        new("Găești", "RO-DB"),
        new("Titu", "RO-DB"),
        new("Fieni", "RO-DB"),

        // Dolj
        new("Craiova", "RO-DJ"),
        new("Băilești", "RO-DJ"),
        new("Calafat", "RO-DJ"),
        new("Filiași", "RO-DJ"),
        new("Segarcea", "RO-DJ"),
        new("Dăbuleni", "RO-DJ"),
        new("Bechet", "RO-DJ"),

        // Galați
        new("Galați", "RO-GL"),
        new("Tecuci", "RO-GL"),
        new("Târgu Bujor", "RO-GL"),
        new("Berești", "RO-GL"),

        // Giurgiu
        new("Giurgiu", "RO-GR"),
        new("Bolintin-Vale", "RO-GR"),
        new("Mihăilești", "RO-GR"),

        // Gorj
        new("Târgu Jiu", "RO-GJ"),
        new("Motru", "RO-GJ"),
        new("Rovinari", "RO-GJ"),
        new("Bumbești-Jiu", "RO-GJ"),
        new("Țicleni", "RO-GJ"),
        new("Novaci", "RO-GJ"),
        new("Tismana", "RO-GJ"),
        new("Turceni", "RO-GJ"),

        // Harghita
        new("Miercurea Ciuc", "RO-HR"),
        new("Odorheiu Secuiesc", "RO-HR"),
        new("Gheorgheni", "RO-HR"),
        new("Toplița", "RO-HR"),
        new("Cristuru Secuiesc", "RO-HR"),
        new("Bălan", "RO-HR"),
        new("Borsec", "RO-HR"),
        new("Vlăhița", "RO-HR"),
        new("Băile Tușnad", "RO-HR"),

        // Hunedoara
        new("Deva", "RO-HD"),
        new("Hunedoara", "RO-HD"),
        new("Petroșani", "RO-HD"),
        new("Lupeni", "RO-HD"),
        new("Vulcan", "RO-HD"),
        new("Brad", "RO-HD"),
        new("Orăștie", "RO-HD"),
        new("Petrila", "RO-HD"),
        new("Simeria", "RO-HD"),
        new("Călan", "RO-HD"),
        new("Hațeg", "RO-HD"),
        new("Uricani", "RO-HD"),
        new("Aninoasa", "RO-HD"),
        new("Geoagiu", "RO-HD"),

        // Ialomița
        new("Slobozia", "RO-IL"),
        new("Fetești", "RO-IL"),
        new("Urziceni", "RO-IL"),
        new("Țăndărei", "RO-IL"),
        new("Amara", "RO-IL"),
        new("Căzănești", "RO-IL"),
        new("Fierbinți-Târg", "RO-IL"),

        // Iași
        new("Iași", "RO-IS"),
        new("Pașcani", "RO-IS"),
        new("Hârlău", "RO-IS"),
        new("Târgu Frumos", "RO-IS"),
        new("Podu Iloaiei", "RO-IS"),

        // Ilfov
        new("Buftea", "RO-IF"),
        new("Voluntari", "RO-IF"),
        new("Pantelimon", "RO-IF"),
        new("Popești-Leordeni", "RO-IF"),
        new("Bragadiru", "RO-IF"),
        new("Chitila", "RO-IF"),
        new("Măgurele", "RO-IF"),
        new("Otopeni", "RO-IF"),
        new("Cornetu", "RO-IF"),
        new("1 Decembrie", "RO-IF"),

        // Maramureș
        new("Baia Mare", "RO-MM"),
        new("Sighetu Marmației", "RO-MM"),
        new("Borșa", "RO-MM"),
        new("Vișeu de Sus", "RO-MM"),
        new("Târgu Lăpuș", "RO-MM"),
        new("Baia Sprie", "RO-MM"),
        new("Seini", "RO-MM"),
        new("Șomcuta Mare", "RO-MM"),
        new("Cavnic", "RO-MM"),
        new("Ulmeni", "RO-MM"),
        new("Tăuții-Măgherăuș", "RO-MM"),
        new("Dragomirești", "RO-MM"),

        // Mehedinți
        new("Drobeta-Turnu Severin", "RO-MH"),
        new("Orșova", "RO-MH"),
        new("Strehaia", "RO-MH"),
        new("Vânju Mare", "RO-MH"),
        new("Baia de Aramă", "RO-MH"),

        // Mureș
        new("Târgu Mureș", "RO-MS"),
        new("Reghin", "RO-MS"),
        new("Sighișoara", "RO-MS"),
        new("Târnăveni", "RO-MS"),
        new("Luduș", "RO-MS"),
        new("Sovata", "RO-MS"),
        new("Iernut", "RO-MS"),
        new("Ungheni", "RO-MS"),
        new("Sângeorgiu de Pădure", "RO-MS"),
        new("Miercurea Nirajului", "RO-MS"),

        // Neamț
        new("Piatra Neamț", "RO-NT"),
        new("Roman", "RO-NT"),
        new("Târgu Neamț", "RO-NT"),
        new("Bicaz", "RO-NT"),
        new("Roznov", "RO-NT"),

        // Olt
        new("Slatina", "RO-OT"),
        new("Caracal", "RO-OT"),
        new("Balș", "RO-OT"),
        new("Corabia", "RO-OT"),
        new("Scornicești", "RO-OT"),
        new("Drăgănești-Olt", "RO-OT"),
        new("Piatra-Olt", "RO-OT"),

        // Prahova
        new("Ploiești", "RO-PH"),
        new("Câmpina", "RO-PH"),
        new("Băicoi", "RO-PH"),
        new("Breaza", "RO-PH"),
        new("Mizil", "RO-PH"),
        new("Sinaia", "RO-PH"),
        new("Bușteni", "RO-PH"),
        new("Azuga", "RO-PH"),
        new("Comarnic", "RO-PH"),
        new("Urlați", "RO-PH"),
        new("Vălenii de Munte", "RO-PH"),
        new("Boldești-Scăeni", "RO-PH"),
        new("Slănic", "RO-PH"),
        new("Plopeni", "RO-PH"),

        // Sălaj
        new("Zalău", "RO-SJ"),
        new("Șimleu Silvaniei", "RO-SJ"),
        new("Jibou", "RO-SJ"),
        new("Cehu Silvaniei", "RO-SJ"),

        // Satu Mare
        new("Satu Mare", "RO-SM"),
        new("Carei", "RO-SM"),
        new("Negrești-Oaș", "RO-SM"),
        new("Tășnad", "RO-SM"),
        new("Livada", "RO-SM"),
        new("Ardud", "RO-SM"),

        // Sibiu
        new("Sibiu", "RO-SB"),
        new("Mediaș", "RO-SB"),
        new("Cisnădie", "RO-SB"),
        new("Avrig", "RO-SB"),
        new("Agnita", "RO-SB"),
        new("Dumbrăveni", "RO-SB"),
        new("Copșa Mică", "RO-SB"),
        new("Tălmaciu", "RO-SB"),
        new("Miercurea Sibiului", "RO-SB"),
        new("Ocna Sibiului", "RO-SB"),
        new("Săliște", "RO-SB"),

        // Suceava
        new("Suceava", "RO-SV"),
        new("Fălticeni", "RO-SV"),
        new("Rădăuți", "RO-SV"),
        new("Câmpulung Moldovenesc", "RO-SV"),
        new("Vatra Dornei", "RO-SV"),
        new("Gura Humorului", "RO-SV"),
        new("Siret", "RO-SV"),
        new("Solca", "RO-SV"),
        new("Cajvana", "RO-SV"),
        new("Dolhasca", "RO-SV"),
        new("Frasin", "RO-SV"),
        new("Liteni", "RO-SV"),
        new("Milișăuți", "RO-SV"),
        new("Salcea", "RO-SV"),
        new("Vicovu de Sus", "RO-SV"),

        // Teleorman
        new("Alexandria", "RO-TR"),
        new("Roșiori de Vede", "RO-TR"),
        new("Turnu Măgurele", "RO-TR"),
        new("Zimnicea", "RO-TR"),
        new("Videle", "RO-TR"),

        // Timiș
        new("Timișoara", "RO-TM"),
        new("Lugoj", "RO-TM"),
        new("Sânnicolau Mare", "RO-TM"),
        new("Jimbolia", "RO-TM"),
        new("Făget", "RO-TM"),
        new("Buziaș", "RO-TM"),
        new("Deta", "RO-TM"),
        new("Recaș", "RO-TM"),
        new("Gătaia", "RO-TM"),
        new("Ciacova", "RO-TM"),

        // Tulcea
        new("Tulcea", "RO-TL"),
        new("Babadag", "RO-TL"),
        new("Măcin", "RO-TL"),
        new("Isaccea", "RO-TL"),
        new("Sulina", "RO-TL"),

        // Vâlcea
        new("Râmnicu Vâlcea", "RO-VL"),
        new("Drăgășani", "RO-VL"),
        new("Călimănești", "RO-VL"),
        new("Brezoi", "RO-VL"),
        new("Horezu", "RO-VL"),
        new("Băile Govora", "RO-VL"),
        new("Băile Olănești", "RO-VL"),
        new("Ocnele Mari", "RO-VL"),
        new("Berbești", "RO-VL"),
        new("Bălcești", "RO-VL"),

        // Vaslui
        new("Vaslui", "RO-VS"),
        new("Bârlad", "RO-VS"),
        new("Huși", "RO-VS"),
        new("Negrești", "RO-VS"),
        new("Murgeni", "RO-VS"),

        // Vrancea
        new("Focșani", "RO-VN"),
        new("Adjud", "RO-VN"),
        new("Mărășești", "RO-VN"),
        new("Panciu", "RO-VN"),
        new("Odobești", "RO-VN")
    };

    #endregion
}

public record CountyDto(string Code, string Name);
public record CityDto(string Name, string CountyCode);