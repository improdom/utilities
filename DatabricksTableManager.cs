using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class DaxPrettyPrinter
{
    // Entry point
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

            // Normalize key function casing (optional)
            if (IsKeyword(t)) t = t.ToUpperInvariant();

            // Measure assignment
            if (t == ":=")
            {
                EnsureSpaceBefore(sb);
                sb.Append(":=");
                NewLine();
                NewLine();
                prev = t;
                continue;
            }

            // Braces list formatting: { ... }
            if (t == "{")
            {
                bool multilineList = ShouldMultilineBraceList(tokens, i, out int closeBraceIndex);

                // Keep "{ x, y }" inline for short lists
                if (!multilineList)
                {
                    EnsureSpaceBefore(sb);
                    Write("{");
                    EnsureSpaceAfter(sb);
                    prev = t;
                    continue;
                }

                // Multiline list:
                EnsureSpaceBefore(sb);
                Write("{");
                NewLine();
                indent++;

                // Emit list items separated by commas into lines until matching "}"
                // We assume tokenizer gives commas as tokens.
                for (int j = i + 1; j < closeBraceIndex; j++)
                {
                    string tok = tokens[j];

                    if (tok == ",")
                    {
                        // comma ends the line
                        Write(",");
                        NewLine();
                        continue;
                    }

                    // avoid leading spaces at line start; Write() handles indentation
                    Write(tok);
                }

                indent--;
                NewLine();
                Write("}");

                i = closeBraceIndex; // jump to closing brace consumed
                prev = "}";
                continue;
            }

            if (t == "}")
            {
                // Usually handled by the { formatter above
                Write("}");
                prev = t;
                continue;
            }

            // Parentheses formatting
            if (t == "(")
            {
                bool breakAfterParen = ShouldBreakAfterOpenParen(tokens, i, prev);

                // Tabular Editor style has a space before "(" for function calls: CALCULATE (
                if (prev != null && IsIdentifierLike(prev))
                    EnsureSpaceBefore(sb);

                Write("(");

                if (breakAfterParen)
                {
                    indent++;
                    NewLine();
                }

                prev = t;
                continue;
            }

            if (t == ")")
            {
                // If we previously broke after "(", we should close aligned
                bool wasMultiline = WasLikelyMultilineParenContext(sb);

                if (wasMultiline)
                {
                    indent = Math.Max(0, indent - 1);

                    if (!atLineStart)
                        NewLine();

                    Write(")");
                }
                else
                {
                    // inline close
                    Write(")");
                }

                prev = t;
                continue;
            }

            // Comma formatting:
            if (t == ",")
            {
                Write(",");
                // In your screenshot: every CALCULATE argument goes on its own line
                NewLine();
                prev = t;
                continue;
            }

            // Operators spacing (including IN)
            if (IsOperatorToken(t))
            {
                EnsureSpaceBefore(sb);
                Write(t.ToUpperInvariant() == "IN" ? "IN" : t);
                EnsureSpaceAfter(sb);
                prev = t;
                continue;
            }

            // Default spacing rule
            if (NeedsSpaceBetween(prev, t))
                EnsureSpaceBefore(sb);

            // Special: when we see "IN" and next is "{", keep "IN {" on same line
            // and let the brace formatter decide whether to multiline the list.
            Write(t);
            prev = t;
        }

        TrimTrailingSpaces(sb);
        return sb.ToString().TrimEnd();
    }

    // ----------------- Heuristics -----------------

    // Decide if we should newline after "("
    // - Always multiline for CALCULATE(
    // - For KEEPFILTERS( ... ): inline if short/simple; multiline if contains long IN {list} or nested commas
    private static bool ShouldBreakAfterOpenParen(List<string> tokens, int openParenIndex, string prevToken)
    {
        string prev = prevToken ?? "";

        if (prev.Equals("CALCULATE", StringComparison.OrdinalIgnoreCase))
            return true;

        if (prev.Equals("KEEPFILTERS", StringComparison.OrdinalIgnoreCase))
        {
            int close = FindMatching(tokens, openParenIndex, "(", ")");
            if (close < 0) return true;

            // If there is any top-level comma inside, prefer multiline
            if (ContainsTopLevelComma(tokens, openParenIndex + 1, close - 1))
                return true;

            // If there's an IN { ... } and the brace list is long => multiline
            for (int i = openParenIndex + 1; i < close; i++)
            {
                if (tokens[i].Equals("IN", StringComparison.OrdinalIgnoreCase))
                {
                    int brace = NextNonWsToken(tokens, i + 1);
                    if (brace >= 0 && tokens[brace] == "{")
                    {
                        if (ShouldMultilineBraceList(tokens, brace, out _))
                            return true;
                    }
                }
            }

            // Otherwise keep it inline (this matches your "NOT ... IN { \"Y\" }" case)
            return false;
        }

        // Default: inline unless it’s a known “argument-list heavy” function
        if (IsArgumentHeavyFunction(prev))
            return true;

        return false;
    }

    private static bool IsArgumentHeavyFunction(string name)
        => name.Equals("FILTER", StringComparison.OrdinalIgnoreCase)
        || name.Equals("SUMMARIZECOLUMNS", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ADDCOLUMNS", StringComparison.OrdinalIgnoreCase)
        || name.Equals("SELECTCOLUMNS", StringComparison.OrdinalIgnoreCase);

    // Brace list is multiline if items > 3 or any item is "large" (numbers etc.)
    private static bool ShouldMultilineBraceList(List<string> tokens, int openBraceIndex, out int closeBraceIndex)
    {
        closeBraceIndex = FindMatching(tokens, openBraceIndex, "{", "}");
        if (closeBraceIndex < 0) return false;

        // Count items at top level inside braces
        int items = 0;
        int depthParen = 0;
        int depthBrace = 0;

        var currentItem = new StringBuilder();
        int maxItemLen = 0;

        for (int i = openBraceIndex + 1; i < closeBraceIndex; i++)
        {
            string t = tokens[i];

            if (t == "(") depthParen++;
            if (t == ")") depthParen--;
            if (t == "{") depthBrace++;
            if (t == "}") depthBrace--;

            if (depthParen == 0 && depthBrace == 0 && t == ",")
            {
                items++;
                maxItemLen = Math.Max(maxItemLen, currentItem.ToString().Trim().Length);
                currentItem.Clear();
                continue;
            }

            currentItem.Append(t);
        }

        // last item
        if (currentItem.ToString().Trim().Length > 0)
        {
            items++;
            maxItemLen = Math.Max(maxItemLen, currentItem.ToString().Trim().Length);
        }

        // Heuristics tuned to your screenshot
        if (items > 3) return true;
        if (maxItemLen >= 8) return true; // large numbers tend to be better multiline

        return false;
    }

    private static bool ContainsTopLevelComma(List<string> tokens, int start, int end)
    {
        int p = 0, b = 0;
        for (int i = start; i <= end; i++)
        {
            var t = tokens[i];
            if (t == "(") p++;
            else if (t == ")") p--;
            else if (t == "{") b++;
            else if (t == "}") b--;
            else if (t == "," && p == 0 && b == 0)
                return true;
        }
        return false;
    }

    private static int FindMatching(List<string> tokens, int openIndex, string open, string close)
    {
        int depth = 0;
        for (int i = openIndex; i < tokens.Count; i++)
        {
            if (tokens[i] == open) depth++;
            else if (tokens[i] == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static int NextNonWsToken(List<string> tokens, int start)
    {
        // Tokenizer already removes whitespace; kept for clarity
        if (start >= tokens.Count) return -1;
        return start;
    }

    // crude: if we recently inserted a newline after an open paren, we’re in multiline mode.
    // This helps align the closing ')'.
    private static bool WasLikelyMultilineParenContext(StringBuilder sb)
    {
        // If the last non-space char is '\n', we are at a new line => treat multiline.
        for (int i = sb.Length - 1; i >= 0; i--)
        {
            char c = sb[i];
            if (c == ' ' || c == '\t' || c == '\r') continue;
            return c == '\n';
        }
        return false;
    }

    // ----------------- Tokenizer -----------------

    private static List<string> Tokenize(string s)
    {
        var tokens = new List<string>();
        int i = 0;

        while (i < s.Length)
        {
            char c = s[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // strings
            if (c == '"')
            {
                int start = i++;
                while (i < s.Length)
                {
                    if (s[i] == '"')
                    {
                        i++;
                        if (i < s.Length && s[i] == '"') { i++; continue; } // escaped ""
                        break;
                    }
                    i++;
                }
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // quoted table names
            if (c == '\'')
            {
                int start = i++;
                while (i < s.Length)
                {
                    if (s[i] == '\'')
                    {
                        i++;
                        if (i < s.Length && s[i] == '\'') { i++; continue; } // escaped ''
                        break;
                    }
                    i++;
                }
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // 2-char ops
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

            // punctuation
            if (c is '(' or ')' or ',' or '{' or '}')
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

            // [Measure/Column]
            if (c == '[')
            {
                int start = i++;
                while (i < s.Length && s[i] != ']') i++;
                if (i < s.Length) i++;
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // identifier/number
            if (char.IsLetterOrDigit(c) || c == '_' )
            {
                int start = i++;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' ))
                    i++;
                tokens.Add(s.Substring(start, i - start));
                continue;
            }

            // fallback
            tokens.Add(c.ToString());
            i++;
        }

        return tokens;
    }

    // ----------------- Helpers -----------------

    private static bool IsKeyword(string t)
        => t.Equals("calculate", StringComparison.OrdinalIgnoreCase)
        || t.Equals("keepfilters", StringComparison.OrdinalIgnoreCase)
        || t.Equals("var", StringComparison.OrdinalIgnoreCase)
        || t.Equals("return", StringComparison.OrdinalIgnoreCase)
        || t.Equals("in", StringComparison.OrdinalIgnoreCase)
        || t.Equals("not", StringComparison.OrdinalIgnoreCase);

    private static bool IsOperatorToken(string t)
        => t is "=" or ">=" or "<=" or "<>" or ">" or "<" or "+" or "-" or "*" or "/" or "&&" or "||"
           || t.Equals("IN", StringComparison.OrdinalIgnoreCase);

    private static bool IsIdentifierLike(string t)
    {
        if (string.IsNullOrEmpty(t)) return false;
        if (t.StartsWith("[") && t.EndsWith("]")) return true;
        if (t.StartsWith("'") && t.EndsWith("'")) return true;
        return char.IsLetterOrDigit(t[0]) || t[0] == '_';
    }

    private static bool NeedsSpaceBetween(string prev, string cur)
    {
        if (string.IsNullOrEmpty(prev) || string.IsNullOrEmpty(cur)) return false;

        if (prev == "(" || prev == "{" ) return false;
        if (cur == ")" || cur == "}" || cur == ",") return false;

        if (IsOperatorToken(prev) || IsOperatorToken(cur)) return false;

        // avoid space between identifier and [Column] etc? usually fine either way
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
