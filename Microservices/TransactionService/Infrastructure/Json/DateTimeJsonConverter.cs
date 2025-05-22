using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TransactionService.Infrastructure.Json
{
    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        private static readonly string[] _formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            "yyyy-MM-ddTHH:mm:ss.ffZ",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd",
            // Unix timestamp format handling will be done separately
        };

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string dateString = reader.GetString();

                // Try to parse using the specified formats
                if (DateTime.TryParseExact(dateString, _formats, CultureInfo.InvariantCulture, 
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result))
                {
                    return result;
                }
                
                // Try general parsing
                if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, 
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
                {
                    return result;
                }

                // It might be a Unix timestamp (seconds since epoch)
                if (long.TryParse(dateString, out long unixTimestamp))
                {
                    // Convert Unix timestamp to DateTime
                    return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
                }

                // Last resort: return current time rather than failing
                Console.WriteLine($"WARNING: Could not parse datetime string: '{dateString}', using current UTC time");
                return DateTime.UtcNow;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // Handle numeric timestamp (Unix timestamp in seconds or milliseconds)
                long timestamp = reader.GetInt64();
                
                // If timestamp is in milliseconds (13 digits), convert to seconds
                if (timestamp > 10000000000) // More than 10 billion means it's likely milliseconds
                {
                    timestamp /= 1000;
                }
                
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            }
            
            // Fall back to current time if nothing else works
            return DateTime.UtcNow;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // Write in ISO 8601 format
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
        }
    }
}