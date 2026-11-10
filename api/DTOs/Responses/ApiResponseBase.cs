using System.Net;

namespace SeatCharter.Api.DTOs.Responses;

public class ApiResponseBase<T>
{
    public T? ResponseData { get; set; }
    public int StatusCode { get; set; }
    public bool Status { get; set; }
    public string ResponseMessage { get; set; } = string.Empty;

    public ApiResponseBase() { }
    public ApiResponseBase(T? data, int statusCode, string message, bool status)
    { ResponseData = data; StatusCode = statusCode; ResponseMessage = message; Status = status; }

    public static ApiResponseBase<T> Ok(T data, string message = "Successful") => new(data, (int)HttpStatusCode.OK, message, true);
    public static ApiResponseBase<T> OkNoData(string message = "Successful") => new(default, (int)HttpStatusCode.OK, message, true);
    public static ApiResponseBase<T> Created(T data, string message = "Created successfully.") => new(data, (int)HttpStatusCode.Created, message, true);
    public static ApiResponseBase<T> BadRequest(string message) => new(default, (int)HttpStatusCode.BadRequest, message, false);
    public static ApiResponseBase<T> Unauthorized(string message) => new(default, (int)HttpStatusCode.Unauthorized, message, false);
    public static ApiResponseBase<T> Forbidden(string message) => new(default, (int)HttpStatusCode.Forbidden, message, false);
    public static ApiResponseBase<T> NotFound(string message) => new(default, (int)HttpStatusCode.NotFound, message, false);
    public static ApiResponseBase<T> Conflict(string message) => new(default, (int)HttpStatusCode.Conflict, message, false);
    public static ApiResponseBase<T> UnprocessableEntity(string message) => new(default, (int)HttpStatusCode.UnprocessableEntity, message, false);
    /// <summary>428: the request is fine but needs the user's confirmation; resend with the confirm flag set.</summary>
    public static ApiResponseBase<T> ConfirmationRequired(string message) => new(default, (int)HttpStatusCode.PreconditionRequired, message, false);
    public static ApiResponseBase<T> ServerError(string message = "An unexpected error occurred.") => new(default, (int)HttpStatusCode.InternalServerError, message, false);

    public bool IsSuccess => StatusCode is >= 200 and <= 299;
}
