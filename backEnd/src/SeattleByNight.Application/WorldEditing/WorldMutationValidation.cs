using SeattleByNight.Domain.Enums;
using SeattleByNight.Domain;

namespace SeattleByNight.Application.WorldEditing;

internal static class WorldMutationValidation
{
    public static Dictionary<string, string[]> ValidateCreateRoom(CreateRoomMutation mutation)
    {
        var errors = new Dictionary<string, string[]>();

        AddRequiredLength(errors, "name", mutation.Name, 120);
        AddRequiredLength(errors, "description", mutation.Description, 4000);

        if (mutation.AccessType != (long)RoomAccessType.Public)
        {
            errors["accessType"] = ["Access type must be Public."];
        }

        AddCoordinateError(errors, "mapX", mutation.MapX);
        AddCoordinateError(errors, "mapY", mutation.MapY);
        AddCoordinateError(errors, "mapLayer", mutation.MapLayer);

        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdateRoom(UpdateRoomMutation mutation, Guid version)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequiredLength(errors, "name", mutation.Name, 120);
        AddRequiredLength(errors, "description", mutation.Description, 4000);

        if (mutation.AccessType != (long)RoomAccessType.Public)
        {
            errors["accessType"] = ["Access type must be Public."];
        }

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
