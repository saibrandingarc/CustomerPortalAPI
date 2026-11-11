using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomerPortalAPI.Controllers
{
	public class NullableCustomDateTimeConverter : JsonConverter<DateTime?>
    {
        private const string DateFormat = "ddd, d MMM yyyy HH:mm:ss zzz";

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null; // Handle null or empty values
            }

            return DateTime.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteNullValue(); // Handle null values
            }
        }
    }
}

