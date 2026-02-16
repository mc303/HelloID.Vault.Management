using System.Text.Json;

namespace HelloID.Vault.Services.Import.Utilities;

/// <summary>
/// Provides JSON element conversion utilities for import operations.
/// </summary>
public static class JsonElementHelper
{
    /// <summary>
    /// Converts a JsonElement to its string representation for storage.
    /// Handles strings, numbers, booleans, null, and nested objects/arrays.
    /// </summary>
    public static string GetJsonElementValue(object? value)
    {
        if (value == null)
            return "null";

        // When deserializing to Dictionary<string, object?>, JsonElements become JsonElement objects
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "null",
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Array or JsonValueKind.Object => element.GetRawText(),
                _ => element.ToString()
            };
        }

        return value.ToString() ?? "null";
    }
}
