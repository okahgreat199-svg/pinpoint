using SeatCharter.Api.DTOs;

namespace SeatCharter.Api.BusinessLogic.Interfaces;

public interface IUserCacheService
{
    UserValidationData? Get(string userId);
    void Set(UserValidationData data);
    /// <summary>Called only by UserCacheInvalidationInterceptor. Don't call by hand.</summary>
    void Invalidate(string userId);
}
