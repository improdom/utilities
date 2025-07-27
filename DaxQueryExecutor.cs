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

    // Match everything in brackets: [MeasureName]
    var bracketRegex = new Regex(@"\[(?<name>[^\[\]]+)\]", RegexOptions.IgnoreCase);
    var matches = bracketRegex.Matches(dax);

    foreach (Match match in matches)
    {
        var index = match.Index;
        var measureCandidate = match.Groups["name"].Value.Trim();

        // Look behind the current match up to 100 characters
        var lookBehind = dax.Substring(0, index);
        var lastQuote = lookBehind.LastIndexOf('\'');
        var lastBracket = lookBehind.LastIndexOf('[');

        // Check if there's a closing quote and opening bracket right before this one (i.e., 'Table'[Column])
        bool isQuotedTableColumn = lastQuote != -1 && lastBracket > lastQuote;

        // Check if pattern is like TableName[ColumnName]
        var tablePattern = new Regex(@"[A-Za-z0-9_]+(\s*)\[\s*$");
        bool isUnquotedTableColumn = tablePattern.IsMatch(lookBehind.Substring(Math.Max(0, lookBehind.Length - 50)));

        // If it's not part of a column reference, it's a measure
        if (!isQuotedTableColumn && !isUnquotedTableColumn)
        {
            measures.Add(measureCandidate);
        }
    }

    return measures;
}


