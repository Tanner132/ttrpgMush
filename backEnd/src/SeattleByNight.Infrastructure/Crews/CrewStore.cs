using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Crews;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.Crews;

// See ICrewStore and 8.1 "Crews".
public sealed class CrewStore(SeattleByNightDbContext db, TimeProvider timeProvider) : ICrewStore
{
    private static readonly string[] OpenStatuses =
    [
        MissionInstanceStatus.Accepted.ToString(),
        MissionInstanceStatus.InProgress.ToString(),
        MissionInstanceStatus.ReadyToTurnIn.ToString(),
    ];

    public async Task<CrewView?> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var crewId = await db.Characters.AsNoTracking()
            .Where(character => character.Id == characterId)
            .Select(character => character.CrewId)
            .SingleOrDefaultAsync(cancellationToken);
        return crewId is null ? null : await ViewAsync(crewId.Value, cancellationToken);
    }

    public async Task<CrewResult> InviteAsync(
        Guid inviterCharacterId, string inviteeName, CancellationToken cancellationToken = default)
    {
        var inviter = await FindAsync(inviterCharacterId, cancellationToken);
        if (inviter is null)
        {
            return CrewResult.Failure(CrewError.CharacterNotFound);
        }

        var normalized = inviteeName.Trim().ToUpperInvariant();
        var invitee = await db.Characters
            .Where(character => character.NormalizedName == normalized
                && character.LifecycleState == CharacterLifecycleState.Finalized)
            .SingleOrDefaultAsync(cancellationToken);
        if (invitee is null)
        {
            return CrewResult.Failure(CrewError.NobodyByThatName);
        }

        if (invitee.Id == inviter.Id)
        {
            return CrewResult.Failure(CrewError.CannotInviteYourself);
        }

        if (invitee.CurrentRoomId != inviter.CurrentRoomId)
        {
            return CrewResult.Failure(CrewError.NotInTheSameRoom);
        }

        if (invitee.CrewId is not null)
        {
            return CrewResult.Failure(CrewError.AlreadyInACrew);
        }

        if (inviter.CrewId is { } crewId && await CountMembersAsync(crewId, cancellationToken) >= CrewRules.MaxMembers)
        {
            return CrewResult.Failure(CrewError.CrewFull);
        }

        var now = timeProvider.GetUtcNow();
        await db.CrewInvitations
            .Where(invite => invite.InviterCharacterId == inviter.Id && invite.InviteeCharacterId == invitee.Id)
            .ExecuteDeleteAsync(cancellationToken);
        db.CrewInvitations.Add(new CrewInvitation
        {
            InviterCharacterId = inviter.Id,
            InviteeCharacterId = invitee.Id,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + CrewRules.InvitationLifetime,
        });
        await db.SaveChangesAsync(cancellationToken);

        return new CrewResult(
            CrewError.None,
            Message: $"You invite {invitee.Name} to your crew.",
            NotifyCharacterIds: [invitee.Id],
            Notice: $"{inviter.Name} invites you to their crew. Type /crew join to accept.");
    }

    public async Task<CrewResult> JoinAsync(Guid inviteeCharacterId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var invitee = await FindAsync(inviteeCharacterId, cancellationToken);
        if (invitee is null)
        {
            return CrewResult.Failure(CrewError.CharacterNotFound);
        }

        if (invitee.CrewId is not null)
        {
            return CrewResult.Failure(CrewError.AlreadyInACrew);
        }

        var now = timeProvider.GetUtcNow();
        var invitation = await db.CrewInvitations
            .Where(invite => invite.InviteeCharacterId == invitee.Id && invite.ExpiresAtUtc > now)
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var inviter = invitation is null ? null : await FindAsync(invitation.InviterCharacterId, cancellationToken);
        if (inviter is null)
        {
            return CrewResult.Failure(CrewError.NoInvitation);
        }

        // The crew joined is the inviter's now, or a new one for the two.
        var members = inviter.CrewId is { } existingId
            ? await db.Characters.Where(character => character.CrewId == existingId).ToListAsync(cancellationToken)
            : [inviter];
        if (members.Count + 1 > CrewRules.MaxMembers)
        {
            return CrewResult.Failure(CrewError.CrewFull);
        }

        var memberIds = members.Select(member => member.Id).ToArray();
        if (await db.MissionInstances.AnyAsync(
                run => memberIds.Contains(run.CharacterId) && OpenStatuses.Contains(run.Status), cancellationToken))
        {
            return CrewResult.Failure(CrewError.CrewOnAJob);
        }

        var crewId = inviter.CrewId;
        if (crewId is null)
        {
            var crew = new Crew { Name = $"{inviter.Name}'s crew", CreatedAtUtc = now };
            db.Crews.Add(crew);
            inviter.CrewId = crew.Id;
            crewId = crew.Id;
        }

        invitee.CrewId = crewId;
        await db.CrewInvitations
            .Where(invite => invite.InviteeCharacterId == invitee.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var view = await ViewAsync(crewId.Value, cancellationToken);
        return new CrewResult(
            CrewError.None,
            view,
            Message: $"You join {view!.Name}.",
            NotifyCharacterIds: memberIds,
            Notice: $"{invitee.Name} joins the crew.");
    }

    public async Task<CrewResult> LeaveAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await FindAsync(characterId, cancellationToken);
        if (character is null)
        {
            return CrewResult.Failure(CrewError.CharacterNotFound);
        }

        if (character.CrewId is not { } crewId)
        {
            return CrewResult.Failure(CrewError.NotInACrew);
        }

        // Leaving is always allowed. Acceptances already made stay the
        // character's own: they are still on any run they were on (8.1).
        character.CrewId = null;
        await db.SaveChangesAsync(cancellationToken);

        var remaining = await db.Characters.Where(member => member.CrewId == crewId)
            .Select(member => member.Id)
            .ToListAsync(cancellationToken);
        if (remaining.Count == 0)
        {
            await db.Crews.Where(crew => crew.Id == crewId).ExecuteDeleteAsync(cancellationToken);
        }

        return new CrewResult(
            CrewError.None,
            Message: "You leave the crew.",
            NotifyCharacterIds: remaining,
            Notice: $"{character.Name} leaves the crew.");
    }

    private Task<Character?> FindAsync(Guid characterId, CancellationToken cancellationToken) =>
        db.Characters.SingleOrDefaultAsync(
            character => character.Id == characterId && character.LifecycleState == CharacterLifecycleState.Finalized,
            cancellationToken);

    private Task<int> CountMembersAsync(Guid crewId, CancellationToken cancellationToken) =>
        db.Characters.CountAsync(character => character.CrewId == crewId, cancellationToken);

    private async Task<CrewView?> ViewAsync(Guid crewId, CancellationToken cancellationToken)
    {
        var crew = await db.Crews.AsNoTracking().SingleOrDefaultAsync(row => row.Id == crewId, cancellationToken);
        if (crew is null)
        {
            return null;
        }

        var members = await db.Characters.AsNoTracking()
            .Where(character => character.CrewId == crewId)
            .OrderBy(character => character.Name)
            .Select(character => new CrewMember(character.Id, character.Name))
            .ToListAsync(cancellationToken);
        return new CrewView(crew.Id, crew.Name, members);
    }
}
