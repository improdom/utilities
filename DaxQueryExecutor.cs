using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class DaxFilterParser
{
    public static List<DateTime> ExtractCobDates(string daxQuery)
    {
        var cobDates = new List<DateTime>();

        // Normalize input: remove extra spaces, unify casing
        var normalized = Regex.Replace(daxQuery, @"\s+", " ").ToLowerInvariant();

        // Match patterns like: treatas({ date(2025, 6, 11) }, 'cob date'[cob date])
        var pattern = @"treatas\s*\(\s*\{\s*date\s*\(\s*(\d{4})\s*,\s*(\d{1,2})\s*,\s*(\d{1,2})\s*\)\s*\}\s*,\s*'[^']*cob\s*date'\s*\[\s*cob\s*date\s*\]\s*\)";

        var matches = Regex.Matches(normalized, pattern, RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int year) &&
                int.TryParse(match.Groups[2].Value, out int month) &&
                int.TryParse(match.Groups[3].Value, out int day))
            {
                try
                {
                    cobDates.Add(new DateTime(year, month, day));
                }
                catch
                {
                    // Skip invalid dates
                }
            }
        }

        return cobDates;
    }
}
