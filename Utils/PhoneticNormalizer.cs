using System;
using System.Collections.Generic;

namespace LivingCompanionsValley.Utils
{
    public static class PhoneticNormalizer
    {
        public static string NormalizeTranscript(string rawTranscript, List<string> knownNames)
        {
            if (string.IsNullOrWhiteSpace(rawTranscript))
                return rawTranscript;

            if (knownNames == null || knownNames.Count == 0)
                return rawTranscript;

            string[] words = rawTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool modified = false;

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];

                // Remove punctuation attached to the word
                string cleanWord = word;
                char lastChar = word[^1];
                bool hasPunctuation = char.IsPunctuation(lastChar);

                if (hasPunctuation)
                {
                    cleanWord = word.Substring(0, word.Length - 1);
                }

                if (cleanWord.Length > 3)
                {
                    string? bestMatch = null;
                    int bestDistance = int.MaxValue;

                    foreach (var name in knownNames)
                    {
                        int distance = CalculateLevenshteinDistance(cleanWord.ToLowerInvariant(), name.ToLowerInvariant());
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestMatch = name;
                        }
                    }

                    // Tolerance rule based on word length
                    int maxTolerance = cleanWord.Length <= 5 ? 1 : 2;

                    if (bestMatch != null && bestDistance > 0 && bestDistance <= maxTolerance)
                    {
                        words[i] = hasPunctuation ? bestMatch + lastChar : bestMatch;
                        modified = true;
                    }
                }
            }

            return modified ? string.Join(" ", words) : rawTranscript;
        }

        private static int CalculateLevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int n = a.Length;
            int m = b.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }
    }
}
