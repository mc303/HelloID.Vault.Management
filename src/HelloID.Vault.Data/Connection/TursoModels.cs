using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Represents a request to the Turso Hrana v2 pipeline endpoint.
/// </summary>
public class TursoPipelineRequest
{
    [JsonPropertyName("requests")]
    public List<TursoRequestItem> Requests { get; set; } = [];
}

/// <summary>
/// Represents a single request item in the pipeline.
/// </summary>
public class TursoRequestItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "execute";

    [JsonPropertyName("stmt")]
    public TursoStatement? Statement { get; set; }
}

/// <summary>
/// Represents a SQL statement with parameters.
/// </summary>
public class TursoStatement
{
    [JsonPropertyName("sql")]
    public string Sql { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public List<TursoValue> Args { get; set; } = [];

    [JsonPropertyName("want_rows")]
    public bool WantRows { get; set; } = true;
}

/// <summary>
/// Represents a typed value for Turso API.
/// </summary>
[JsonConverter(typeof(TursoValueConverter))]
public class TursoValue
{
    public object? Value { get; set; }
    public TursoValueType Type { get; set; }

    public static TursoValue Null() => new() { Type = TursoValueType.Null, Value = null };
    public static TursoValue Integer(long value) => new() { Type = TursoValueType.Integer, Value = value };
    public static TursoValue Float(double value) => new() { Type = TursoValueType.Float, Value = value };
    public static TursoValue Text(string value) => new() { Type = TursoValueType.Text, Value = value };
    public static TursoValue Blob(byte[] value) => new() { Type = TursoValueType.Blob, Value = value };
}

/// <summary>
/// Types of values in Turso API.
/// </summary>
public enum TursoValueType
{
    Null,
    Integer,
    Float,
    Text,
    Blob
}

/// <summary>
/// Response from Turso pipeline endpoint.
/// </summary>
public class TursoPipelineResponse
{
    [JsonPropertyName("results")]
    public List<TursoResultItem> Results { get; set; } = [];
}

/// <summary>
/// Represents a single result item from pipeline response.
/// </summary>
public class TursoResultItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public TursoResponse? Response { get; set; }

    [JsonPropertyName("error")]
    public TursoError? Error { get; set; }
}

/// <summary>
/// Response from executing a statement.
/// </summary>
public class TursoResponse
{
    [JsonPropertyName("result")]
    public TursoResult? Result { get; set; }
}

/// <summary>
/// Result from a query execution.
/// </summary>
public class TursoResult
{
    [JsonPropertyName("cols")]
    public List<TursoColumn> Columns { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<List<TursoResultValue>> Rows { get; set; } = [];

    [JsonPropertyName("affected_row_count")]
    public int AffectedRowCount { get; set; }

    [JsonPropertyName("last_insert_rowid")]
    public JsonElement LastInsertRowIdJson { get; set; }

    /// <summary>
    /// Gets the last insert row ID as a long? value.
    /// Handles both string and number JSON representations.
    /// </summary>
    [JsonIgnore]
    public long? LastInsertRowId
    {
        get
        {
            if (LastInsertRowIdJson.ValueKind == JsonValueKind.Null ||
                LastInsertRowIdJson.ValueKind == JsonValueKind.Undefined)
                return null;

            if (LastInsertRowIdJson.ValueKind == JsonValueKind.String)
            {
                var str = LastInsertRowIdJson.GetString();
                if (string.IsNullOrEmpty(str))
                    return null;
                return long.TryParse(str, out var result) ? result : null;
            }

            if (LastInsertRowIdJson.ValueKind == JsonValueKind.Number)
                return LastInsertRowIdJson.GetInt64();

            return null;
        }
    }

    [JsonPropertyName("replication_index")]
    public string? ReplicationIndex { get; set; }
}

/// <summary>
/// Column metadata.
/// </summary>
public class TursoColumn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("decltype")]
    public string? DeclType { get; set; }
}

/// <summary>
/// Value in a result row.
/// </summary>
public class TursoResultValue
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "null";

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

/// <summary>
/// Error response from Turso API.
/// </summary>
public class TursoError
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("stack")]
    public string? Stack { get; set; }
}

/// <summary>
/// Generic query result wrapper.
/// </summary>
/// <typeparam name="T">The type of entities returned.</typeparam>
public class TursoQueryResult<T>
{
    public List<T> Rows { get; set; } = [];
    public int AffectedRowCount { get; set; }
    public long? LastInsertRowId { get; set; }
    public bool Success { get; set; }
    public TursoError? Error { get; set; }
}

/// <summary>
/// Result of a transaction execution.
/// </summary>
public class TursoTransactionResult
{
    public bool Success { get; set; }
    public int TotalAffectedRows { get; set; }
    public List<TursoError> Errors { get; set; } = [];
}

/// <summary>
/// JSON converter for TursoValue.
/// </summary>
public class TursoValueConverter : JsonConverter<TursoValue>
{
    public override TursoValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return TursoValue.Null();
        }

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("type", out var typeProp))
        {
            var type = typeProp.GetString();
            var valueProp = root.TryGetProperty("value", out var v) ? v : default;

            return type switch
            {
                "integer" => TursoValue.Integer(valueProp.GetInt64()),
                "float" => TursoValue.Float(valueProp.GetDouble()),
                "text" => TursoValue.Text(valueProp.GetString() ?? string.Empty),
                "blob" => TursoValue.Blob(valueProp.GetBytesFromBase64()),
                _ => TursoValue.Null()
            };
        }

        return TursoValue.Null();
    }

    public override void Write(Utf8JsonWriter writer, TursoValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", value.Type switch
        {
            TursoValueType.Null => "null",
            TursoValueType.Integer => "integer",
            TursoValueType.Float => "float",
            TursoValueType.Text => "text",
            TursoValueType.Blob => "blob",
            _ => "null"
        });

        if (value.Type == TursoValueType.Null)
        {
            writer.WriteNull("value");
        }
        else if (value.Type == TursoValueType.Blob && value.Value is byte[] bytes)
        {
            writer.WriteString("value", Convert.ToBase64String(bytes));
        }
        else if (value.Type == TursoValueType.Integer && value.Value is long l)
        {
            writer.WriteString("value", l.ToString());
        }
        else if (value.Type == TursoValueType.Integer && value.Value is int i)
        {
            writer.WriteString("value", i.ToString());
        }
        else if (value.Type == TursoValueType.Float && value.Value is double d)
        {
            writer.WriteString("value", d.ToString(CultureInfo.InvariantCulture));
        }
        else if (value.Type == TursoValueType.Text && value.Value is string s)
        {
            writer.WriteString("value", s);
        }
        else if (value.Value != null)
        {
            writer.WriteString("value", value.Value.ToString());
        }
        else
        {
            writer.WriteNull("value");
        }

        writer.WriteEndObject();
    }
}
