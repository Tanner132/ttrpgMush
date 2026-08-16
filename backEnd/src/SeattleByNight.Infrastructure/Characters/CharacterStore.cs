using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeattleByNight.Application.Characters;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.Characters;

public sealed class CharacterStore : ICharacterStore
{
    private const int MaxCharactersPerUser = 2;

    private readonly SeattleByNightDbContext _dbContext;

    public CharacterStore(SeattleByNightDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CharacterSummary>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Characters
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new CharacterSummary(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateCharacterResult> CreateAsync(
        Guid userId,
        string name,
        string normalizedName,
        Guid startingRoomId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent character creation for the same user by locking their row.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM asp_net_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);

        var characterCount = await _dbContext.Characters
            .CountAsync(c => c.UserId == userId, cancellationToken);

        if (characterCount >= MaxCharactersPerUser)
        {
            return CreateCharacterResult.Failure(CreateCharacterError.LimitReached);
        }

        var nameTaken = await _dbContext.Characters
            .AnyAsync(c => c.NormalizedName == normalizedName, cancellationToken);

        if (nameTaken)
        {
            return CreateCharacterResult.Failure(CreateCharacterError.NameTaken);
        }

        var character = new Character
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NormalizedName = normalizedName,
            CurrentRoomId = startingRoomId
        };

        _dbContext.Characters.Add(character);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return CreateCharacterResult.Failure(CreateCharacterError.NameTaken);
        }

        await transaction.CommitAsync(cancellationToken);

        return CreateCharacterResult.Success(new CharacterSummary(character.Id, character.Name));
    }
}
