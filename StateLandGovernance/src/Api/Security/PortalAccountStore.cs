using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace StateLandGovernance.Api.Security;

// Host-owned identity storage. It contains no case records and is never migrated on API startup.
public sealed class PortalAccountDbContext(DbContextOptions<PortalAccountDbContext> options) : DbContext(options)
{
    public DbSet<PortalAccount> Accounts => Set<PortalAccount>();
    public DbSet<PortalAccountSetting> Settings => Set<PortalAccountSetting>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("portal_auth");
        model.Entity<PortalAccount>().ToTable("accounts").HasKey(x => x.Username);
        model.Entity<PortalAccount>().HasIndex(x => x.ActorId).IsUnique();
        model.Entity<PortalAccountSetting>().ToTable("settings").HasKey(x => x.Key);
    }
}
public sealed class PortalAccount
{
    public string Username { get; set; } = "";
    public Guid ActorId { get; set; }
    public string ProfileJson { get; set; } = "";
}
public sealed class PortalAccountSetting
{
    public string Key { get; set; } = "";
    public string Json { get; set; } = "";
}

[DbContext(typeof(PortalAccountDbContext))]
[Migration("20261021000100_AddPortalAccounts")]
public sealed class AddPortalAccounts : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        migration.EnsureSchema("portal_auth");
        migration.CreateTable("accounts", schema: "portal_auth", columns: t => new
        {
            Username = t.Column<string>(nullable: false), ActorId = t.Column<Guid>(nullable: false),
            ProfileJson = t.Column<string>(nullable: false)
        }, constraints: t => t.PrimaryKey("PK_accounts", x => x.Username));
        migration.CreateIndex("IX_accounts_ActorId", "accounts", "ActorId", "portal_auth", unique: true);
        migration.CreateTable("settings", schema: "portal_auth", columns: t => new
        {
            Key = t.Column<string>(nullable: false), Json = t.Column<string>(nullable: false)
        }, constraints: t => t.PrimaryKey("PK_settings", x => x.Key));
    }
    protected override void Down(MigrationBuilder migration) =>
        throw new NotSupportedException("Preserve account identities and trusted configuration; restore code without deleting accounts.");
}

