using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuickSheetRegex;

class Program
{
    static void Main()
    {
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

                if (type == "init")
                {
                    var resp = new { type = "register", name = "quicksheet-regex", version = "1.0.0", prefix = "regex" };
                    Console.WriteLine(JsonSerializer.Serialize(resp));
                    Console.Out.Flush();
                }
                else if (type == "activate")
                {
                    string id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    string param = "";
                    if (root.TryGetProperty("params", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Array)
                    {
                        var arr = paramsEl.EnumerateArray();
                        if (arr.MoveNext()) param = arr.Current.GetString() ?? "";
                    }

                    var cells = Explain(param.Trim());
                    var response = new { type = "write", id, cells };
                    Console.WriteLine(JsonSerializer.Serialize(response));
                    Console.Out.Flush();
                }
            }
            catch { }
        }
    }

    static List<object> Explain(string pattern)
    {
        var cells = new List<object>();

        if (string.IsNullOrEmpty(pattern))
        {
            cells.Add(new { r = 0, c = 0, v = "Usage: regex <pattern>" });
            cells.Add(new { r = 1, c = 0, v = "Example: regex ^[a-z]+\\d{2,4}$" });
            return cells;
        }

        // Validate the regex
        try
        {
            _ = new Regex(pattern);
        }
        catch (ArgumentException ex)
        {
            cells.Add(new { r = 0, c = 0, v = $"INVALID: {ex.Message}" });
            return cells;
        }

        // Header
        cells.Add(new { r = 0, c = 0, v = $"Pattern: {pattern}" });

        int row = 1;

        // Tokenize and explain
        var tokens = Tokenize(pattern);
        foreach (var (token, explanation) in tokens)
        {
            cells.Add(new { r = row, c = 0, v = token });
            cells.Add(new { r = row, c = 1, v = explanation });
            row++;
        }

        // Summary
        row++;
        cells.Add(new { r = row, c = 0, v = "Summary:" });
        cells.Add(new { r = row, c = 1, v = Summarize(pattern, tokens) });

        return cells;
    }

    static List<(string token, string explanation)> Tokenize(string pattern)
    {
        var results = new List<(string, string)>();
        int i = 0;

        while (i < pattern.Length)
        {
            char c = pattern[i];

            // Anchors
            if (c == '^' && i == 0)
            {
                results.Add(("^", "Start of string anchor"));
                i++;
            }
            else if (c == '$' && i == pattern.Length - 1)
            {
                results.Add(("$", "End of string anchor"));
                i++;
            }
            // Character classes
            else if (c == '[')
            {
                int end = pattern.IndexOf(']', i + 1);
                if (end == -1) end = pattern.Length - 1;
                string cls = pattern[i..(end + 1)];
                results.Add((cls, ExplainCharClass(cls)));
                i = end + 1;
            }
            // Groups
            else if (c == '(')
            {
                int depth = 1;
                int end = i + 1;
                while (end < pattern.Length && depth > 0)
                {
                    if (pattern[end] == '(' && (end == 0 || pattern[end - 1] != '\\')) depth++;
                    else if (pattern[end] == ')' && (end == 0 || pattern[end - 1] != '\\')) depth--;
                    end++;
                }
                string group = pattern[i..end];
                results.Add((group, ExplainGroup(group)));
                i = end;
            }
            // Escape sequences
            else if (c == '\\' && i + 1 < pattern.Length)
            {
                string esc = pattern[i..(i + 2)];
                results.Add((esc, ExplainEscape(esc)));
                i += 2;
            }
            // Quantifiers
            else if (c == '{')
            {
                int end = pattern.IndexOf('}', i + 1);
                if (end == -1) { results.Add(("{", "Literal '{'")); i++; continue; }
                string quant = pattern[i..(end + 1)];
                // Attach to previous token if possible
                if (results.Count > 0)
                {
                    var (prevTok, prevExp) = results[^1];
                    results[^1] = (prevTok + quant, prevExp + " " + ExplainQuantifier(quant));
                }
                else
                {
                    results.Add((quant, ExplainQuantifier(quant)));
                }
                i = end + 1;
            }
            else if (c == '*' || c == '+' || c == '?')
            {
                string quant = c.ToString();
                if (i + 1 < pattern.Length && pattern[i + 1] == '?')
                {
                    quant += "?";
                }
                if (results.Count > 0)
                {
                    var (prevTok, prevExp) = results[^1];
                    results[^1] = (prevTok + quant, prevExp + " " + ExplainQuantifier(quant));
                }
                else
                {
                    results.Add((quant, ExplainQuantifier(quant)));
                }
                i += quant.Length;
            }
            // Alternation
            else if (c == '|')
            {
                results.Add(("|", "OR — alternation"));
                i++;
            }
            // Dot
            else if (c == '.')
            {
                results.Add((".", "Any character (except newline)"));
                i++;
            }
            // Literal
            else
            {
                // Collect consecutive literals
                int start = i;
                while (i < pattern.Length && !IsMetaChar(pattern[i]))
                    i++;
                string lit = pattern[start..i];
                results.Add((lit, lit.Length == 1 ? $"Literal '{lit}'" : $"Literal text \"{lit}\""));
            }
        }

        return results;
    }

    static bool IsMetaChar(char c) =>
        c == '.' || c == '*' || c == '+' || c == '?' || c == '|' ||
        c == '(' || c == ')' || c == '[' || c == ']' ||
        c == '{' || c == '}' || c == '\\' || c == '^' || c == '$';

    static string ExplainCharClass(string cls)
    {
        bool negated = cls.Length > 2 && cls[1] == '^';
        string inner = negated ? cls[2..^1] : cls[1..^1];
        string prefix = negated ? "NOT " : "";

        return inner switch
        {
            "a-z" => $"{prefix}Any lowercase letter",
            "A-Z" => $"{prefix}Any uppercase letter",
            "a-zA-Z" => $"{prefix}Any letter",
            "0-9" => $"{prefix}Any digit",
            "a-z0-9" or "a-zA-Z0-9" => $"{prefix}Any alphanumeric character",
            "\\s" => $"{prefix}Whitespace",
            _ => $"{prefix}One of: {inner}"
        };
    }

    static string ExplainGroup(string group)
    {
        if (group.StartsWith("(?:"))
            return $"Non-capturing group: {group[3..^1]}";
        if (group.StartsWith("(?="))
            return $"Positive lookahead: {group[3..^1]}";
        if (group.StartsWith("(?!"))
            return $"Negative lookahead: {group[3..^1]}";
        if (group.StartsWith("(?<="))
            return $"Positive lookbehind: {group[4..^1]}";
        if (group.StartsWith("(?<!"))
            return $"Negative lookbehind: {group[4..^1]}";
        if (group.StartsWith("(?<") || group.StartsWith("(?'"))
        {
            int nameEnd = group.IndexOfAny(new[] { '>', '\'' }, 3);
            if (nameEnd > 0)
            {
                string name = group[3..nameEnd];
                return $"Named capture group '{name}'";
            }
        }
        return $"Capturing group: {group[1..^1]}";
    }

    static string ExplainEscape(string esc)
    {
        return esc switch
        {
            "\\d" => "Any digit [0-9]",
            "\\D" => "Any non-digit",
            "\\w" => "Any word char [a-zA-Z0-9_]",
            "\\W" => "Any non-word char",
            "\\s" => "Any whitespace",
            "\\S" => "Any non-whitespace",
            "\\b" => "Word boundary",
            "\\B" => "Non-word boundary",
            "\\n" => "Newline",
            "\\r" => "Carriage return",
            "\\t" => "Tab",
            "\\." => "Literal '.'",
            "\\*" => "Literal '*'",
            "\\+" => "Literal '+'",
            "\\?" => "Literal '?'",
            "\\(" => "Literal '('",
            "\\)" => "Literal ')'",
            "\\[" => "Literal '['",
            "\\]" => "Literal ']'",
            "\\{" => "Literal '{'",
            "\\}" => "Literal '}'",
            "\\\\" => "Literal '\\'",
            "\\/" => "Literal '/'",
            "\\^" => "Literal '^'",
            "\\$" => "Literal '$'",
            "\\|" => "Literal '|'",
            _ => $"Escaped: {esc[1]}"
        };
    }

    static string ExplainQuantifier(string q)
    {
        if (q == "*") return "(zero or more, greedy)";
        if (q == "*?") return "(zero or more, lazy)";
        if (q == "+") return "(one or more, greedy)";
        if (q == "+?") return "(one or more, lazy)";
        if (q == "?") return "(optional)";
        if (q == "??") return "(optional, lazy)";

        // {n}, {n,}, {n,m}
        string inner = q.TrimStart('{').TrimEnd('}');
        if (inner.Contains(','))
        {
            var parts = inner.Split(',');
            if (string.IsNullOrEmpty(parts[1]))
                return $"({parts[0]} or more times)";
            return $"({parts[0]} to {parts[1]} times)";
        }
        return $"(exactly {inner} times)";
    }

    static string Summarize(string pattern, List<(string token, string explanation)> tokens)
    {
        int groups = 0;
        bool hasAnchorStart = false, hasAnchorEnd = false;
        foreach (var (tok, _) in tokens)
        {
            if (tok == "^") hasAnchorStart = true;
            if (tok == "$") hasAnchorEnd = true;
            if (tok.StartsWith("(") && !tok.StartsWith("(?:")) groups++;
        }

        var parts = new List<string>();
        if (hasAnchorStart && hasAnchorEnd)
            parts.Add("Full-string match");
        else if (hasAnchorStart)
            parts.Add("Anchored at start");
        else if (hasAnchorEnd)
            parts.Add("Anchored at end");

        if (groups > 0)
            parts.Add($"{groups} capture group(s)");

        parts.Add($"{tokens.Count} component(s)");

        return string.Join(", ", parts);
    }
}
