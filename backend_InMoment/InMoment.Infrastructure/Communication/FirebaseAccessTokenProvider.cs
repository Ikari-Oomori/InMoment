using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Communication;

public sealed class FirebaseAccessTokenProvider
{
    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/firebase.messaging"
    ];

    private readonly FirebasePushOptions _options;
    private GoogleCredential? _credential;
    private string? _cachedToken;
    private DateTimeOffset _cachedUntilUtc = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _sync = new(1, 1);

    public FirebaseAccessTokenProvider(IOptions<FirebasePushOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Firebase push is disabled.");

        if (string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath))
            throw new InvalidOperationException("FirebasePush:ServiceAccountJsonPath is not configured.");

        if (!File.Exists(_options.ServiceAccountJsonPath))
            throw new FileNotFoundException(
                $"Firebase service account file not found: {_options.ServiceAccountJsonPath}");

        if (!string.IsNullOrWhiteSpace(_cachedToken) &&
            DateTimeOffset.UtcNow < _cachedUntilUtc)
        {
            return _cachedToken;
        }

        await _sync.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) &&
                DateTimeOffset.UtcNow < _cachedUntilUtc)
            {
                return _cachedToken;
            }

            _credential ??= GoogleCredential
                .FromFile(_options.ServiceAccountJsonPath)
                .CreateScoped(Scopes);

            if (_credential.UnderlyingCredential is not ITokenAccess tokenAccess)
                throw new InvalidOperationException("Unable to obtain Google token access credential.");

            var token = await tokenAccess.GetAccessTokenForRequestAsync(null, ct);

            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Google access token is empty.");

            _cachedToken = token;
            _cachedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(50);

            return token;
        }
        finally
        {
            _sync.Release();
        }
    }
}