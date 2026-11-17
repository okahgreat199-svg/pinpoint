using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.BusinessLogic.Services;
using SeatCharter.Api.Domain;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.Tests;

public class DeepLinkTests
{
    [Theory]
    [InlineData("https://seatcharter.example/c/AbC-_1", "AbC-_1")]
    [InlineData("https://other.host/c/AbC-_1/", "AbC-_1")]
    [InlineData("  AbC-_1  ", "AbC-_1")]
    [InlineData("123456", "123456")]
    [InlineData("https://seatcharter.example/x/AbC", "https://seatcharter.example/x/AbC")]
    public void ExtractToken_HandlesLinksBareTokensAndCodes(string scanned, string expected)
        => Assert.Equal(expected, QrToken.ExtractToken(scanned));

    [Fact]
    public void DeepLink_RoundTripsThroughValidation()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var link = QrToken.ToDeepLink("https://seatcharter.example/", QrToken.Create(id, QrToken.StepAt(now, 15), secret));

        Assert.StartsWith("https://seatcharter.example/c/", link);
        Assert.Equal(QrValidation.Valid, QrToken.Validate(QrToken.ExtractToken(link), id, secret, now, 15));
    }

    private sealed class NoJobs : IAttendanceJobService
    {
        public Task<(int Recipients, int Delivered)> SendPresencePromptAsync(Session s, CancellationToken ct = default) => Task.FromResult((0, 0));
        public Task CloseSessionAsync(Session s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> SendDuePromptsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> ProcessPromptTimeoutsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> AutoCloseExpiredSessionsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> PurgeExpiredLocationsAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("https://seatcharter.example", true)]
    public async Task GetQr_PayloadIsDeepLinkOnlyWhenConfigured(string baseUrl, bool expectLink)
    {
        var db = TestDb.Create();
        var clock = new FakeClock();
        var lecturer = db.AddUser(UserGroup.Lecturer, "lec");
        var session = db.AddOpenSession(db.AddCourse(lecturer), clock.GetUtcNow().UtcDateTime.AddMinutes(-1));
        await db.SaveChangesAsync();

        var service = new SessionService(db, TestDb.As(lecturer), new NoJobs(),
            new CampusService(db, new MemoryCache(new MemoryCacheOptions()), TestDb.Settings, NullLogger<CampusService>.Instance), TestDb.Settings,
            new DeepLinkSettings { PublicBaseUrl = baseUrl }, clock, NullLogger<SessionService>.Instance);
        var qr = (await service.GetQrAsync(session.Id)).ResponseData!;

        Assert.Equal(expectLink, qr.QrPayload.StartsWith("https://"));
        Assert.Equal(qr.Token, QrToken.ExtractToken(qr.QrPayload));
    }
}
