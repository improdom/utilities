using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class AesUtility
{
    // Replace with your secure key and IV (must be 32 and 16 bytes respectively)
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012"); // 32 bytes
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456"); // 16 bytes

    public static string Encrypt(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Key;
            aes.IV = IV;

            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var writer = new StreamWriter(cryptoStream))
                {
                    writer.Write(plainText);
                }

                return Convert.ToBase64String(ms.ToArray())
                             .Replace('+', '-')
                             .Replace('/', '_')
                             .TrimEnd('='); // URL-safe
            }
        }
    }

    public static string Decrypt(string encryptedText)
    {
        // Rebuild base64 from URL-safe format
        string base64 = encryptedText.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        byte[] buffer = Convert.FromBase64String(base64);

        using (Aes aes = Aes.Create())
        {
            aes.Key = Key;
            aes.IV = IV;

            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream(buffer))
            using (var cryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var reader = new StreamReader(cryptoStream))
            {
                return reader.ReadToEnd();
            }
        }
    }

    SELECT
  business_date,
  query_name,
  attribute_map,
  measure_map,
  concat_ws(',', map_keys(attribute_map), map_keys(measure_map)) AS used_attributes
FROM pbi_fact_risk_results_trend
}



private static HashSet<string> ExtractMeasures(string dax)
{
    var measures = new HashSet<string>();

    // This regex matches [MeasureName] that is NOT preceded by either:
    // - a quote and word: 'Some Table'[
    // - an unquoted word: SomeTable[
    // The lookbehind is fixed-width due to .NET limitations
    var regex = new Regex(@"(?<!['\w]{1,30}\s*\[)\[(?<measure>[^\[\]]+)\]", RegexOptions.IgnoreCase);

    foreach (Match match in regex.Matches(dax))
    {
        var measureName = match.Groups["measure"].Value.Trim();
        measures.Add(measureName);
    }

    return measures;
}



