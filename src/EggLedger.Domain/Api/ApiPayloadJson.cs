using System.Text.Json;
using System.Text.Json.Serialization;

/*
JSON contract for the server-side decode transport; sync server and WASM client must use the SAME
options. Populate is required: protogen collection properties are get-only, so without it STJ
silently leaves every repeated field empty. Do NOT add IncludeFields (the __pbn__ backing fields
only bloat the payload).
*/

namespace EggLedger.Domain.Api;

/// <summary>JSON options shared by both ends of the decode transport. See file header.</summary>
public static class ApiPayloadJson {
    public static JsonSerializerOptions Options { get; } = new() {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };
}
