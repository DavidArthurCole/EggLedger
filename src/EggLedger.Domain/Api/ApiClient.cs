using System.IO.Compression;
using Ei;
using EggLedger.Domain.Ei;
using ProtoBuf;

namespace EggLedger.Domain.Api;

/// <summary>
/// Port of Go api/request.go. Pure HTTP+protobuf client against the Egg Inc
/// API. POSTs base64(protobuf) as form field <c>data</c>, reads a base64 body,
/// decodes to protobuf. The contract is frozen; defaults match the Go source.
/// </summary>
public sealed partial class ApiClient
{
    /// <summary>Default game client version string. Must track the live game.</summary>
    public const string DefaultAppVersion = "1.35.7";

    /// <summary>Default game client build string. Must track the live game.</summary>
    public const string DefaultAppBuild = "111343";

    /// <summary>Default numeric client version. Must track the live game.</summary>
    public const uint DefaultClientVersion = 72;

    /// <summary>Default reported platform.</summary>
    public const Platform DefaultPlatform = Platform.Ios;

    /// <summary>Default API host prefix.</summary>
    public const string DefaultApiPrefix = "https://ctx-dot-auxbrainhome.appspot.com";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _client;

    /// <summary>Host prefix every endpoint is appended to.</summary>
    public string ApiPrefix { get; }

    /// <summary>Client version string sent in BasicRequestInfo.Version.</summary>
    public string AppVersion { get; }

    /// <summary>Client build string sent in BasicRequestInfo.Build.</summary>
    public string AppBuild { get; }

    /// <summary>Numeric client version sent in request messages.</summary>
    public uint ClientVersion { get; }

    /// <summary>Platform reported to the API.</summary>
    public Platform Platform { get; }

    /// <summary>
    /// Creates a client. Pass an <see cref="HttpClient"/> (or one built from a
    /// custom <see cref="HttpMessageHandler"/>) to stub the transport in tests.
    /// When null, a shared 5s-timeout client is used. Constants can be
    /// overridden but default exactly to the live-game values.
    /// </summary>
    public ApiClient(
        HttpClient? httpClient = null,
        string apiPrefix = DefaultApiPrefix,
        string appVersion = DefaultAppVersion,
        string appBuild = DefaultAppBuild,
        uint clientVersion = DefaultClientVersion,
        Platform platform = DefaultPlatform)
    {
        _client = httpClient ?? new HttpClient { Timeout = RequestTimeout };
        ApiPrefix = apiPrefix;
        AppVersion = appVersion;
        AppBuild = appBuild;
        ClientVersion = clientVersion;
        Platform = platform;
    }

    /// <summary>
    /// Builds a BasicRequestInfo. Platform is sent as its proto enum name
    /// (e.g. "IOS"), matching Go's Platform.String().
    /// </summary>
    public BasicRequestInfo NewBasicRequestInfo(string userId) => new()
    {
        EiUserId = userId,
        ClientVersion = ClientVersion,
        Version = AppVersion,
        Build = AppBuild,
        Platform = EnumNames.ProtoName(Platform),
    };

    /// <summary>
    /// Marshals the request, base64-encodes it, POSTs it as
    /// <c>application/x-www-form-urlencoded</c> with form key <c>data</c>, then
    /// base64-decodes the response body. Throws on non-2xx or transport errors.
    /// Returns the base64-decoded API response (the "raw payload").
    /// </summary>
    public async Task<byte[]> RequestRawPayloadAsync<TReq>(
        string endpoint,
        TReq reqMsg,
        CancellationToken cancellationToken = default)
    {
        string apiUrl = ApiPrefix + endpoint;

        byte[] reqBin;
        using (var buffer = new MemoryStream())
        {
            Serializer.Serialize(buffer, reqMsg);
            reqBin = buffer.ToArray();
        }

        string reqDataEncoded = Convert.ToBase64String(reqBin);
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("data", reqDataEncoded),
        });

        HttpResponseMessage resp;
        try
        {
            resp = await _client.PostAsync(apiUrl, form, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiRequestException($"POST {apiUrl}: timeout after {RequestTimeout}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new ApiRequestException($"POST {apiUrl}: interrupted");
        }
        catch (HttpRequestException ex)
        {
            throw new ApiRequestException($"POST {apiUrl}: {ex.Message}", ex);
        }

        using (resp)
        {
            string body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                throw new ApiRequestException(
                    $"POST {apiUrl}: HTTP {(int)resp.StatusCode}: {body}");
            }

            try
            {
                return Convert.FromBase64String(body);
            }
            catch (FormatException ex)
            {
                throw new ApiRequestException(
                    $"POST {apiUrl}: {body}: base64 decode error: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Decodes a raw payload into <typeparamref name="TMsg"/>. When
    /// <paramref name="authenticated"/>, the payload is an AuthenticatedMessage
    /// whose Message may be zlib-compressed (checked via Compressed). Mirrors Go
    /// DecodeAPIResponse, including the corrupted-UTF-8 error translation.
    /// </summary>
    public TMsg DecodeApiResponse<TMsg>(string apiUrl, byte[] payload, bool authenticated)
    {
        if (!authenticated)
        {
            try
            {
                return Deserialize<TMsg>(payload);
            }
            catch (Exception ex)
            {
                throw InterpretUnmarshalError(
                    $"unmarshaling {apiUrl} response: {ex.Message}", ex);
            }
        }

        AuthenticatedMessage authMsg;
        try
        {
            authMsg = Deserialize<AuthenticatedMessage>(payload);
        }
        catch (Exception ex)
        {
            throw InterpretUnmarshalError(
                $"unmarshaling {apiUrl} response as AuthenticatedMessage: {ex.Message}", ex);
        }

        byte[] msgBytes = authMsg.Message ?? Array.Empty<byte>();
        if (authMsg.Compressed)
        {
            try
            {
                msgBytes = ZlibInflate(msgBytes);
            }
            catch (Exception ex)
            {
                throw new ApiRequestException(
                    $"decompressing AuthenticatedMessage payload in {apiUrl}: {ex.Message}", ex);
            }
        }

        try
        {
            return Deserialize<TMsg>(msgBytes);
        }
        catch (Exception ex)
        {
            throw InterpretUnmarshalError(
                $"unmarshaling AuthenticatedMessage payload in {apiUrl} response: {ex.Message}", ex);
        }
    }

    private static TMsg Deserialize<TMsg>(byte[] data)
    {
        using var ms = new MemoryStream(data, writable: false);
        return Serializer.Deserialize<TMsg>(ms);
    }

    private static byte[] ZlibInflate(byte[] data)
    {
        using var input = new MemoryStream(data, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static ApiRequestException InterpretUnmarshalError(string message, Exception inner)
    {
        if (inner.Message.Contains("invalid UTF-8", StringComparison.OrdinalIgnoreCase) ||
            inner.Message.Contains("invalid UTF8", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiRequestException(
                "API returned corrupted data (invalid UTF-8 in one or more string fields); " +
                "this is a known issue affecting some players, and it can only be resolved when " +
                "Auxbrain fixes their server bug", inner);
        }
        return new ApiRequestException(message, inner);
    }
}

/// <summary>Error raised by <see cref="ApiClient"/> on transport or decode failure.</summary>
public sealed class ApiRequestException : Exception
{
    public ApiRequestException(string message) : base(message) { }

    public ApiRequestException(string message, Exception inner) : base(message, inner) { }
}
