using System.Text.Json;
using System.Text.Json.Serialization;

namespace EggLedger.Domain.Api;

/// <summary>
/// The single JSON contract for the server-side decode transport. The sync
/// server serializes the decoded protobuf-net response with these options; the
/// WASM client rehydrates with the SAME options.
///
/// Populate is required: the protogen-generated collection properties are
/// get-only (public List&lt;T&gt; X { get; } = new();). Without
/// PreferredObjectCreationHandling.Populate, STJ silently leaves every repeated
/// field empty on deserialize. Verified on the real eiafx-config.bin payload:
/// object -&gt; JSON -&gt; object -&gt; JSON is byte-identical with this setting.
/// Do NOT add IncludeFields; the public properties carry all data and the
/// __pbn__ backing fields only bloat the payload.
/// </summary>
public static class ApiPayloadJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };
}
