namespace RoadToMillion.Api.Models;

public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public ResultType Type { get; }
    public string? Location { get; }

    protected Result(bool isSuccess, ResultType type, string? errorMessage = null, string? location = null)
    {
        IsSuccess = isSuccess;
        Type = type;
        ErrorMessage = errorMessage;
        Location = location;
    }

    public static Result Success() => new(true, ResultType.Success);
    public static Result NoContent() => new(true, ResultType.NoContent);
    public static Result NotFound() => new(false, ResultType.NotFound, "Resource not found");
    public static Result BadRequest(string message) => new(false, ResultType.BadRequest, message);
    public static Result Conflict(string message) => new(false, ResultType.Conflict, message);
    public static Result Error(string message) => new(false, ResultType.Error, message);
}

public class Result<T> : Result
{
    public T? Data { get; }

    private Result(bool isSuccess, ResultType type, T? data = default, string? errorMessage = null, string? location = null)
        : base(isSuccess, type, errorMessage, location)
    {
        Data = data;
    }

    public static Result<T> Success(T data) => new(true, ResultType.Success, data);
    public static Result<T> Created(T data, string location) => new(true, ResultType.Created, data, location: location);
    public static new Result<T> NotFound() => new(false, ResultType.NotFound);
    public static new Result<T> BadRequest(string message) => new(false, ResultType.BadRequest, errorMessage: message);
    public static new Result<T> Conflict(string message) => new(false, ResultType.Conflict, errorMessage: message);
    public static new Result<T> Error(string message) => new(false, ResultType.Error, errorMessage: message);
}

public enum ResultType
{
    Success,
    Created,
    NoContent,
    BadRequest,
    NotFound,
    Conflict,
    Error
}
