using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeattleByNight.Domain.Entities;

namespace SeattleByNight.Infrastructure.Persistence.Configurations;

public sealed class CityMapConfiguration : IEntityTypeConfiguration<CityMap>
{
    public void Configure(EntityTypeBuilder<CityMap> builder)
    {
        builder.ToTable("maps");
        builder.HasKey(map => map.Id);
        builder.Property(map => map.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(map => map.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(map => map.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(map => map.Version).HasColumnName("version").HasColumnType("uuid").IsConcurrencyToken().ValueGeneratedNever();
        builder.HasIndex(map => map.Name).IsUnique().HasDatabaseName("ux_maps_name");
    }
}

public sealed class TravelLinkConfiguration : IEntityTypeConfiguration<TravelLink>
{
    public void Configure(EntityTypeBuilder<TravelLink> builder)
    {
        builder.ToTable("travel_links");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(link => link.SourceRoomId).HasColumnName("source_room_id").HasColumnType("uuid").IsRequired();
        builder.Property(link => link.DestinationRoomId).HasColumnName("destination_room_id").HasColumnType("uuid").IsRequired();
        builder.Property(link => link.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(link => link.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(link => link.Version).HasColumnName("version").HasColumnType("uuid").IsConcurrencyToken().ValueGeneratedNever();

        builder.HasIndex(link => new { link.SourceRoomId, link.Name }).IsUnique().HasDatabaseName("ux_travel_links_source_name");
        builder.HasIndex(link => link.DestinationRoomId).HasDatabaseName("ix_travel_links_destination");

        // A link goes with either of its rooms.
        builder.HasOne<Room>().WithMany().HasForeignKey(link => link.SourceRoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Room>().WithMany().HasForeignKey(link => link.DestinationRoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts");
        builder.HasKey(district => district.Id);
        builder.Property(district => district.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(district => district.MapId).HasColumnName("map_id").HasColumnType("uuid").IsRequired();
        builder.Property(district => district.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(district => district.DefaultDescription).HasColumnName("default_description").HasMaxLength(4000).IsRequired();
        builder.Property(district => district.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        builder.Property(district => district.Version).HasColumnName("version").HasColumnType("uuid").IsConcurrencyToken().ValueGeneratedNever();
        builder.HasIndex(district => new { district.MapId, district.Name }).IsUnique().HasDatabaseName("ux_districts_map_name");
        builder.HasOne<CityMap>().WithMany().HasForeignKey(district => district.MapId).OnDelete(DeleteBehavior.Restrict);
    }
}
