using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace StateLandGovernance.Api.Security;

[DbContext(typeof(PortalAccountDbContext))]
public sealed class PortalAccountModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model)
    {
        model.HasAnnotation("ProductVersion", "8.0.2").HasDefaultSchema("portal_auth");
        model.Entity<PortalAccount>(entity =>
        {
            entity.Property(x => x.Username).HasColumnType("text");
            entity.Property(x => x.ActorId).HasColumnType("uuid");
            entity.Property(x => x.ProfileJson).IsRequired().HasColumnType("text");
            entity.HasKey(x => x.Username);
            entity.HasIndex(x => x.ActorId).IsUnique();
            entity.ToTable("accounts", "portal_auth");
        });
        model.Entity<PortalAccountSetting>(entity =>
        {
            entity.Property(x => x.Key).HasColumnType("text");
            entity.Property(x => x.Json).IsRequired().HasColumnType("text");
            entity.HasKey(x => x.Key);
            entity.ToTable("settings", "portal_auth");
        });
    }
}
