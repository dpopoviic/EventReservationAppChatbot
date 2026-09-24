using System.Text;

namespace EventReservationApp.Helpers
{
    /// <summary>
    /// Normalizuje tekst za poredjenje: cirilicu prevodi u latinicu,
    /// c/c/s/z/dj bez kvacica i sve prebacuje u mala slova.
    /// Tako se "Самит", "SUMMIT" i "Summit" medjusobno poklapaju.
    /// </summary>
    public static class TextNormalizer
    {
        private static readonly Dictionary<char, string> Map = new()
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['ђ'] = "dj",
            ['е'] = "e", ['ж'] = "z", ['з'] = "z", ['и'] = "i", ['ј'] = "j", ['к'] = "k",
            ['л'] = "l", ['љ'] = "lj", ['м'] = "m", ['н'] = "n", ['њ'] = "nj", ['о'] = "o",
            ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['ћ'] = "c", ['у'] = "u",
            ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "c", ['џ'] = "dz", ['ш'] = "s",
            ['č'] = "c", ['ć'] = "c", ['š'] = "s", ['ž'] = "z", ['đ'] = "dj",
        };

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value.Trim().ToLowerInvariant())
            {
                if (Map.TryGetValue(ch, out var replacement))
                    sb.Append(replacement);
                else
                    sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
