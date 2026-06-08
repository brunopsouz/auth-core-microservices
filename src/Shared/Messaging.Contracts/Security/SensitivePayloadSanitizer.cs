using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shared.Messaging.Contracts.Security;

/// <summary>
/// Representa sanitizador de payloads e textos com dados sensíveis.
/// </summary>
public static partial class SensitivePayloadSanitizer
{
    private const string REDACTED_VALUE = "[REDACTED]";

    private static readonly HashSet<string> _sensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "confirmationCode",
        "password",
        "accessToken",
        "refreshToken",
        "sessionId"
    };

    /// <summary>
    /// Operação para sanitizar texto livre antes de logs, erros ou saídas administrativas.
    /// </summary>
    /// <param name="value">Texto a sanitizar.</param>
    /// <returns>Texto sanitizado.</returns>
    public static string SanitizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sanitized = SensitiveJsonStringValueRegex().Replace(value, $"$1\"{REDACTED_VALUE}\"");
        sanitized = SensitiveTextValueRegex().Replace(sanitized, $"$1{REDACTED_VALUE}");

        return SixDigitCodeRegex().Replace(sanitized, REDACTED_VALUE);
    }

    /// <summary>
    /// Operação para sanitizar payload JSON preservando sua estrutura.
    /// </summary>
    /// <param name="payload">Payload JSON a sanitizar.</param>
    /// <returns>Payload JSON sanitizado.</returns>
    public static string SanitizeJson(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(payload);
            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteSanitizedValue(writer, document.RootElement, propertyName: null);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return SanitizeText(payload);
        }
    }

    /// <summary>
    /// Operação para indicar se uma chave representa dado sensível.
    /// </summary>
    /// <param name="key">Chave a avaliar.</param>
    /// <returns>Verdadeiro quando a chave deve ser mascarada.</returns>
    public static bool IsSensitiveKey(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && _sensitiveKeys.Contains(key.Trim());
    }

    private static void WriteSanitizedValue(
        Utf8JsonWriter writer,
        JsonElement element,
        string? propertyName)
    {
        if (IsSensitiveKey(propertyName))
        {
            writer.WriteStringValue(REDACTED_VALUE);
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteSanitizedValue(writer, property.Value, property.Name);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteSanitizedValue(writer, item, propertyName: null);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(SanitizeText(element.GetString()));
                break;

            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                writer.WriteNullValue();
                break;
        }
    }

    [GeneratedRegex("(\"(?:confirmationCode|password|accessToken|refreshToken|sessionId)\"\\s*:\\s*)\"[^\"]*\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveJsonStringValueRegex();

    [GeneratedRegex("(\\b(?:confirmationCode|password|accessToken|refreshToken|sessionId)\\b\\s*[:=]\\s*)([^\\s,;]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveTextValueRegex();

    [GeneratedRegex("\\b\\d{6}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex SixDigitCodeRegex();
}
