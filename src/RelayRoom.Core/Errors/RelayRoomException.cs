namespace RelayRoom.Core.Errors;

public class RelayRoomException : Exception
{
    public RelayRoomException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class NotFoundException(string message = "Not found.") : RelayRoomException(message, 404);

public sealed class ForbiddenException(string message = "Forbidden.") : RelayRoomException(message, 403);

public sealed class ConflictException(string message) : RelayRoomException(message, 409);

public sealed class ValidationException(string message) : RelayRoomException(message, 400);

public sealed class RoomExpiredException() : RelayRoomException("This room has expired.", 410);

public sealed class RoomFullException() : RelayRoomException("This room is full.", 409);

public sealed class QuotaExceededException(string message) : RelayRoomException(message, 413);
