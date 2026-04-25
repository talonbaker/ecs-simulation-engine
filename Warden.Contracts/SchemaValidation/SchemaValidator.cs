using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Warden.Contracts.SchemaValidation;

/// <summary>
/// Minimal Draft 2020-12 JSON Schema validator.
///
/// SUPPORTED KEYWORDS (validated)
/// ───────────────────────────────
/// type, required, properties, additionalProperties, items, enum, const,
/// minimum, maximum, minLength, maxLength, minItems, maxItems, maxProperties,
/// pattern, oneOf, allOf, if, then, $ref, $defs
///
/// IGNORED KEYWORDS (metadata, no validation effect)
/// ───────────────────────────────────────────────────
/// $schema, $id, title, description, format, default
///
/// UNSUPPORTED KEYWORDS
/// ─────────────────────
/// Any keyword not in the above two lists throws <see cref="NotSupportedException"/>
/// when the schema is first loaded (AT-05 requirement — fail at load, not at call).
///
/// EXTERNAL $ref
/// ─────────────
/// References to external schema files (e.g. <c>./sonnet-to-haiku.schema.json</c>)
/// are intentionally treated as "accept any value". The six embedded schemas are
/// the only schemas this validator loads.
/// </summary>
public static class SchemaValidator
{
    // ── Keyword registry ───────────────────────────────────────────────────────

    private static readonly HashSet<string> _validatingKeywords = new(StringComparer.Ordinal)
    {
        "type", "required", "properties", "additionalProperties", "items",
        "enum", "const", "minimum", "maximum", "minLength", "maxLength",
        "minItems", "maxItems", "maxProperties", "pattern",
        "oneOf", "allOf", "if", "then",
        "$ref", "$defs"
    };

