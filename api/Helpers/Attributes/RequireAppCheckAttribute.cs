using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.Helpers.Attributes;

/// <summary>
/// Requires a valid Firebase App Check token in X-Firebase-AppCheck, i.e. the call came from our genuine app on a
/// genuine device (Play Integrity). No-op while AppCheck:Enabled is false. Pairs with device-key signing:
/// App Check proves the app, the signature proves the bound phone.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireAppCheckAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Firebase-AppCheck";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        if (!services.GetRequiredService<AppCheckSettings>().Enabled) return;

        var token = context.HttpContext.Request.Headers[HeaderName].ToString();
        var result = await services.GetRequiredService<IAppCheckVerifier>().VerifyAsync(token, context.HttpContext.RequestAborted);
        if (result.IsValid) return;

        services.GetRequiredService<ILogger<RequireAppCheckAttribute>>()
            .LogWarning("App Check rejected on {Path}: {Reason}", context.HttpContext.Request.Path, result.Error);
        context.Result = new ObjectResult(ApiResponseBase<object>.Unauthorized("This action must be performed from the Pinpoint app."))
            { StatusCode = StatusCodes.Status401Unauthorized };
    }
}
