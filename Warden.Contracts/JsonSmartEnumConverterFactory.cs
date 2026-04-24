using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Contracts;

/// <summary>
/// A <see cref="JsonConverterFactory"/> that correctly routes enum serialisation
/// across three distinct policies used in this project:
///
/// <list type="bullet">
///   <item><b>Kebab-case</b> — enums annotated with
///     <c>[JsonConverter(typeof(JsonKebabCaseEnumConverter&lt;T&gt;))]</c>
///     (e.g. <see cref="Handshake.BlockReason"/>, <see cref="Handshake.AssertionKind"/>)</item>
///   <item><b>PascalCase / verbatim</b> — enums annotated with
///     <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c> and no naming policy
///     (e.g. <see cref="Telemetry.DominantDrive"/>)</item>
///   <item><b>CamelCase fallback</b> — all other enums with no attribute
///     (e.g. <see cref="Handshake.OutcomeCode"/>, <see cref="Telemetry.SpeciesType"/>)</item>
/// </list>
///
/// <b>Why this factory exists:</b> System.Text.Json applies converters registered in
/// <see cref="JsonSerializerOptions.Converters"/> with higher priority than a
/// <c>[JsonConverter]</c> attribute on the type itself.  A naïve global
/// <see cref="JsonStringEnumConverter"/> therefore swallows all enums before
/// their type-level attribute is consulted.  This factory reads the attribute
/// itself and delegates — giving type-level attributes effective precedence.
/// </summary>
internal sealed class JsonSmartEnumConverterFactory : JsonConverterFactory
{
    private static readonly JsonStringEnumConverter _camelCaseFallback =
        new(JsonNamingPolicy.CamelCase);

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsEnum;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // If the enum declares [JsonConverter], honour it
        var attr = typeToConvert.GetCustomAttribute<JsonConverterAttribute>(inherit: false);
        if (attr?.ConverterType is Type converterType)
        {
            var instance = (JsonConverter)Activator.CreateInstance(converterType)!;

            // STJ forbids a factory returning another factory from CreateConverter.
            // JsonStringEnumConverter is itself a factory, so we must unwrap it.
            if (instance is JsonConverterFactory nested)
                return nested.CreateConverter(typeToConvert, options);

            return instance;
        }

        // Default: camelCase string representation
        return _camelCaseFallback.CreateConverter(typeToConvert, options);
    }
}
