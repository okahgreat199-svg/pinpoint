using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Data;
using SeatCharter.Api.Domain;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.DTOs.Requests.Devices;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.DTOs.Responses.Devices;
using SeatCharter.Api.Models;
using static SeatCharter.Api.Helpers.Enums;

namespace SeatCharter.Api.BusinessLogic.Services;

public class DeviceService : IDeviceService
{
    private readonly ApplicationDbContext _db;
    private readonly UserInfoToken _userInfo;
    private readonly TimeProvider _clock;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(ApplicationDbContext db, UserInfoToken userInfo, TimeProvider clock, ILogger<DeviceService> logger)
    {
        _db = db;
        _userInfo = userInfo;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private ApiResponseBase<T> Fail500<T>(Exception ex, [CallerMemberName] string method = "")
    {
        _logger.LogError(ex, "{Method} failed", method);
        return ApiResponseBase<T>.ServerError("An error occurred. Please try again.");
    }

    public async Task<ApiResponseBase<DeviceDto>> RegisterAsync(RegisterDeviceRequest request)
    {
        try
        {
            var userId = _userInfo.RequireUserId();
            var publicKey = request.PublicKey.Trim();
            var hardwareId = string.IsNullOrWhiteSpace(request.HardwareId) ? null : request.HardwareId.Trim();

            if (!DeviceSignature.IsValidPublicKey(publicKey))
                return ApiResponseBase<DeviceDto>.BadRequest("Public key must be a base64 ECDSA P-256 key.");

            // Same key re-registered by its owner (e.g. app restart): idempotent.
            var existing = await _db.Devices.FirstOrDefaultAsync(d =>
                d.UserId == userId && d.PublicKey == publicKey && d.Status != DeviceStatus.Revoked);
            if (existing is not null)
            {
                if (!string.IsNullOrWhiteSpace(request.PushToken)) existing.PushToken = request.PushToken;
                await _db.SaveChangesAsync();
                return ApiResponseBase<DeviceDto>.Ok(MapToDto(existing), $"Device already registered ({existing.Status}).");
            }

            // BR-14: a device belongs to one account at a time (matched by key or by OS install id).
            var boundElsewhere = await _db.Devices.AnyAsync(d =>
                d.UserId != userId && d.Status != DeviceStatus.Revoked
                && (d.PublicKey == publicKey || (hardwareId != null && d.HardwareId == hardwareId)));
            if (boundElsewhere)
                return ApiResponseBase<DeviceDto>.Conflict("This phone is already linked to another account. Ask an administrator to reset it.");

            var mine = await _db.Devices.Where(d => d.UserId == userId && d.Status != DeviceStatus.Revoked).ToListAsync();
            var hasActive = mine.Any(d => d.Status == DeviceStatus.Active);

            // BR-13: a student's first device is Active; later ones wait for a lecturer.
            // Staff devices aren't used for check-in, so they switch over immediately.
            var autoActivate = !_userInfo.IsStudent() || !hasActive;

            foreach (var old in mine.Where(d => autoActivate || d.Status == DeviceStatus.Pending))
            {
                old.Status = DeviceStatus.Revoked;   // one active + at most one pending request
                old.RevokedAt = Now;
            }

            var device = new Device
            {
                UserId = userId,
                PublicKey = publicKey,
                HardwareId = hardwareId,
                Platform = request.Platform.Trim(),
                Model = request.Model.Trim(),
                PushToken = request.PushToken,
                Status = autoActivate ? DeviceStatus.Active : DeviceStatus.Pending,
                BoundAt = Now,
                ApprovedAt = autoActivate ? Now : null
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Device {DeviceId} registered for {UserId} as {Status}", device.Id, userId, device.Status);
            return ApiResponseBase<DeviceDto>.Created(MapToDto(device), device.Status == DeviceStatus.Active
                ? "Device registered and active."
                : "Device registered. A lecturer must approve it before you can check in.");
        }
        catch (Exception ex) { return Fail500<DeviceDto>(ex); }
    }

    public async Task<ApiResponseBase<List<DeviceDto>>> GetMineAsync()
    {
        try
        {
            var userId = _userInfo.RequireUserId();
            var items = await _db.Devices.AsNoTracking()
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.BoundAt)
                .ToListAsync();
            return ApiResponseBase<List<DeviceDto>>.Ok(items.Select(MapToDto).ToList(), $"{items.Count} device(s) found.");
        }
        catch (Exception ex) { return Fail500<List<DeviceDto>>(ex); }
    }

    public async Task<ApiResponseBase<List<PendingDeviceDto>>> GetPendingAsync()
    {
        try
        {
            var query = _db.Devices.AsNoTracking().Where(d => d.Status == DeviceStatus.Pending);
            if (!_userInfo.IsAdmin())
            {
                var lecturerId = _userInfo.RequireUserId();
                query = query.Where(d => _db.Enrolments.Any(e => e.StudentId == d.UserId && e.Course.LecturerId == lecturerId));
            }

            var items = await query
                .OrderBy(d => d.BoundAt)
                .Select(d => new PendingDeviceDto
                {
                    Id = d.Id,
                    StudentId = d.UserId,
                    StudentName = d.User.FullName,
                    MatricNo = d.User.MatricNo,
                    Platform = d.Platform,
                    Model = d.Model,
                    BoundAt = d.BoundAt,
                    CurrentDeviceModel = _db.Devices
                        .Where(a => a.UserId == d.UserId && a.Status == DeviceStatus.Active)
                        .Select(a => a.Model)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return ApiResponseBase<List<PendingDeviceDto>>.Ok(items, $"{items.Count} pending device request(s).");
        }
        catch (Exception ex) { return Fail500<List<PendingDeviceDto>>(ex); }
    }

    public async Task<ApiResponseBase<DeviceDto>> ApproveAsync(string id)
    {
        try
        {
            var device = await FindReviewableAsync(id);
            if (device is null) return ApiResponseBase<DeviceDto>.NotFound("Device request not found.");
            if (device.Status != DeviceStatus.Pending)
                return ApiResponseBase<DeviceDto>.UnprocessableEntity($"Only pending devices can be approved. This one is {device.Status}.");

            await using var tx = await _db.Database.BeginTransactionAsync();

            var previous = await _db.Devices
                .Where(d => d.UserId == device.UserId && d.Status == DeviceStatus.Active)
                .ToListAsync();
            foreach (var old in previous)
            {
                old.Status = DeviceStatus.Revoked;
                old.RevokedAt = Now;
            }

            device.Status = DeviceStatus.Active;
            device.ApprovedById = _userInfo.RequireUserId();
            device.ApprovedAt = Now;

            Audit("DeviceApproved", device.Id, new { device.UserId, replaced = previous.Select(p => p.Id) });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponseBase<DeviceDto>.Ok(MapToDto(device), "Device approved.");
        }
        catch (Exception ex) { return Fail500<DeviceDto>(ex); }
    }

    public async Task<ApiResponseBase<DeviceDto>> RejectAsync(string id)
    {
        try
        {
            var device = await FindReviewableAsync(id);
            if (device is null) return ApiResponseBase<DeviceDto>.NotFound("Device request not found.");
            if (device.Status != DeviceStatus.Pending)
                return ApiResponseBase<DeviceDto>.UnprocessableEntity($"Only pending devices can be rejected. This one is {device.Status}.");

            device.Status = DeviceStatus.Revoked;
            device.RevokedAt = Now;
            Audit("DeviceRejected", device.Id, new { device.UserId });
            await _db.SaveChangesAsync();

            return ApiResponseBase<DeviceDto>.Ok(MapToDto(device), "Device request rejected.");
        }
        catch (Exception ex) { return Fail500<DeviceDto>(ex); }
    }

    public async Task<ApiResponseBase<DeviceDto>> UpdatePushTokenAsync(string id, UpdatePushTokenRequest request)
    {
        try
        {
            var userId = _userInfo.RequireUserId();
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
            if (device is null) return ApiResponseBase<DeviceDto>.NotFound("Device not found.");
            if (device.Status == DeviceStatus.Revoked)
                return ApiResponseBase<DeviceDto>.UnprocessableEntity("This device has been revoked.");

            device.PushToken = request.PushToken.Trim();
            await _db.SaveChangesAsync();
            return ApiResponseBase<DeviceDto>.Ok(MapToDto(device), "Push token updated.");
        }
        catch (Exception ex) { return Fail500<DeviceDto>(ex); }
    }

    public async Task<ApiResponseBase<int>> ResetUserDevicesAsync(string userId)
    {
        try
        {
            if (!await _db.Users.AnyAsync(u => u.Id == userId))
                return ApiResponseBase<int>.NotFound("User not found.");

            var devices = await _db.Devices.Where(d => d.UserId == userId && d.Status != DeviceStatus.Revoked).ToListAsync();
            foreach (var d in devices)
            {
                d.Status = DeviceStatus.Revoked;
                d.RevokedAt = Now;
            }
            Audit("DevicesReset", userId, new { revoked = devices.Select(d => d.Id) }, entityType: nameof(User));
            await _db.SaveChangesAsync();

            return ApiResponseBase<int>.Ok(devices.Count, $"{devices.Count} device(s) revoked. The next device this user registers will be active.");
        }
        catch (Exception ex) { return Fail500<int>(ex); }
    }

    // ── helpers ──

    /// <summary>Admins see every request; lecturers only those from students in their courses (else 404).</summary>
    private async Task<Device?> FindReviewableAsync(string id)
    {
        var query = _db.Devices.Where(d => d.Id == id);
        if (!_userInfo.IsAdmin())
        {
            var lecturerId = _userInfo.RequireUserId();
            query = query.Where(d => _db.Enrolments.Any(e => e.StudentId == d.UserId && e.Course.LecturerId == lecturerId));
        }
        return await query.FirstOrDefaultAsync();
    }

    private void Audit(string action, string entityId, object data, string entityType = nameof(Device))
        => _db.AuditLogs.Add(new AuditLog
        {
            ActorId = _userInfo.RequireUserId(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            At = Now,
            DataJson = JsonSerializer.Serialize(data)
        });

    private static DeviceDto MapToDto(Device d) => new()
    {
        Id = d.Id,
        UserId = d.UserId,
        Platform = d.Platform,
        Model = d.Model,
        Status = d.Status.ToString(),
        HasPushToken = !string.IsNullOrEmpty(d.PushToken),
        BoundAt = d.BoundAt,
        ApprovedAt = d.ApprovedAt,
        RevokedAt = d.RevokedAt
    };
}
