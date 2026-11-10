namespace SeatCharter.Api.BusinessLogic.Interfaces;

public sealed record PushMessage(string Token, string Title, string Body, IReadOnlyDictionary<string, string> Data);

public interface IPushNotificationService
{
    bool IsConfigured { get; }
    /// <summary>Sends each message; returns how many FCM accepted. Never throws for per-token failures.</summary>
    Task<int> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct = default);
}
