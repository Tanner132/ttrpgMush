using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SeattleByNight.Infrastructure.Persistence;

public sealed class SeattleByNightDbContextFactory : IDesignTimeDbContextFactory<SeattleByNightDbContext>
{
    public SeattleByNightDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__SeattleByNight")
            ?? "Host=localhost;Port=5432;Database=seattlebynight;Username=seattlebynight;Password=localdevpassword";

        var optionsBuilder = new DbContextOptionsBuilder<SeattleByNightDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SeattleByNightDbContext(optionsBuilder.Options);
    }
}
