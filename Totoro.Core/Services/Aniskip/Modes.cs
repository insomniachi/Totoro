using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Refit;

namespace Totoro.Core.Services.Aniskip;

internal class PostVoteRequestBodyV2
{
    [JsonPropertyName("voteType")]
    [JsonConverter(typeof(JsonStringEnumConverterEx<VoteType>))]
    public VoteType VoteType { get; set; }
}

internal class PostVoteResponseV2
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

internal enum VoteType
{
    [EnumMember(Value = "upvote")]
    Upvote,

    [EnumMember(Value = "downvote")]
    Downvote
}

internal class PostCreateSkipTimeRequestBodyV2
{
    [JsonPropertyName("skipType")]
    [JsonConverter(typeof(JsonStringEnumConverterEx<SkipType>))]
    public SkipType SkipType { get; set; }

    [JsonPropertyName("providerName")]
    public string ProviderName { get; set; }

    [JsonPropertyName("startTime")]
    public double StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public double EndTime { get; set; }

    [JsonPropertyName("episodeLength")]
    public double EpisodeLength { get; set; }

    [JsonPropertyName("submitterId")]
    [JsonConverter(typeof(GuidJsonConverter))]
    public Guid SubmitterId { get; set; }
}

internal class PostCreateSkipTimeResponseV2
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("skipId")]
    [JsonConverter(typeof(GuidJsonConverter))]
    public Guid SkipId { get; set; }
}

internal class GetSkipTimesResponseV2
{
    [JsonPropertyName("status")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("found")]
    public bool IsFound { get; set; }

    [JsonPropertyName("results")]
    public SkipTime[] Results { get; set; }
}

internal class GetSkipTimesQueryV2
{
    [AliasAs("types[]")]
    [Query(CollectionFormat.Multi)]
    public SkipType[] Types { get; set; }

    [AliasAs("episodeLength")]
    public double EpisodeLength { get; set; }
}

internal class SkipTime
{
    [JsonPropertyName("interval")]
    public Interval Interval { get; set; }

    [JsonPropertyName("skipType")]
    [JsonConverter(typeof(JsonStringEnumConverterEx<SkipType>))]
    public SkipType SkipType { get; set; }

    [JsonPropertyName("skipId")]
    [JsonConverter(typeof(GuidJsonConverter))]
    public Guid SkipId { get; set; }

    [JsonPropertyName("episodeLength")]
    public double EpisodeLength { get; set; }
}

internal class Interval
{
    [JsonPropertyName("startTime")]
    public double StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public double EndTime { get; set; }
}

internal enum SkipType
{
    [EnumMember(Value = "op")]
    Opening,

    [EnumMember(Value = "ed")]
    Ending,

    [EnumMember(Value = "mixed-op")]
    MixedOpening,

    [EnumMember(Value = "mixed-ed")]
    MixedEnding,

    [EnumMember(Value = "recap")]
    Recap
}

internal class GuidJsonConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        _ = Guid.TryParse(reader.GetString(), out var guid);
        return guid;
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("D"));
    }
}

internal class JsonStringEnumConverterEx<T> : JsonConverter<T>
    where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString().FromEnumString<T>();
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToEnumString());
    }
}

public static class EnumExtensions
{
    public static string ToEnumString<TField>(this TField field)
        where TField : Enum
    {
        var fieldInfo = typeof(TField).GetField(field.ToString()) ?? throw new UnreachableException($"Field {nameof(field)} was not found.");

        var attributes = (EnumMemberAttribute[])fieldInfo.GetCustomAttributes(typeof(EnumMemberAttribute), false);
        if (attributes.Length == 0)
            throw new NotImplementedException($"The field has not been annotated with a {nameof(EnumMemberAttribute)}.");

        var value = attributes[0].Value ?? throw new NotImplementedException($"{nameof(EnumMemberAttribute)}.{nameof(EnumMemberAttribute.Value)} has not been set for this field.");

        return value;
    }

    public static TField FromEnumString<TField>(this string str)
        where TField : Enum
    {
        var fields = typeof(TField).GetFields();
        foreach (var field in fields)
        {
            var attributes = (EnumMemberAttribute[])field.GetCustomAttributes(typeof(EnumMemberAttribute), false);
            if (attributes.Length == 0)
                continue;

            var value = attributes[0].Value ?? throw new NotImplementedException($"{nameof(EnumMemberAttribute)}.{nameof(EnumMemberAttribute.Value)} has not been set for the field {field.Name}.");

            if (string.Equals(value, str, StringComparison.OrdinalIgnoreCase))
                return (TField)Enum.Parse(typeof(TField), field.Name) ?? throw new ArgumentNullException(field.Name);
        }

        throw new InvalidOperationException($"'{str}' was not found in enum {typeof(TField).Name}.");
    }
}


