using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;

namespace Faemiyah.Arranger.Contracts;

/// <summary>
/// The ISO 4217 currencies in active use, shared by the server (validation) and the client (pickers).
/// </summary>
public static class Currencies
{
    /// <summary>
    /// The currency used when nothing else has been chosen.
    /// </summary>
    public const string Default = "EUR";

    /// <summary>
    /// All currencies, ordered by code.
    /// </summary>
    public static IReadOnlyList<Currency> All { get; } =
    [
        new("AED", "UAE Dirham"),
        new("AFN", "Afghani"),
        new("ALL", "Lek"),
        new("AMD", "Armenian Dram"),
        new("AOA", "Kwanza"),
        new("ARS", "Argentine Peso"),
        new("AUD", "Australian Dollar"),
        new("AWG", "Aruban Florin"),
        new("AZN", "Azerbaijan Manat"),
        new("BAM", "Convertible Mark"),
        new("BBD", "Barbados Dollar"),
        new("BDT", "Taka"),
        new("BHD", "Bahraini Dinar"),
        new("BIF", "Burundi Franc"),
        new("BMD", "Bermudian Dollar"),
        new("BND", "Brunei Dollar"),
        new("BOB", "Boliviano"),
        new("BRL", "Brazilian Real"),
        new("BSD", "Bahamian Dollar"),
        new("BTN", "Ngultrum"),
        new("BWP", "Pula"),
        new("BYN", "Belarusian Ruble"),
        new("BZD", "Belize Dollar"),
        new("CAD", "Canadian Dollar"),
        new("CDF", "Congolese Franc"),
        new("CHF", "Swiss Franc"),
        new("CLP", "Chilean Peso"),
        new("CNY", "Yuan Renminbi"),
        new("COP", "Colombian Peso"),
        new("CRC", "Costa Rican Colon"),
        new("CUP", "Cuban Peso"),
        new("CVE", "Cabo Verde Escudo"),
        new("CZK", "Czech Koruna"),
        new("DJF", "Djibouti Franc"),
        new("DKK", "Danish Krone"),
        new("DOP", "Dominican Peso"),
        new("DZD", "Algerian Dinar"),
        new("EGP", "Egyptian Pound"),
        new("ERN", "Nakfa"),
        new("ETB", "Ethiopian Birr"),
        new("EUR", "Euro"),
        new("FJD", "Fiji Dollar"),
        new("FKP", "Falkland Islands Pound"),
        new("GBP", "Pound Sterling"),
        new("GEL", "Lari"),
        new("GHS", "Ghana Cedi"),
        new("GIP", "Gibraltar Pound"),
        new("GMD", "Dalasi"),
        new("GNF", "Guinean Franc"),
        new("GTQ", "Quetzal"),
        new("GYD", "Guyana Dollar"),
        new("HKD", "Hong Kong Dollar"),
        new("HNL", "Lempira"),
        new("HTG", "Gourde"),
        new("HUF", "Forint"),
        new("IDR", "Rupiah"),
        new("ILS", "New Israeli Sheqel"),
        new("INR", "Indian Rupee"),
        new("IQD", "Iraqi Dinar"),
        new("IRR", "Iranian Rial"),
        new("ISK", "Iceland Krona"),
        new("JMD", "Jamaican Dollar"),
        new("JOD", "Jordanian Dinar"),
        new("JPY", "Yen"),
        new("KES", "Kenyan Shilling"),
        new("KGS", "Som"),
        new("KHR", "Riel"),
        new("KMF", "Comorian Franc"),
        new("KPW", "North Korean Won"),
        new("KRW", "Won"),
        new("KWD", "Kuwaiti Dinar"),
        new("KYD", "Cayman Islands Dollar"),
        new("KZT", "Tenge"),
        new("LAK", "Lao Kip"),
        new("LBP", "Lebanese Pound"),
        new("LKR", "Sri Lanka Rupee"),
        new("LRD", "Liberian Dollar"),
        new("LSL", "Loti"),
        new("LYD", "Libyan Dinar"),
        new("MAD", "Moroccan Dirham"),
        new("MDL", "Moldovan Leu"),
        new("MGA", "Malagasy Ariary"),
        new("MKD", "Denar"),
        new("MMK", "Kyat"),
        new("MNT", "Tugrik"),
        new("MOP", "Pataca"),
        new("MRU", "Ouguiya"),
        new("MUR", "Mauritius Rupee"),
        new("MVR", "Rufiyaa"),
        new("MWK", "Malawi Kwacha"),
        new("MXN", "Mexican Peso"),
        new("MYR", "Malaysian Ringgit"),
        new("MZN", "Mozambique Metical"),
        new("NAD", "Namibia Dollar"),
        new("NGN", "Naira"),
        new("NIO", "Cordoba Oro"),
        new("NOK", "Norwegian Krone"),
        new("NPR", "Nepalese Rupee"),
        new("NZD", "New Zealand Dollar"),
        new("OMR", "Rial Omani"),
        new("PAB", "Balboa"),
        new("PEN", "Sol"),
        new("PGK", "Kina"),
        new("PHP", "Philippine Peso"),
        new("PKR", "Pakistan Rupee"),
        new("PLN", "Zloty"),
        new("PYG", "Guarani"),
        new("QAR", "Qatari Rial"),
        new("RON", "Romanian Leu"),
        new("RSD", "Serbian Dinar"),
        new("RUB", "Russian Ruble"),
        new("RWF", "Rwanda Franc"),
        new("SAR", "Saudi Riyal"),
        new("SBD", "Solomon Islands Dollar"),
        new("SCR", "Seychelles Rupee"),
        new("SDG", "Sudanese Pound"),
        new("SEK", "Swedish Krona"),
        new("SGD", "Singapore Dollar"),
        new("SHP", "Saint Helena Pound"),
        new("SLE", "Leone"),
        new("SOS", "Somali Shilling"),
        new("SRD", "Surinam Dollar"),
        new("SSP", "South Sudanese Pound"),
        new("STN", "Dobra"),
        new("SYP", "Syrian Pound"),
        new("SZL", "Lilangeni"),
        new("THB", "Baht"),
        new("TJS", "Somoni"),
        new("TMT", "Turkmenistan New Manat"),
        new("TND", "Tunisian Dinar"),
        new("TOP", "Pa'anga"),
        new("TRY", "Turkish Lira"),
        new("TTD", "Trinidad and Tobago Dollar"),
        new("TWD", "New Taiwan Dollar"),
        new("TZS", "Tanzanian Shilling"),
        new("UAH", "Hryvnia"),
        new("UGX", "Uganda Shilling"),
        new("USD", "US Dollar"),
        new("UYU", "Peso Uruguayo"),
        new("UZS", "Uzbekistan Sum"),
        new("VES", "Bolivar Soberano"),
        new("VND", "Dong"),
        new("VUV", "Vatu"),
        new("WST", "Tala"),
        new("XAF", "CFA Franc BEAC"),
        new("XCD", "East Caribbean Dollar"),
        new("XCG", "Caribbean Guilder"),
        new("XOF", "CFA Franc BCEAO"),
        new("XPF", "CFP Franc"),
        new("YER", "Yemeni Rial"),
        new("ZAR", "Rand"),
        new("ZMW", "Zambian Kwacha"),
        new("ZWG", "Zimbabwe Gold"),
    ];

