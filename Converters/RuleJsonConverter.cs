using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RulesEngine.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System;
using System.Collections.Generic;

namespace DemoRuleEngine.Converters;

public class RuleJsonConverter : ValueConverter<Rule, string>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null, // Match PascalCase property names
        Converters = { new JsonStringEnumConverter(), new RuleEnumerableConverter() }
    };

    public RuleJsonConverter()
        : base(
            v => Serialize(v),
            v => Deserialize(v))
    {
    }

    private static string Serialize(Rule rule)
    {
        if (rule is null)
            return string.Empty;

        // Serialize to an intermediate JsonNode to prune redundant properties
        var node = JsonSerializer.SerializeToNode(rule, JsonOptions);
        if (node is JsonObject obj)
        {
            // Prune fields managed by dedicated DB columns to avoid double-write desyncs
            obj.Remove("RuleName");
            obj.Remove("Enabled");
        }

        return node?.ToJsonString(JsonOptions) ?? string.Empty;
    }

    private static Rule Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Rule();

        return JsonSerializer.Deserialize<Rule>(json, JsonOptions) ?? new Rule();
    }

    // Handles inner recursive IEnumerable<Rule> arrays safely
    private class RuleEnumerableConverter : JsonConverter<IEnumerable<Rule>>
    {
        public override IEnumerable<Rule>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                return null;

            var rules = new List<Rule>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var rule = JsonSerializer.Deserialize<Rule>(ref reader, options);
                if (rule is not null)
                    rules.Add(rule);
            }
            return rules;
        }

        public override void Write(Utf8JsonWriter writer, IEnumerable<Rule> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var rule in value)
            {
                JsonSerializer.Serialize(writer, rule, options);
            }
            writer.WriteEndArray();
        }
    }
}
