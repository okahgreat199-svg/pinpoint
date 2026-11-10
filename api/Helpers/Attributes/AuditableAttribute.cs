namespace SeatCharter.Api.Helpers.Attributes;

/// <summary>
/// Marker attribute for entities that should have row-level audit logging
/// in the ApplicationDbContext SaveChangesAsync override.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AuditableAttribute : Attribute
{
}
