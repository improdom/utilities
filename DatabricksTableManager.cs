using System;
using System.Collections.Generic;
using System.Text;

public static class DaxPrettyPrinter
{
    public static string FormatDax(string dax, int indentSize = 4)
    {
        if (string.IsNullOrWhiteSpace(dax))
            return string.Empty;

        var tokens = Tokenize(dax);
        var sb = new StringBuilder();

        int indent = 0;
        bool atLineStart = true;

        string prev = null;

        void NewLine()
        {
            // avoid trailing spaces at EOL
            TrimTrailingSpaces(sb);
            sb.AppendLine();
            atLineStart = true;
        }

        void WriteIndentIfNeeded()
        {
            if (!atLineStart) return;
            sb.Append(new string(' ', Math.Max(0, indent) * indentSize));
            atLineStart = false;
        }

        void Write(string s)
        {
            WriteIndentIfNeeded();
            sb.Append(s);
        }

        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];
            string next = (i + 1 < tokens.Count) ? tokens[i + 1] : null;

            // normalize keyword casing for readability (optional)
            if (IsKeyword(t)) t = t.ToUpperInvariant();

            // Special: measure assignment `:=`
            if (t == ":=")
            {
                // ensure " := " spacing
                EnsureSpaceBefore(sb);
                sb.Append(":=");
                NewLine();
                NewLine(); // matches the “blank line then expression” feel
                prev = t;
                continue;
            }

            // Opening paren
            if (t == "(")
            {
                // space before "(" if it looks like a function call: IDENTIFIER (
                if (prev != null && (IsIdentifierLike(prev) || prev == "]" || prev == "'" || prev == "\""))
                    EnsureSpaceBefore(sb);

                Write("(");
                indent++;
                NewLine();
                prev = t;
                continue;
            }

            // Closing paren
            if (t == ")")
            {
                indent = Math.Max(0, indent - 1);

                // If we're mid-line with content, break before closing paren for alignment
                if (!atLineStart)
                    NewLine();

                Write(")");
                prev = t;
                continue;
            }

            // Comma => line break after
            if (t == ",")
            {
                Write(",");
                NewLine();
                prev = t;
                continue;
            }

            // Operators: enforce spaces around
            if (IsOperatorToken(t))
            {
                EnsureSpaceBefore(sb);
                Write(t);
                EnsureSpaceAfter(sb);
                prev = t;
                continue;
            }

            // Default token printing with smart spacing
            if (NeedsSpaceBetween(prev, t))
                EnsureSpaceBefore(sb);

            Write(t);
            prev = t;
        }

        TrimTrailingSpaces(sb);
        return sb.ToString().TrimEnd();
    }

    // ---------------- Tokenizer ----------------

    private static List<string> Tokenize(string s)
    {
        var tokens = new List<string>();
        int i = 0;

        while (i < s.Length)
        {
            char c = s[i];

            // whitespace => collapse
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // line comment //
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                int start = i;
                i += 2;
                while (i < s.Length && s[i] != '\n') i++;
                tokens.Add(s.Substring(start, i - start).TrimEnd('\r'));
                continue;
            }

            // block comment /* ... */
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                int start = i;
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                i = Math.Min(s.Length, i + 2);
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // string literal "..."
            if (c == '"')
            {
                int start = i++;
                while (i < s.Length)
                {
                    if (s[i] == '"' )
                    {
                        i++;
                        // handle doubled quotes "" inside strings
                        if (i < s.Length && s[i] == '"') { i++; continue; }
                        break;
                    }
                    i++;
                }
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // quoted table name 'Table Name'
            if (c == '\'')
            {
                int start = i++;
                while (i < s.Length)
                {
                    if (s[i] == '\'')
                    {
                        i++;
                        // handle doubled '' inside
                        if (i < s.Length && s[i] == '\'') { i++; continue; }
                        break;
                    }
                    i++;
                }
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // two-char operators and :=
            if (i + 1 < s.Length)
            {
                string two = s.Substring(i, 2);
                if (two is ":=" or ">=" or "<=" or "<>" or "&&" or "||")
                {
                    tokens.Add(two);
                    i += 2;
                    continue;
                }
            }

            // single-char punctuation
            if (c is '(' or ')' or ',' )
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            // single-char operators
            if (c is '=' or '+' or '-' or '*' or '/' or '>' or '<')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            // bracketed measure/column [Name]
            if (c == '[')
            {
                int start = i++;
                while (i < s.Length && s[i] != ']') i++;
                if (i < s.Length) i++; // include ]
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // identifier/number/keyword (includes underscores)
            if (char.IsLetterOrDigit(c) || c == '_' )
            {
                int start = i++;
                while (i < s.Length)
                {
                    char ch = s[i];
                    if (char.IsLetterOrDigit(ch) || ch == '_' )
                        i++;
                    else
                        break;
                }
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // fallback: emit char as token
            tokens.Add(c.ToString());
            i++;
        }

        return tokens;
    }

    // ---------------- Formatting helpers ----------------

    private static bool IsKeyword(string t)
        => t.Equals("calculate", StringComparison.OrdinalIgnoreCase)
        || t.Equals("keepfilters", StringComparison.OrdinalIgnoreCase)
        || t.Equals("var", StringComparison.OrdinalIgnoreCase)
        || t.Equals("return", StringComparison.OrdinalIgnoreCase)
        || t.Equals("in", StringComparison.OrdinalIgnoreCase);

    private static bool IsOperatorToken(string t)
        => t is "=" or ">=" or "<=" or "<>" or ">" or "<" or "+" or "-" or "*" or "/" or "&&" or "||"
           || t.Equals("IN", StringComparison.OrdinalIgnoreCase);

    private static bool IsIdentifierLike(string t)
    {
        if (string.IsNullOrEmpty(t)) return false;
        if (t.StartsWith("[") && t.EndsWith("]")) return true;     // [Measure]
        if (t.StartsWith("'") && t.EndsWith("'")) return true;     // 'Table Name'
        return char.IsLetterOrDigit(t[0]) || t[0] == '_';
    }

    private static bool NeedsSpaceBetween(string prev, string cur)
    {
        if (string.IsNullOrEmpty(prev) || string.IsNullOrEmpty(cur)) return false;

        // no spaces at line start handled elsewhere
        if (prev == "(") return false;
        if (cur == ")") return false;
        if (cur == ",") return false;

        // operators handle their own spacing
        if (IsOperatorToken(prev) || IsOperatorToken(cur)) return false;

        // identifiers next to identifiers should have a space (e.g., "KEEPFILTERS IN" though IN is operator/keyword)
        if (IsIdentifierLike(prev) && IsIdentifierLike(cur))
            return true;

        return false;
    }

    private static void EnsureSpaceBefore(StringBuilder sb)
    {
        if (sb.Length == 0) return;
        char last = sb[sb.Length - 1];
        if (last != ' ' && last != '\n' && last != '\r' && last != '\t')
            sb.Append(' ');
    }

    private static void EnsureSpaceAfter(StringBuilder sb)
    {
        if (sb.Length == 0) return;
        char last = sb[sb.Length - 1];
        if (last != ' ')
            sb.Append(' ');
    }

    private static void TrimTrailingSpaces(StringBuilder sb)
    {
        while (sb.Length > 0)
        {
            char last = sb[sb.Length - 1];
            if (last == ' ' || last == '\t')
                sb.Length--;
            else
                break;
        }
    }
}
