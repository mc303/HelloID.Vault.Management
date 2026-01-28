namespace HelloID.Vault.Services.Import.Utilities;

/// <summary>
/// Provides utility methods for string formatting and transformation.
/// </summary>
public static class StringFormatter
{
    /// <summary>
    /// Converts snake_case or camelCase to Title Case with spaces.
    /// Examples:
    ///   ip_phone_number -> IP Phone Number
    ///   mobilePhoneNumber -> Mobile Phone Number
    /// </summary>
    public static string FormatDisplayName(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return string.Empty;

        var words = new List<string>();
        var currentWord = "";

        for (int i = 0; i < fieldKey.Length; i++)
        {
            var c = fieldKey[i];

            if (c == '_' || c == '-')
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord);
                    currentWord = "";
                }
            }
            else if (char.IsUpper(c) && currentWord.Length > 0)
            {
                words.Add(currentWord);
                currentWord = c.ToString();
            }
            else
            {
                currentWord += c;
            }
        }

        if (currentWord.Length > 0)
        {
            words.Add(currentWord);
        }

        // Capitalize each word
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Length == 1)
            {
                // Single letter words stay uppercase (like "I" in "IP")
                words[i] = words[i].ToUpper();
            }
            else
            {
                // Regular words: capitalize first letter
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }

        return string.Join(" ", words);
    }
}
