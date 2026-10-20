using System;
using System.Text;

namespace Sirroh.Qol
{
    /// <summary>Pure, network-safe markup parser; never alters the ZDO string.</summary>
    internal static class SignMaterialMarkup
    {
        internal static string Strip(string text, out char variant)
        {
            variant = '\0';
            if (string.IsNullOrEmpty(text) || text.Length < 7) return text;
            StringBuilder output = null;
            int copyFrom = 0;
            for (int i = 0; i <= text.Length - 7; ++i)
            {
                if (text[i] != '<' ||
                    string.Compare(text, i, "<mat=", 0, 5, StringComparison.OrdinalIgnoreCase) != 0 ||
                    text[i + 6] != '>') continue;
                char v = char.ToUpperInvariant(text[i + 5]);
                if (v != 'B' && v != 'S' && v != 'G' && v != 'R' && v != 'Y') continue;
                if (output == null) output = new StringBuilder(text.Length);
                output.Append(text, copyFrom, i - copyFrom);
                variant = v; // Last recognized marker takes precedence.
                i += 6;
                copyFrom = i + 1;
            }
            if (output == null) return text; // Invalid tags are preserved verbatim.
            output.Append(text, copyFrom, text.Length - copyFrom);
            return output.ToString();
        }
    }
}
