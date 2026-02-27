private static string? FormatDate(string? date)
{
    if (string.IsNullOrWhiteSpace(date))
        return date;

    // Accept plain date OR ISO timestamps
    if (DateTimeOffset.TryParse(date, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
    {
        return dto.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    return date;
}
