// Standardized to production level
// Purpose: Convert JSON numbers (0/1) or strings ("0"/"1"/"true"/"false") to C# bool during deserialization
// Dependencies: System.Text.Json

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Shared.Converters
{
    public class NumberToBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True) return true;
            if (reader.TokenType == JsonTokenType.False) return false;
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.TryGetInt64(out long l) ? l != 0 : reader.GetDouble() != 0;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                string? val = reader.GetString();
                if (bool.TryParse(val, out bool b)) return b;
                if (long.TryParse(val, out long l)) return l != 0;
            }
            return false;
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
