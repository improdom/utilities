using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MrvBuilder.MetricsViews
{
    // Adjust these aliases to match your real namespaces/types.
    using Old = Marvel.CubiQ.MrvDaxBuilder;
    using New = MrvBuilder.MetricsViews;

    public interface IFilterAdapter<in TOldFilter, out TNewFilter>
    {
        TNewFilter Adapt(TOldFilter oldFilter);
    }

    /// <summary>
    /// Adapts your existing MRV Builder Filter objects into the simplified MetricsView Filter model.
    ///
    /// Responsibilities:
    /// - Copy Dimension/Attribute/FilterType
    /// - Convert FilterValue into either:
    ///     - IN list (Values) if multiple tokens are detected
    ///     - scalar (ScalarValue) if only one token is detected
    /// - Infer numeric vs string for each token (basic heuristic)
    ///
    /// Notes:
    /// - SourceTableName/SourceColumnName are intentionally NOT set here (or copied if already present).
    ///   They are populated later by FilterSourceEnricher using SQL mapping.
    /// </summary>
    public sealed class FilterAdapter : IFilterAdapter<Old.Filter, New.Filter>
    {
        public New.Filter Adapt(Old.Filter oldFilter)
        {
            if (oldFilter is null) throw new ArgumentNullException(nameof(oldFilter));

            var nf = new New.Filter
            {
                Dimension = Normalize(oldFilter.Dimension),
                Attribute = Normalize(oldFilter.Attribute),
                FilterType = MapFilterType(oldFilter.FilterType),

                // If your old filter already has these properties populated, keep them.
                // Otherwise, FilterSourceEnricher will populate them later.
                SourceTableName = Normalize(GetIfExists(oldFilter, "SourceTableName")),
                SourceColumnName = Normalize(GetIfExists(oldFilter, "SourceColumnName"))
            };

            var raw = (oldFilter.FilterValue ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return nf;

            // 1) Tokenize
            var tokens = SplitToTokens(raw);

            // 2) Decide scalar vs IN-list
            if (tokens.Count <= 1)
            {
                var fv = ToFilterValue(tokens[0]);
                nf.ScalarValue = fv.Raw;
                nf.ScalarIsString = fv.IsString;
                return nf;
            }

            foreach (var t in tokens)
                nf.Values.Add(ToFilterValue(t));

            return nf;
        }

        private static New.FilterType MapFilterType(Old.FilterType oldType)
        {
            // Adapt to your old enum values.
            return oldType switch
            {
                Old.FilterType.Include => New.FilterType.Include,
                Old.FilterType.Exclude => New.FilterType.Exclude,

                // If your old model has "TableInclude"/etc., treat as Include for now.
                _ => New.FilterType.Include
            };
        }

        /// <summary>
        /// Splits FilterValue into tokens.
        /// Handles common formats:
        /// - "A" or "123"
        /// - "A,B,C"
        /// - "{A,B,C}"
        /// - "IN {A, B, C}"
        /// - "IN (A, B, C)"
        /// - "\"A\"" or "'A'"
        /// </summary>
        private static List<string> SplitToTokens(string raw)
        {
            raw = raw.Trim();

            // Remove a leading "IN" wrapper if present
            // Example: IN {1,2} or IN (1,2)
            if (raw.StartsWith("IN", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw.Substring(2).TrimStart();
            }

            // Strip surrounding braces/parens if the whole string is wrapped
            raw = StripOuter(raw, '{', '}');
            raw = StripOuter(raw, '(', ')');

            // If after stripping it still contains commas, split
            var tokens = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Where(t => t.Length > 0)
                            .ToList();

            // If no commas, keep as single token
            if (tokens.Count == 0)
                tokens.Add(raw);

            return tokens;
        }

        private static string StripOuter(string s, char open, char close)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == open && s[^1] == close)
                return s.Substring(1, s.Length - 2).Trim();
            return s;
        }

        /// <summary>
        /// Basic inference:
        /// - If surrounded by quotes -> string
        /// - If parses as long -> numeric
        /// - Otherwise -> string
        /// </summary>
        private static New.FilterValue ToFilterValue(string token)
        {
            token = token.Trim();

            // strip quotes
            if ((token.StartsWith("\"") && token.EndsWith("\"")) ||
                (token.StartsWith("'") && token.EndsWith("'")))
            {
                var inner = token.Substring(1, token.Length - 2);
                return new New.FilterValue(inner, isString: true);
            }

            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return new New.FilterValue(token, isString: false);

            return new New.FilterValue(token, isString: true);
        }

        private static string? Normalize(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Lets this adapter compile even if your old Filter doesn't yet expose SourceTableName/SourceColumnName.
        /// If your old class *does* have them, you can remove this and access directly.
        /// </summary>
        private static string? GetIfExists(object obj, string propName)
        {
            var p = obj.GetType().GetProperty(propName);
            if (p == null) return null;
            return p.GetValue(obj) as string;
        }
    }
}
