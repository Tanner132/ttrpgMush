using SeattleByNight.Domain.Enums;
using SeattleByNight.Domain;

namespace SeattleByNight.Application.WorldEditing;

public static class WorldMutationValidation
{
    public static Dictionary<string, string[]> ValidateCreateRoom(CreateRoomMutation mutation)
    {
        var errors = new Dictionary<string, string[]>();

        AddRequiredLength(errors, "name", mutation.Name, 120);
        // WF-73: a room may leave its description blank and show its
        // district's; publishing refuses a room with neither.
        AddMaxLength(errors, "description", mutation.Description, 4000);

        AddAccessTypeError(errors, mutation.AccessType);

        AddCoordinateError(errors, "mapX", mutation.MapX);
        AddCoordinateError(errors, "mapY", mutation.MapY);
        AddCoordinateError(errors, "mapLayer", mutation.MapLayer);

        return errors;
    }

    // Workstream 8: every room is Public. Leaving it out means Public too; the
    // field stays for player-owned rooms (workstream 7).
    public static bool TryParseAuthoredAccessType(string? value, out RoomAccessType accessType)
    {
        accessType = RoomAccessType.Public;
        return value is null || value == nameof(RoomAccessType.Public);
    }

    private static void AddAccessTypeError(Dictionary<string, string[]> errors, string? accessType)
    {
        if (!TryParseAuthoredAccessType(accessType, out _))
        {
            errors["accessType"] = ["Every room is Public: job sites are ordinary rooms, and player-owned rooms come later."];
        }
    }

    public static Dictionary<string, string[]> ValidateUpdateRoom(UpdateRoomMutation mutation, Guid version)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequiredLength(errors, "name", mutation.Name, 120);
        // WF-73: a room may leave its description blank and show its
        // district's; publishing refuses a room with neither.
        AddMaxLength(errors, "description", mutation.Description, 4000);

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

        var unknown = (mutation.States ?? []).Where(state => !ExitStates.IsValid(state)).ToArray();
        if (unknown.Length > 0)
        {
            errors["states"] =
            [
                $"Unknown door state: {string.Join(", ", unknown)}. A door is any of: {string.Join(", ", ExitStates.All)}.",
            ];
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

    private static void AddMaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value is not null && value.Length > maxLength)
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
