using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.Controllers;

/// <summary>
/// Web-facing endpoints for deep-link QR codes. Not part of the JSON API, so (like file downloads) they don't use the envelope.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class DeepLinkController : ControllerBase
{
    private readonly DeepLinkSettings _settings;

    public DeepLinkController(DeepLinkSettings settings) => _settings = settings;

    /// <summary>GET /.well-known/assetlinks.json — lets Android open https://host/c/... links straight in the app</summary>
    [HttpGet("/.well-known/assetlinks.json")]
    public IActionResult AssetLinks()
    {
        if (string.IsNullOrWhiteSpace(_settings.AndroidPackageName) || _settings.AndroidCertFingerprints.Length == 0)
            return NotFound();

        return new JsonResult(new[]
        {
            new
            {
                relation = new[] { "delegate_permission/common.handle_all_urls" },
                target = new
                {
                    @namespace = "android_app",
                    package_name = _settings.AndroidPackageName,
                    sha256_cert_fingerprints = _settings.AndroidCertFingerprints
                }
            }
        });
    }

    /// <summary>GET /c/{token} — fallback page when a QR is scanned without the app installed</summary>
    [HttpGet("/c/{token}")]
    public IActionResult Landing(string token)
    {
        // The token is in the URL: don't echo it, cache it, or leak it via Referer.
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";

        var download = string.IsNullOrWhiteSpace(_settings.AppDownloadUrl)
            ? string.Empty
            : $"""<p><a class="btn" href="{HtmlEncoder.Default.Encode(_settings.AppDownloadUrl)}">Get the Pinpoint app</a></p>""";

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Pinpoint check-in</title>
              <style>
                body { font: 1.125rem/1.5 system-ui, sans-serif; margin: 0; padding: 2rem 1rem; background: #fff; color: #111; }
                main { max-width: 32rem; margin: 0 auto; }
                .btn { display: inline-block; padding: .875rem 1.25rem; min-height: 48px; background: #0b57d0; color: #fff; border-radius: .5rem; text-decoration: none; font-weight: 600; }
                @media (prefers-color-scheme: dark) { body { background: #111; color: #eee; } }
              </style>
            </head>
            <body>
              <main>
                <h1>Check in with the Pinpoint app</h1>
                <p>Attendance can only be recorded from the Pinpoint app on your registered phone.</p>
                <p>If the app is installed, open it and tap <strong>Scan</strong>. The code on screen changes every 15 seconds, so scan it again from inside the app.</p>
                {{download}}
              </main>
            </body>
            </html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }
}
