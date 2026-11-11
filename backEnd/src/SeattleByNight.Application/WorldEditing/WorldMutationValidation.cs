using SeattleByNight.Domain.Enums;
using SeattleByNight.Domain;

namespace SeattleByNight.Application.WorldEditing;

public static class WorldMutationValidation
{
    public static Dictionary<string, string[]> ValidateCreateRoom(CreateRoomMutation mutation)
    {
        var errors = new Dictionary<string, string[]>();

        AddRequiredLength(errors, "name", mutation.Name, 120);
        AddRequiredLength(errors, "description", mutation.Description, 4000);

        AddAccessTypeError(errors, mutation.AccessType);

        AddCoordinateError(errors, "mapX", mutation.MapX);
        AddCoordinateError(errors, "mapY", mutation.MapY);
        AddCoordinateError(errors, "mapLayer", mutation.MapLayer);

        return errors;
    }

    // WF-25: a room is Public, or Private (part of a job site; only runners on
    // the run get in, into their run's copy). Instanced is engine-only: it
    // marks a run's copy and is never authored.
    public static bool TryParseAuthoredAccessType(string? value, out RoomAccessType accessType)
    {
        accessType = value switch
        {
            nameof(RoomAccessType.Public) => RoomAccessType.Public,
            nameof(RoomAccessType.Private) => RoomAccessType.Private,
            _ => RoomAccessType.Instanced,
        };

        return accessType != RoomAccessType.Instanced;
    }

    private static void AddAccessTypeError(Dictionary<string, string[]> errors, string? accessType)
    {
        if (!TryParseAuthoredAccessType(accessType, out _))
        {
            errors["accessType"] = ["Access type must be Public or Private."];
        }
    }

    public static Dictionary<string, string[]> ValidateUpdateRoom(UpdateRoomMutation mutation, Guid version)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequiredLength(errors, "name", mutation.Name, 120);
        AddRequiredLength(errors, "description", mutation.Description, 4000);

        AddAccessTypeError(errors, mutation.AccessType);

        if (version == Guid.Empty)
        {
            errors["version"] = ["Version is required."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateExit(RoomExitMutation mutation, bool requiresVersion, Guid version)
    {
        var errors = new Dictionary<string, string[]>();

        if (mutation.SourceRoomId == Guid.Empty)
        {
            errors["sourceRoomId"] = ["Source room is required."];
        }

        if (mutation.DestinationRoomId == Guid.Empty)
        {
            errors["destinationRoomId"] = ["Destination room is required."];
        }

        if (!RoomDirections.IsValid(mutation.Direction))
        {
            errors["direction"] = [$"Direction must be one of: {string.Join(',', RoomDirections.All)}."];
        }

        if (requiresVersion && version == Guid.Empty)
        {
            errors["version"] = ["Version is required."];
        }

        return errors;
    }

    private static void AddRequiredLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["The field is required."];
        }
        else if (value.Length > maxLength)
        {
            errors[field] = [$"The field must not exceed {maxLength} characters."];
        }
    }

    private static void AddCoordinateError(Dictionary<string, string[]> errors, string field, long? value)
    {
        if (value is null)
        {
            errors[field] = ["The coordinate is required."];
        }
        else if (value is < int.MinValue or > int.MaxValue)
        {
            errors[field] = ["The coordinate must be a 32-bit integer."];
        }
    }
}