    // Declared after All, which static initialization order requires.
    private static readonly FrozenDictionary<string, string> Names = All.ToFrozenDictionary(c => c.Code, c => c.Name, StringComparer.Ordinal);

    // ISO 4217 minor units that differ from two.
    private static readonly FrozenDictionary<string, int> UnusualDecimals = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["BIF"] = 0, ["CLP"] = 0, ["DJF"] = 0, ["GNF"] = 0, ["ISK"] = 0, ["JPY"] = 0, ["KMF"] = 0, ["KRW"] = 0,
        ["PYG"] = 0, ["RWF"] = 0, ["UGX"] = 0, ["VND"] = 0, ["VUV"] = 0, ["XAF"] = 0, ["XOF"] = 0, ["XPF"] = 0,
        ["BHD"] = 3, ["IQD"] = 3, ["JOD"] = 3, ["KWD"] = 3, ["LYD"] = 3, ["OMR"] = 3, ["TND"] = 3,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The number of decimals amounts in a currency have.
    /// </summary>
    /// <param name="code">The currency code.</param>
    /// <returns>The ISO 4217 minor units, two unless known otherwise.</returns>
    public static int Decimals(string code)
    {
        return UnusualDecimals.GetValueOrDefault(code, 2);
    }

    /// <summary>
    /// Whether the code is a known currency code. Codes are upper case.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns><b>true</b> when known.</returns>
    public static bool IsKnown(string? code)
    {
        return code != null && Names.ContainsKey(code);
    }

    /// <summary>
    /// The name of a currency.
    /// </summary>
    /// <param name="code">The currency code.</param>
    /// <returns>The name, or the code when not known.</returns>
    public static string Name(string code)
    {
        return Names.GetValueOrDefault(code, code);
    }
}
