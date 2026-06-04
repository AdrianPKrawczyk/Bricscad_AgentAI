using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Bricscad_AgentAI_V2.UI
{
    public static class LatexToUnicodeConverter
    {
        private static readonly Dictionary<char, char> Superscripts = new Dictionary<char, char>
        {
            { '0', '⁰' }, { '1', '¹' }, { '2', '²' }, { '3', '³' }, { '4', '⁴' },
            { '5', '⁵' }, { '6', '⁶' }, { '7', '⁷' }, { '8', '⁸' }, { '9', '⁹' },
            { '+', '⁺' }, { '-', '⁻' }, { '=', '⁼' }, { '(', '⁽' }, { ')', '⁾' },
            { 'a', 'ᵃ' }, { 'b', 'ᵇ' }, { 'c', 'ᶜ' }, { 'd', 'ᵈ' }, { 'e', 'ᵉ' },
            { 'f', 'ᶠ' }, { 'g', 'ᵍ' }, { 'h', 'ʰ' }, { 'i', 'ⁱ' }, { 'j', 'ʲ' },
            { 'k', 'ᵏ' }, { 'l', 'ˡ' }, { 'm', 'ᵐ' }, { 'n', 'ⁿ' }, { 'o', 'ᵒ' },
            { 'p', 'ᵖ' }, { 'r', 'ʳ' }, { 's', 'ˢ' }, { 't', 'ᵗ' }, { 'u', 'ᵘ' },
            { 'v', 'ᵛ' }, { 'w', 'ʷ' }, { 'x', 'ˣ' }, { 'y', 'ʸ' }, { 'z', 'ᶻ' },
            { 'A', 'ᴬ' }, { 'B', 'ᴮ' }, { 'D', 'ᴰ' }, { 'E', 'ᴱ' }, { 'G', 'ᴳ' },
            { 'H', 'ᴴ' }, { 'I', 'ᴵ' }, { 'J', 'ᴶ' }, { 'K', 'ᴲ' }, { 'L', 'ᴸ' },
            { 'M', 'ᴹ' }, { 'N', 'ᴺ' }, { 'O', 'ᴼ' }, { 'P', 'ᴾ' }, { 'R', 'ᴿ' },
            { 'T', 'ᵀ' }, { 'U', 'ᵁ' }, { 'V', 'ⱽ' }, { 'W', 'ᵂ' }
        };

        private static readonly Dictionary<char, char> Subscripts = new Dictionary<char, char>
        {
            { '0', '₀' }, { '1', '₁' }, { '2', '₂' }, { '3', '₃' }, { '4', '₄' },
            { '5', '₅' }, { '6', '₆' }, { '7', '₇' }, { '8', '₈' }, { '9', '₉' },
            { '+', '₊' }, { '-', '₋' }, { '=', '₌' }, { '(', '₍' }, { ')', '₎' },
            { 'a', 'ₐ' }, { 'e', 'ₑ' }, { 'h', 'ₕ' }, { 'i', 'ᵢ' }, { 'j', 'ⱼ' },
            { 'k', 'ₖ' }, { 'l', 'ₗ' }, { 'm', 'ₘ' }, { 'n', 'ₙ' }, { 'o', 'ₒ' },
            { 'p', 'ₚ' }, { 'r', 'ᵣ' }, { 's', 'ₛ' }, { 't', 'ₜ' }, { 'u', 'ᵤ' },
            { 'v', 'ᵥ' }, { 'x', 'ₓ' }
        };

        private static readonly Dictionary<string, string> GreekLetters = new Dictionary<string, string>
        {
            { "\\alpha", "α" }, { "\\beta", "β" }, { "\\gamma", "γ" }, { "\\delta", "δ" },
            { "\\epsilon", "ε" }, { "\\zeta", "ζ" }, { "\\eta", "η" }, { "\\theta", "θ" },
            { "\\iota", "ι" }, { "\\kappa", "κ" }, { "\\lambda", "λ" }, { "\\mu", "μ" },
            { "\\nu", "ν" }, { "\\xi", "ξ" }, { "\\pi", "π" }, { "\\rho", "ρ" },
            { "\\sigma", "σ" }, { "\\tau", "τ" }, { "\\upsilon", "υ" }, { "\\phi", "φ" },
            { "\\chi", "χ" }, { "\\psi", "ψ" }, { "\\omega", "ω" },
            { "\\Delta", "Δ" }, { "\\Gamma", "Γ" }, { "\\Theta", "Θ" }, { "\\Lambda", "Λ" },
            { "\\Xi", "Ξ" }, { "\\Pi", "Π" }, { "\\Sigma", "Σ" }, { "\\Phi", "Φ" },
            { "\\Psi", "Ψ" }, { "\\Omega", "Ω" }
        };

        private static readonly Dictionary<string, string> MathSymbols = new Dictionary<string, string>
        {
            { "\\times", "×" },
            { "\\cdot", "·" },
            { "\\approx", "≈" },
            { "\\pm", "±" },
            { "\\neq", "≠" },
            { "\\leq", "≤" },
            { "\\geq", "≥" },
            { "\\degree", "°" },
            { "\\infty", "∞" },
            { "\\partial", "∂" },
            { "\\nabla", "∇" },
            { "\\leftarrow", "←" },
            { "\\rightarrow", "→" },
            { "\\uparrow", "↑" },
            { "\\downarrow", "↓" },
            { "\\leftrightarrow", "↔" },
            { "\\Leftarrow", "⇐" },
            { "\\Rightarrow", "⇒" },
            { "\\Leftrightarrow", "⇔" },
            { "\\exists", "∃" },
            { "\\forall", "∀" },
            { "\\in", "∈" },
            { "\\notin", "∉" },
            { "\\ni", "∋" },
            { "\\prod", "∏" },
            { "\\sum", "∑" },
            { "\\minus", "−" }
        };

        public static string Convert(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Szukamy wyrażeń matematycznych w formacie $$...$$ lub $...$
            // Najpierw obsługujemy bloki podwójnego dolara (display math)
            string result = Regex.Replace(input, @"\$\$(.*?)\$\$", m => FormatLatexExpression(m.Groups[1].Value), RegexOptions.Singleline);
            
            // Następnie pojedyncze dolary (inline math)
            result = Regex.Replace(result, @"\$(.*?)\$", m => FormatLatexExpression(m.Groups[1].Value), RegexOptions.Singleline);

            return result;
        }

        private static string FormatLatexExpression(string latex)
        {
            if (string.IsNullOrEmpty(latex))
                return string.Empty;

            string formatted = latex.Trim();

            // 1. Obsługa \text{...} -> usuwamy \text{ i klamrę zamykającą
            // Wykorzystujemy pętlę, by poradzić sobie z wieloma wystąpieniami \text
            while (true)
            {
                var match = Regex.Match(formatted, @"\\text\{([^{}]*)\}");
                if (!match.Success)
                    break;
                formatted = formatted.Replace(match.Value, match.Groups[1].Value);
            }

            // 2. Podmiana symboli matematycznych
            foreach (var sym in MathSymbols)
            {
                formatted = formatted.Replace(sym.Key, sym.Value);
            }

            // 3. Podmiana liter greckich
            foreach (var greek in GreekLetters)
            {
                formatted = formatted.Replace(greek.Key, greek.Value);
            }

            // 4. Obsługa ułamków \frac{a}{b} -> (a)/(b)
            while (true)
            {
                var match = Regex.Match(formatted, @"\\frac\{([^{}]+)\}\{([^{}]+)\}");
                if (!match.Success)
                    break;
                formatted = formatted.Replace(match.Value, $"({match.Groups[1].Value})/({match.Groups[2].Value})");
            }

            // 5. Obsługa pierwiastków \sqrt{a} -> √(a)
            while (true)
            {
                var match = Regex.Match(formatted, @"\\sqrt\{([^{}]+)\}");
                if (!match.Success)
                    break;
                formatted = formatted.Replace(match.Value, $"√({match.Groups[1].Value})");
            }

            // 6. Obsługa indeksów górnych z klamrami ^{...}
            while (true)
            {
                var match = Regex.Match(formatted, @"\^\{([^{}]+)\}");
                if (!match.Success)
                    break;
                string replacement = ConvertToSuperscript(match.Groups[1].Value);
                formatted = formatted.Replace(match.Value, replacement);
            }

            // 7. Obsługa indeksów górnych bez klamer ^x
            formatted = Regex.Replace(formatted, @"\^([0-9a-zA-Z+-])", m => ConvertToSuperscript(m.Groups[1].Value));

            // 8. Obsługa indeksów dolnych z klamrami _{...}
            while (true)
            {
                var match = Regex.Match(formatted, @"_\{([^{}]+)\}");
                if (!match.Success)
                    break;
                string replacement = ConvertToSubscript(match.Groups[1].Value);
                formatted = formatted.Replace(match.Value, replacement);
            }

            // 9. Obsługa indeksów dolnych bez klamer _x
            formatted = Regex.Replace(formatted, @"_([0-9a-zA-Z+-])", m => ConvertToSubscript(m.Groups[1].Value));

            // 10. Oczyszczanie białych znaków (np. \  lub \, na spację)
            formatted = formatted.Replace("\\ ", " ");
            formatted = formatted.Replace("\\,", " ");
            
            // 11. Usunięcie pozostałych backslashy przed popularnymi znakami (np. \{ -> {, \} -> })
            formatted = formatted.Replace("\\{", "{");
            formatted = formatted.Replace("\\}", "}");

            return formatted;
        }

        private static string ConvertToSuperscript(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (Superscripts.TryGetValue(c, out char sup))
                    sb.Append(sup);
                else
                    sb.Append(c); // Jeśli nie ma odpowiednika, zostawiamy znak bez zmian
            }
            return sb.ToString();
        }

        private static string ConvertToSubscript(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (Subscripts.TryGetValue(c, out char sub))
                    sb.Append(sub);
                else
                    sb.Append(c); // Poprawny fallback do oryginalnego znaku
            }
            return sb.ToString();
        }
    }
}
