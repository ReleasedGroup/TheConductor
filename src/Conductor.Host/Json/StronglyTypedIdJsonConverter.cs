using System.Text.Json;
using System.Text.Json.Serialization;
using Conductor.Core.Domain.Ids;

namespace Conductor.Host.Json;

internal delegate bool TryParseStronglyTypedId<TId>(string? value, out TId id)
    where TId : struct;

internal sealed class StronglyTypedIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct
{
    private readonly TryParseStronglyTypedId<TId> tryParse;

    public StronglyTypedIdJsonConverter(TryParseStronglyTypedId<TId> tryParse)
    {
        this.tryParse = tryParse;
    }

    public override TId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"{typeToConvert.Name} must be a GUID string.");
        }

        string? value = reader.GetString();
        if (tryParse(value, out TId id))
        {
            return id;
        }

        throw new JsonException($"{typeToConvert.Name} must be a non-empty GUID string.");
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

internal static class StronglyTypedIdJsonConverters
{
    public static void AddTo(JsonSerializerOptions options)
    {
        options.Converters.Add(new StronglyTypedIdJsonConverter<ProjectId>(ProjectId.TryParse));
        options.Converters.Add(new StronglyTypedIdJsonConverter<WorkflowProfileId>(WorkflowProfileId.TryParse));
        options.Converters.Add(new StronglyTypedIdJsonConverter<SecretId>(SecretId.TryParse));
    }
}
