using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SeatCharter.Api.DTOs.Responses;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.Helpers.Jwt;

public static class JwtAuthenticationConfigurationExtension
{
    private static readonly JsonSerializerOptions EnvelopeJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void AddJwtAuthenticationConfiguration(this IServiceCollection services, JwtSettings settings)
    {
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultScheme             = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                ValidateIssuer = true,  ValidIssuer = settings.Issuer,
                ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateLifetime = true, RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
                NameClaimType = "sub",
                RoleClaimType = "users_group"
            };
            o.Events = new JwtBearerEvents
            {
                // Empty-body 401 → envelope. Distinguish expired vs missing.
                OnChallenge = async ctx =>
                {
                    ctx.HandleResponse();
                    if (ctx.Response.HasStarted) return;
                    var msg = ctx.AuthenticateFailure is SecurityTokenExpiredException
                        ? "Token expired. Please sign in again."
                        : !string.IsNullOrEmpty(ctx.Error) ? $"Authentication failed: {ctx.Error}" : "Authentication required.";
                    ctx.Response.StatusCode = 401;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(ApiResponseBase<object>.Unauthorized(msg), EnvelopeJson));
                },
                OnForbidden = async ctx =>
                {
                    if (ctx.Response.HasStarted) return;
                    ctx.Response.StatusCode = 403;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                        ApiResponseBase<object>.Forbidden("You do not have permission to access this resource."), EnvelopeJson));
                }
            };
        });
    }
}