    private static readonly HashSet<string> _ignoredKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "title", "description", "format", "default"
    };

    private static readonly HashSet<string> _allKnownKeywords =
        new(_validatingKeywords.Concat(_ignoredKeywords), StringComparer.Ordinal);

    // ── Schema cache ───────────────────────────────────────────────────────────

    private static readonly Dictionary<Schema, JsonDocument> _cache = new();
    private static readonly object _cacheLock = new();

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates <paramref name="json"/> against the embedded schema identified
    /// by <paramref name="schema"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown on first load if the schema contains an unsupported keyword.
    /// </exception>
    public static ValidationResult Validate(string json, Schema schema)
    {
        var schemaDoc = LoadSchema(schema);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            return new ValidationResult(false, new[] { $"JSON parse error: {ex.Message}" });
        }

        using (doc)
        {
            var errors = new List<string>();
            ValidateElement(doc.RootElement, schemaDoc.RootElement, schemaDoc.RootElement, "", errors);
            return errors.Count == 0
                ? ValidationResult.Ok
                : new ValidationResult(false, errors.AsReadOnly());
        }
    }

    /// <summary>
    /// Validates <paramref name="json"/> against a raw schema JSON string.
    /// Scans for unsupported keywords and throws <see cref="NotSupportedException"/>
    /// before any validation occurs. Use in tests to exercise the keyword guard.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown if the schema contains a keyword not in the known set.
    /// </exception>
    public static ValidationResult ValidateWithSchema(string json, string schemaJson)
    {
        JsonDocument schemaDoc;
        try { schemaDoc = JsonDocument.Parse(schemaJson); }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Schema JSON is invalid: {ex.Message}", nameof(schemaJson));
        }

        using (schemaDoc)
        {
            ScanSchemaObject(schemaDoc.RootElement, "$");

            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch (JsonException ex)
            {
                return new ValidationResult(false, new[] { $"JSON parse error: {ex.Message}" });
            }

            using (doc)
            {
                var errors = new List<string>();
                ValidateElement(doc.RootElement, schemaDoc.RootElement, schemaDoc.RootElement, "", errors);
                return errors.Count == 0
                    ? ValidationResult.Ok
                    : new ValidationResult(false, errors.AsReadOnly());
            }
        }
    }

    // ── Schema loading + keyword scan ──────────────────────────────────────────

    private static JsonDocument LoadSchema(Schema schema)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(schema, out var cached))
                return cached;

            var resourceName = SchemaResourceName(schema);
            var asm          = typeof(SchemaValidator).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. " +
                    $"Available: {string.Join(", ", asm.GetManifestResourceNames())}");

            using var reader = new StreamReader(stream);
            var schemaJson   = reader.ReadToEnd();
            var doc          = JsonDocument.Parse(schemaJson);

            // AT-05: scan for unsupported keywords at load time
            ScanSchemaObject(doc.RootElement, "$");

            _cache[schema] = doc;
            return doc;
        }
    }

    private static string SchemaResourceName(Schema schema) => schema switch
    {
        Schema.WorldState    => "Warden.Contracts.SchemaValidation.world-state.schema.json",
        Schema.OpusToSonnet  => "Warden.Contracts.SchemaValidation.opus-to-sonnet.schema.json",
        Schema.SonnetResult  => "Warden.Contracts.SchemaValidation.sonnet-result.schema.json",
        Schema.SonnetToHaiku => "Warden.Contracts.SchemaValidation.sonnet-to-haiku.schema.json",
        Schema.HaikuResult   => "Warden.Contracts.SchemaValidation.haiku-result.schema.json",
        Schema.AiCommandBatch  => "Warden.Contracts.SchemaValidation.ai-command-batch.schema.json",
        Schema.WorldDefinition => "Warden.Contracts.SchemaValidation.world-definition.schema.json",
        _                      => throw new ArgumentOutOfRangeException(nameof(schema))
    };

    /// <summary>
    /// Recursively walks a schema object and throws <see cref="NotSupportedException"/>
    /// for any property name that is not in the known keyword set.
    /// Only JSON objects whose properties are schema keywords are scanned;
    /// user-defined property containers (the value of "properties", "$defs")
    /// are traversed into their VALUES (which are schema objects), not their keys.
    /// </summary>
    private static void ScanSchemaObject(JsonElement schemaObj, string path)
    {
        if (schemaObj.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in schemaObj.EnumerateObject())
        {
            if (!_allKnownKeywords.Contains(prop.Name))
                throw new NotSupportedException(
                    $"Schema keyword '{prop.Name}' at path '{path}' is not supported by " +
                    $"{nameof(SchemaValidator)}. Add it to the supported set or remove it from the schema.");

            switch (prop.Name)
            {
                case "properties":
                    // Keys are user-defined property names; recurse into values as schemas
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                        foreach (var namedProp in prop.Value.EnumerateObject())
                            ScanSchemaObject(namedProp.Value, $"{path}.properties.{namedProp.Name}");
                    break;

                case "$defs":
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                        foreach (var def in prop.Value.EnumerateObject())
                            ScanSchemaObject(def.Value, $"{path}.$defs.{def.Name}");
                    break;

                case "items":
                    ScanSchemaObject(prop.Value, $"{path}.items");
                    break;

                case "additionalProperties":
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                        ScanSchemaObject(prop.Value, $"{path}.additionalProperties");
                    break;

                case "oneOf":
                case "allOf":
                {
                    if (prop.Value.ValueKind != JsonValueKind.Array) break;
                    int i = 0;
                    foreach (var sub in prop.Value.EnumerateArray())
                        ScanSchemaObject(sub, $"{path}.{prop.Name}[{i++}]");
                    break;
                }

                case "if":
                case "then":
                    ScanSchemaObject(prop.Value, $"{path}.{prop.Name}");
                    break;
            }
        }
    }

    // ── Core validator ─────────────────────────────────────────────────────────

    private static void ValidateElement(
        JsonElement value,
        JsonElement schema,
        JsonElement rootSchema,
        string      path,
        List<string> errors)
    {
        // $ref takes precedence — resolve and re-validate
        if (schema.TryGetProperty("$ref", out var refVal))
        {
            var refStr = refVal.GetString() ?? string.Empty;
            if (refStr.StartsWith("#/", StringComparison.Ordinal))
            {
                var resolved = ResolveLocalRef(refStr, rootSchema);
                ValidateElement(value, resolved, rootSchema, path, errors);
            }
            // External ref — accept any value (not resolvable in embedded validator)
            return;
        }

        foreach (var kw in schema.EnumerateObject())
        {
            switch (kw.Name)
            {
                case "type":
                    CheckType(value, kw.Value, path, errors);
                    break;

                case "required":
                    CheckRequired(value, kw.Value, path, errors);
                    break;

                case "properties":
                    if (value.ValueKind == JsonValueKind.Object)
                        CheckProperties(value, kw.Value, rootSchema, path, errors);
                    break;

                case "additionalProperties":
                    if (value.ValueKind == JsonValueKind.Object)
                    {
                        var definedKeys = GetDefinedPropertyKeys(schema);
                        CheckAdditionalProperties(value, definedKeys, kw.Value, rootSchema, path, errors);
                    }
                    break;

                case "items":
                    if (value.ValueKind == JsonValueKind.Array)
                        CheckItems(value, kw.Value, rootSchema, path, errors);
                    break;

                case "enum":
                    CheckEnum(value, kw.Value, path, errors);
                    break;

                case "const":
                    CheckConst(value, kw.Value, path, errors);
                    break;

                case "minimum":
                    CheckMinimum(value, kw.Value, path, errors);
                    break;

                case "maximum":
                    CheckMaximum(value, kw.Value, path, errors);
                    break;

                case "minLength":
                    CheckMinLength(value, kw.Value, path, errors);
                    break;

                case "maxLength":
                    CheckMaxLength(value, kw.Value, path, errors);
                    break;

                case "minItems":
                    CheckMinItems(value, kw.Value, path, errors);
                    break;

                case "maxItems":
                    CheckMaxItems(value, kw.Value, path, errors);
                    break;

                case "maxProperties":
                    CheckMaxProperties(value, kw.Value, path, errors);
                    break;

                case "pattern":
                    CheckPattern(value, kw.Value, path, errors);
                    break;

                case "oneOf":
                    CheckOneOf(value, kw.Value, rootSchema, path, errors);
                    break;

                case "allOf":
                    CheckAllOf(value, kw.Value, rootSchema, path, errors);
                    break;

                case "then":
                    // Processed together with "if" below
                    CheckIfThen(value, schema, rootSchema, path, errors);
                    break;

                // Handled above or ignored
                case "if":
                case "$ref":
                case "$defs":
                case "$schema": case "$id": case "title":
                case "description": case "format": case "default":
                    break;
            }
        }
    }

    // ── Keyword implementations ────────────────────────────────────────────────

    private static void CheckType(JsonElement value, JsonElement typeSchema, string path, List<string> errors)
    {
        // "type" may be a string or an array of strings
        if (typeSchema.ValueKind == JsonValueKind.String)
        {
            if (!MatchesType(value, typeSchema.GetString()!))
                errors.Add($"{path}: expected type '{typeSchema.GetString()}', got {JsonKindName(value)}.");
        }
        else if (typeSchema.ValueKind == JsonValueKind.Array)
        {
            bool anyMatch = false;
            foreach (var t in typeSchema.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String && MatchesType(value, t.GetString()!))
                { anyMatch = true; break; }
            if (!anyMatch)
                errors.Add($"{path}: value does not match any of the allowed types.");
        }
    }

    private static bool MatchesType(JsonElement value, string typeName) => typeName switch
    {
        "string"  => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number"  => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object"  => value.ValueKind == JsonValueKind.Object,
        "array"   => value.ValueKind == JsonValueKind.Array,
        "null"    => value.ValueKind == JsonValueKind.Null,
        _         => false
    };

    private static void CheckRequired(JsonElement value, JsonElement requiredSchema, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Object) return;
        foreach (var req in requiredSchema.EnumerateArray())
        {
            var name = req.GetString()!;
            if (!value.TryGetProperty(name, out _))
                errors.Add($"{path}: required property '{name}' is missing.");
        }
    }

    private static void CheckProperties(
        JsonElement value, JsonElement propsSchema,
        JsonElement rootSchema, string path, List<string> errors)
    {
        foreach (var propDef in propsSchema.EnumerateObject())
        {
            if (value.TryGetProperty(propDef.Name, out var propValue))
                ValidateElement(propValue, propDef.Value, rootSchema, $"{path}.{propDef.Name}", errors);
        }
    }

    private static HashSet<string> GetDefinedPropertyKeys(JsonElement schema)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("properties", out var props) &&
            props.ValueKind == JsonValueKind.Object)
            foreach (var p in props.EnumerateObject())
                keys.Add(p.Name);
        return keys;
    }

    private static void CheckAdditionalProperties(
        JsonElement value, HashSet<string> definedKeys,
        JsonElement additionalSchema, JsonElement rootSchema,
        string path, List<string> errors)
    {
        if (additionalSchema.ValueKind == JsonValueKind.False)
        {
            foreach (var prop in value.EnumerateObject())
                if (!definedKeys.Contains(prop.Name))
                    errors.Add($"{path}: property '{prop.Name}' is not allowed (additionalProperties: false).");
        }
        else if (additionalSchema.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in value.EnumerateObject())
                if (!definedKeys.Contains(prop.Name))
                    ValidateElement(prop.Value, additionalSchema, rootSchema, $"{path}.{prop.Name}", errors);
        }
    }

    private static void CheckItems(
        JsonElement value, JsonElement itemsSchema,
        JsonElement rootSchema, string path, List<string> errors)
    {
        int i = 0;
        foreach (var item in value.EnumerateArray())
            ValidateElement(item, itemsSchema, rootSchema, $"{path}[{i++}]", errors);
    }

    private static void CheckEnum(JsonElement value, JsonElement enumSchema, string path, List<string> errors)
    {
        foreach (var allowed in enumSchema.EnumerateArray())
            if (JsonElementsEqual(value, allowed))
                return;
        errors.Add($"{path}: value '{value}' is not in the allowed enum values.");
    }

    private static void CheckConst(JsonElement value, JsonElement constSchema, string path, List<string> errors)
    {
        if (!JsonElementsEqual(value, constSchema))
            errors.Add($"{path}: expected constant value '{constSchema}', got '{value}'.");
    }

    private static void CheckMinimum(JsonElement value, JsonElement min, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Number) return;
        if (value.GetDouble() < min.GetDouble())
            errors.Add($"{path}: value {value.GetDouble()} is less than minimum {min.GetDouble()}.");
    }

    private static void CheckMaximum(JsonElement value, JsonElement max, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Number) return;
        if (value.GetDouble() > max.GetDouble())
            errors.Add($"{path}: value {value.GetDouble()} exceeds maximum {max.GetDouble()}.");
    }

    private static void CheckMinLength(JsonElement value, JsonElement min, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.String) return;
        var len = value.GetString()!.Length;
        if (len < min.GetInt32())
            errors.Add($"{path}: string length {len} is less than minLength {min.GetInt32()}.");
    }

    private static void CheckMaxLength(JsonElement value, JsonElement max, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.String) return;
        var len = value.GetString()!.Length;
        if (len > max.GetInt32())
            errors.Add($"{path}: string length {len} exceeds maxLength {max.GetInt32()}.");
    }

    private static void CheckMinItems(JsonElement value, JsonElement min, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Array) return;
        int count = value.GetArrayLength();
        if (count < min.GetInt32())
            errors.Add($"{path}: array has {count} item(s), minimum is {min.GetInt32()}.");
    }

    private static void CheckMaxItems(JsonElement value, JsonElement max, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Array) return;
        int count = value.GetArrayLength();
        if (count > max.GetInt32())
            errors.Add($"{path}: array has {count} item(s), maximum is {max.GetInt32()}.");
    }

    private static void CheckMaxProperties(JsonElement value, JsonElement max, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Object) return;
        int count = 0;
        foreach (var _ in value.EnumerateObject()) count++;
        if (count > max.GetInt32())
            errors.Add($"{path}: object has {count} properties, maximum is {max.GetInt32()}.");
    }

    private static void CheckPattern(JsonElement value, JsonElement pattern, string path, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.String) return;
        var str   = value.GetString()!;
        var regex = pattern.GetString()!;
        if (!Regex.IsMatch(str, regex))
            errors.Add($"{path}: value '{str}' does not match pattern '{regex}'.");
    }

    private static void CheckOneOf(
        JsonElement value, JsonElement oneOfArray,
        JsonElement rootSchema, string path, List<string> errors)
    {
        int matchCount = 0;
        foreach (var sub in oneOfArray.EnumerateArray())
        {
            var subErrors = new List<string>();
            ValidateElement(value, sub, rootSchema, path, subErrors);
            if (subErrors.Count == 0) matchCount++;
        }
        if (matchCount != 1)
            errors.Add($"{path}: value must match exactly one of the oneOf schemas (matched {matchCount}).");
    }

    private static void CheckAllOf(
        JsonElement value, JsonElement allOfArray,
        JsonElement rootSchema, string path, List<string> errors)
    {
        foreach (var sub in allOfArray.EnumerateArray())
            ValidateElement(value, sub, rootSchema, path, errors);
    }

    private static void CheckIfThen(
        JsonElement value, JsonElement schema,
        JsonElement rootSchema, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("if",   out var ifSchema)   ||
            !schema.TryGetProperty("then", out var thenSchema))
            return;

        var ifErrors = new List<string>();
        ValidateElement(value, ifSchema, rootSchema, path, ifErrors);

        if (ifErrors.Count == 0)
            ValidateElement(value, thenSchema, rootSchema, path, errors);
    }

    // ── $ref resolution ────────────────────────────────────────────────────────

    private static JsonElement ResolveLocalRef(string reference, JsonElement root)
    {
        // "#/$defs/entityState" → ["$defs", "entityState"]
        var pointer = reference.Substring(2); // strip "#/"
        var parts   = pointer.Split('/');
        var current = root;

        foreach (var part in parts)
        {
            var decoded = part.Replace("~1", "/").Replace("~0", "~");
            if (!current.TryGetProperty(decoded, out current))
                throw new InvalidOperationException(
                    $"Cannot resolve local $ref '{reference}': segment '{decoded}' not found.");
        }
        return current;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        return a.ValueKind switch
        {
            JsonValueKind.String  => a.GetString() == b.GetString(),
            JsonValueKind.Number  => a.GetDouble() == b.GetDouble(),
            JsonValueKind.True    => true,
            JsonValueKind.False   => true,
            JsonValueKind.Null    => true,
            _                     => a.GetRawText() == b.GetRawText()
        };
    }

    private static string JsonKindName(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String  => "string",
        JsonValueKind.Number  => "number",
        JsonValueKind.True    => "boolean",
        JsonValueKind.False   => "boolean",
        JsonValueKind.Object  => "object",
        JsonValueKind.Array   => "array",
        JsonValueKind.Null    => "null",
        _                     => "undefined"
    };
}
