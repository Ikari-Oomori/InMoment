using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InMoment.Application.Abstractions.Communication;
using InMoment.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Communication;

public sealed class FcmHttpV1PushSender : IPushSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly FirebaseAccessTokenProvider _tokenProvider;
    private readonly FirebasePushOptions _options;
    private readonly ILogger<FcmHttpV1PushSender> _logger;

    public FcmHttpV1PushSender(
        HttpClient httpClient,
        FirebaseAccessTokenProvider tokenProvider,
        IOptions<FirebasePushOptions> options,
        ILogger<FcmHttpV1PushSender> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(PushSendRequest request, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Firebase push disabled. Notification {Type} for user {UserId} skipped.",
                request.NotificationType,
                request.UserId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ProjectId))
            throw new InvalidOperationException("FirebasePush:ProjectId is not configured.");

        if (request.Targets.Count == 0)
            return;

        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);
        var endpoint = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";

        foreach (var target in request.Targets)
        {
            using var httpRequest = BuildHttpRequest(endpoint, accessToken, request, target);

            try
            {
                using var response = await _httpClient.SendAsync(httpRequest, ct);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "FCM push sent. UserId={UserId}, DeviceTokenId={DeviceTokenId}, Type={Type}",
                        request.UserId,
                        target.DeviceTokenId,
                        request.NotificationType);
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync(ct);

                _logger.LogWarning(
                    "FCM push failed. Status={StatusCode}, UserId={UserId}, DeviceTokenId={DeviceTokenId}, Type={Type}, Body={Body}",
                    (int)response.StatusCode,
                    request.UserId,
                    target.DeviceTokenId,
                    request.NotificationType,
                    responseBody);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "FCM push exception. UserId={UserId}, DeviceTokenId={DeviceTokenId}, Type={Type}",
                    request.UserId,
                    target.DeviceTokenId,
                    request.NotificationType);
            }
        }
    }

    private static HttpRequestMessage BuildHttpRequest(
        string endpoint,
        string accessToken,
        PushSendRequest request,
        PushSendTarget target)
    {
        var body = BuildMessageBody(request, target);

        var json = JsonSerializer.Serialize(body, JsonOptions);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return httpRequest;
    }

    private static object BuildMessageBody(PushSendRequest request, PushSendTarget target)
    {
        var data = request.Data.ToDictionary(x => x.Key, x => x.Value);

        data["pushPlatform"] = target.Platform.ToString();
        data["pushProvider"] = target.Provider.ToString();

        return new
        {
            message = new
            {
                token = target.Token,
                notification = new
                {
                    title = request.Title,
                    body = request.Body
                },
                data,
                android = new
                {
                    priority = "HIGH",
                    notification = new
                    {
                        channel_id = "inmoment_default",
                        sound = "default"
                    }
                },
                apns = new
                {
                    headers = new Dictionary<string, string>
                    {
                        ["apns-priority"] = "10"
                    },
                    payload = new Dictionary<string, object?>
                    {
                        ["aps"] = new Dictionary<string, object?>
                        {
                            ["sound"] = "default",
                            ["badge"] = 1,
                            ["content-available"] = 1
                        }
                    }
                }
            }
        };
    }
}