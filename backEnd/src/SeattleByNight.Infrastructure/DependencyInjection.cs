using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.Dice;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoleAdmin;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Characters;
using SeattleByNight.Infrastructure.CharacterCareer;
using SeattleByNight.Infrastructure.CharacterCreation;
using SeattleByNight.Infrastructure.Dice;
using SeattleByNight.Infrastructure.GameEngine;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Movement;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.PlaySessions;
using SeattleByNight.Infrastructure.RoleAdmin;
using SeattleByNight.Infrastructure.RoomChat;
using SeattleByNight.Infrastructure.RoomSessions;
using SeattleByNight.Infrastructure.WorldEditing;

namespace SeattleByNight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SeattleByNightDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IRoomSessionReader, RoomSessionReader>();
        services.AddScoped<ICharacterStore, CharacterStore>();
        services.AddScoped<ICharacterCreationDraftStore, CharacterCreationDraftStore>();
        services.AddScoped<ICharacterCareerStateStore, CharacterCareerStateStore>();
        services.AddScoped<ICharacterCareerHistoryReader, CharacterCareerHistoryReader>();
        services.AddScoped<ICharacterCareerAdvancementStore, CharacterCareerAdvancementStore>();
        services.AddScoped<IPlaySessionStore, PlaySessionStore>();
        services.AddScoped<IRoomChatStore, RoomChatStore>();
        services.AddScoped<IMovementStore, MovementStore>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IUserAdminStore, UserAdminStore>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<IWorldGraphReader, WorldGraphReader>();
        services.AddScoped<IWorldEditorStore, WorldEditorStore>();
        services.AddSingleton<IDiceRandom, CryptographicDiceRandom>();
        services.AddSingleton<IDiceEngine, DiceEngine>();
        services.AddScoped<ICharacterRuntimeStateStore, CharacterRuntimeStateStore>();
        services.AddScoped<IGameTestAuditStore, GameTestAuditStore>();
        services.AddSingleton<ISeedSource, CryptographicSeedSource>();
        services.AddScoped<IActiveEffectReader, ActiveEffectStore>();
        services.AddScoped<IStateChangeApplier, StateChangeApplier>();
        services.AddScoped<IRoomContentReader, RoomContentStore>();
        services.AddScoped<IRoomContentEditor, RoomContentStore>();
        services.AddScoped<IMissionReader, MissionStore>();
        services.AddScoped<IMissionAssignmentStore, MissionStore>();
        services.AddScoped<IEncounterLifecycleStore, EncounterLifecycleStore>();
        services.AddScoped<IGameScopeResolver, GameScopeResolver>();

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.";

                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<SeattleByNightDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
