using Microsoft.Extensions.Caching.Memory;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs;

namespace SeatCharter.Api.BusinessLogic.Services;

public class UserCacheService : IUserCacheService
{
    private static readonly MemoryCacheEntryOptions EntryOptions = new() { SlidingExpiration = TimeSpan.FromMinutes(30) };
    private readonly IMemoryCache _cache;

    public UserCacheService(IMemoryCache cache) => _cache = cache;

    private static string Key(string userId) => $"user:{userId}";

    public UserValidationData? Get(string userId) => _cache.TryGetValue(Key(userId), out UserValidationData? data) ? data : null;

    public void Set(UserValidationData data) => _cache.Set(Key(data.UserId), data, EntryOptions);

    public void Invalidate(string userId) => _cache.Remove(Key(userId));
}
