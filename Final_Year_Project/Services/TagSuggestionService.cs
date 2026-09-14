using System.Text.RegularExpressions;

namespace Final_Year_Project.Services
{
    public class TagSuggestionService
    {
        private readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the","and","or","a","an","of","in","on","for","to","is","are","from","with","by","that","this","it","as","at","me","my","i"
        };

        /// <summary>
        /// Normalize the description and return a list of candidate tokens / n-grams (1..3) in priority order.
        /// </summary>
        public IEnumerable<string> ExtractCandidates(string? description, int maxCandidates = 20)
        {
            if (string.IsNullOrWhiteSpace(description)) yield break;

            var desc = description.Trim();
            if (desc.Length < 2) yield break;

            // Replace non-alphanumeric chars with space
            var normalized = Regex.Replace(desc, @"[^a-zA-Z0-9 _-]+", " ");
            var tokens = normalized.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length >= 2 && !_stopWords.Contains(t))
                .ToList();

            if (!tokens.Any()) yield break;

            var candidates = new List<string>();

            for (int i = 0; i < tokens.Count; i++)
            {
                // unigram
                AddUnique(tokens[i]);

                // bigram
                if (i + 1 < tokens.Count) AddUnique(tokens[i] + " " + tokens[i + 1]);

                // trigram
                if (i + 2 < tokens.Count) AddUnique(tokens[i] + " " + tokens[i + 1] + " " + tokens[i + 2]);
                if (candidates.Count >= maxCandidates) break;
            }

            foreach (var c in candidates.Take(maxCandidates)) yield return c;

            void AddUnique(string v)
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                if (!candidates.Contains(v)) candidates.Add(v);
            }
        }
    }
}
