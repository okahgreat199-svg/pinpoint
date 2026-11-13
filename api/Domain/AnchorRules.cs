namespace SeatCharter.Api.Domain;

/// <summary>
/// Checks the lecturer's location when a session starts. Every student check-in is measured against this
/// anchor, so a lecturer starting a session from home would make the whole register meaningless.
/// </summary>
public static class AnchorRules
{
    /// <summary>Returns a user-facing reason the anchor is unusable, or null when it is fine.</summary>
    public static string? Validate(GeoPoint anchor, double accuracyM, bool isMocked,
        IReadOnlyList<IReadOnlyList<GeoPoint>> campusZones, GeoPoint? room, double roomRadiusM, string? roomName,
        double maxAccuracyM, double maxCampusAllowanceM)
    {
        if (isMocked) return "Your phone is reporting a simulated location. Turn off mock locations to start a session.";

        if (accuracyM > maxAccuracyM)
            return $"Your location is only accurate to {accuracyM:0} m. Move nearer a window and try again.";

        var allowance = Math.Clamp(accuracyM, 0, maxCampusAllowanceM);
        if (campusZones.Count > 0 && !campusZones.Any(z => GeoMath.IsInsideWithAllowance(anchor, z, allowance)))
            return "You need to be on campus to start a session.";

        if (room is { } r)
        {
            var distance = GeoMath.HaversineM(anchor, r);
            if (distance > roomRadiusM + allowance)
                return $"You're about {distance:0} m from {roomName ?? "the scheduled room"}. Start the session from the room.";
        }

        return null;
    }
}