public static class PortalAccountStore
{
    public static void SetDemoPassword(Dictionary<string,string?> profile)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        profile["PasswordSalt"] = Convert.ToBase64String(salt);
        profile["PasswordHash"] = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            DemoAccountProfiles.Password, salt, 100000, HashAlgorithmName.SHA256, 32));
    }
    public static PortalAccountDbContext Open(IConfiguration config)
    {
        var connection = Environment.GetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION")
            ?? config.GetConnectionString("PortalAccounts")
            ?? throw new InvalidOperationException("Portal accounts database is not configured. Run Setup-DemoAccounts.ps1 first.");
        var target = new NpgsqlConnectionStringBuilder(connection);
        foreach (var key in new[] { "GOVERNANCE_INTELLIGENCE_CONNECTION", "WORKFLOW_GOVERNANCE_CONNECTION", "LAND_INTELLIGENCE_CONNECTION" })
            if (Environment.GetEnvironmentVariable(key) is { Length: > 0 } moduleConnection)
            {
                var module = new NpgsqlConnectionStringBuilder(moduleConnection);
                if (target.Database == module.Database && target.Port == module.Port && target.Host == module.Host)
                    throw new InvalidOperationException("Portal accounts must use a separate database from module history.");
            }
        return new(new DbContextOptionsBuilder<PortalAccountDbContext>().UseNpgsql(connection,
            o => o.MigrationsHistoryTable("__ef_migrations_history", "portal_auth")).Options);
    }

    public static async Task SetupAsync(IConfiguration config, IHostEnvironment environment, string contentRoot)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Demo account setup is Development-only.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION")) &&
            string.IsNullOrWhiteSpace(config.GetConnectionString("PortalAccounts")))
        {
            var source = Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION")
                ?? Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION")
                ?? throw new InvalidOperationException("Configure local PostgreSQL before setting up accounts.");
            var connection = new NpgsqlConnectionStringBuilder(source) { Database="portal_accounts_local" };
            if (connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
                throw new InvalidOperationException("Automatic demo database setup requires local PostgreSQL.");
            var directory = new DirectoryInfo(contentRoot);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, ".env"))) directory=directory.Parent;
            if (directory == null) throw new InvalidOperationException("An ignored repository .env is required for local connection setup.");
            await File.AppendAllTextAsync(Path.Combine(directory.FullName, ".env"),
                Environment.NewLine+"PORTAL_ACCOUNTS_CONNECTION="+connection.ConnectionString+Environment.NewLine);
            Environment.SetEnvironmentVariable("PORTAL_ACCOUNTS_CONNECTION", connection.ConnectionString);
        }
        using var db = Open(config);
        await db.Database.MigrateAsync(); // Only this explicitly invoked setup command deploys the account schema.
        var legacyPath = Path.Combine(contentRoot, "development-accounts.local.json");
        using var legacy = File.Exists(legacyPath) ? JsonDocument.Parse(await File.ReadAllTextAsync(legacyPath)) : null;
        var profiles = legacy is null ? DemoAccountProfiles.Create() :
            legacy.RootElement.GetProperty("LocalDevelopmentSignIn").GetProperty("Accounts")
                .EnumerateArray().Select(p => JsonSerializer.Deserialize<Dictionary<string,string?>>(p.GetRawText())!).ToArray();
        foreach (var profile in profiles)
        {
            var username = profile["Username"]!.ToLowerInvariant();
            var actor = Guid.Parse(profile["ActorId"]!);
            var existing = await db.Accounts.SingleOrDefaultAsync(x => x.Username == username);
            if (existing != null)
            {
                if (existing.ActorId != actor) throw new InvalidOperationException("Existing account identity differs; manual owner reconciliation required.");
                continue; // Never overwrite a password, profile or identity.
            }
            if (legacy is null)
            {
                SetDemoPassword(profile);
            }
            db.Accounts.Add(new() { Username=username, ActorId=actor, ProfileJson=JsonSerializer.Serialize(profile) });
        }
        // One-time preservation of this machine's existing trusted grants. Never ship these grants to teammates.
        if (legacy != null)
            foreach (var section in legacy.RootElement.EnumerateObject())
            {
                var value = section.Name == "LocalDevelopmentSignIn"
                    ? JsonSerializer.Serialize(section.Value.EnumerateObject().Where(x => x.Name != "Accounts")
                        .ToDictionary(x => x.Name, x => x.Value)) : section.Value.GetRawText();
                if (!await db.Settings.AnyAsync(x => x.Key == section.Name))
                    db.Settings.Add(new() { Key=section.Name, Json=value });
            }
        await db.SaveChangesAsync();
        Console.WriteLine("Demo account setup complete. Existing accounts and passwords preserved. No case data changed.");
    }

    public static async Task LoadAsync(ConfigurationManager config)
    {
        using var db = Open(config);
        var values = new Dictionary<string,string?>();
        foreach (var setting in await db.Settings.AsNoTracking().ToListAsync())
        {
            using var document = JsonDocument.Parse(setting.Json);
            Flatten(document.RootElement, setting.Key, values);
        }
        var accounts = await db.Accounts.AsNoTracking().OrderBy(x => x.Username).ToListAsync();
        for (var i=0; i<accounts.Count; i++)
        {
            using var document = JsonDocument.Parse(accounts[i].ProfileJson);
            Flatten(document.RootElement, $"LocalDevelopmentSignIn:Accounts:{i}", values);
        }
        config.AddInMemoryCollection(values);
    }
    private static void Flatten(JsonElement value, string path, Dictionary<string,string?> output)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject()) Flatten(property.Value, path+":"+property.Name, output);
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var i=0; foreach (var item in value.EnumerateArray()) Flatten(item, path+":"+i++, output);
        }
        else output[path] = value.ValueKind == JsonValueKind.Null ? null : value.ToString();
    }
}
