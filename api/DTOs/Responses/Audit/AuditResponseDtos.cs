namespace SeatCharter.Api.DTOs.Responses.Audit;

public class AuditLogDto
{
    public string Id { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string? DataJson { get; set; }
}
