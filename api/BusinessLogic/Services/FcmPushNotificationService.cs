using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.BusinessLogic.Services;

/// <summary>FCM sender. Without credentials it logs and reports 0 delivered, so the rest of the flow still runs in dev.</summary>
public class FcmPushNotificationService : IPushNotificationService
{
    private readonly FirebaseMessaging? _messaging;
    private readonly ILogger<FcmPushNotificationService> _logger;

    public FcmPushNotificationService(FirebaseSettings settings, ILogger<FcmPushNotificationService> logger)
    {
        _logger = logger;
        var json = !string.IsNullOrWhiteSpace(settings.CredentialsJson) ? settings.CredentialsJson
                 : !string.IsNullOrWhiteSpace(settings.CredentialsPath) && File.Exists(settings.CredentialsPath) ? File.ReadAllText(settings.CredentialsPath)
                 : null;
        if (json is null)
        {
            _logger.LogWarning("FCM credentials not configured; push notifications are disabled");
            return;
        }

        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromJson(json)
        });
        _messaging = FirebaseMessaging.GetMessaging(app);
    }

    public bool IsConfigured => _messaging is not null;

    public async Task<int> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct = default)
    {
        if (messages.Count == 0) return 0;
        if (_messaging is null)
        {
            _logger.LogInformation("Push disabled; skipped {Count} message(s)", messages.Count);
            return 0;
        }

        var delivered = 0;
        foreach (var chunk in messages.Chunk(500))   // FCM batch limit
        {
            var batch = chunk.Select(m => new Message
            {
                Token = m.Token,
                Data = m.Data,
                Notification = new Notification { Title = m.Title, Body = m.Body },
                Android = new AndroidConfig { Priority = Priority.High }
            }).ToList();

            try
            {
                var response = await _messaging.SendEachAsync(batch, ct);
                delivered += response.SuccessCount;
                if (response.FailureCount > 0)
                    _logger.LogWarning("FCM rejected {FailureCount} of {Total} message(s)", response.FailureCount, batch.Count);
            }
            catch (FirebaseException ex)
            {
                _logger.LogError(ex, "FCM batch send failed");
            }
        }
        return delivered;
    }
}
