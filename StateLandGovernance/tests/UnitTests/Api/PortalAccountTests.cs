using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.Api.Security;
using Xunit;

public sealed class PortalAccountTests
{
    [Fact]
    public void DemoPasswords_UseRandomSaltsAndExistingVerifier()
    {
        var first = DemoAccountProfiles.Create()[0];
        var second = DemoAccountProfiles.Create()[0];
        PortalAccountStore.SetDemoPassword(first); PortalAccountStore.SetDemoPassword(second);
        Assert.NotEqual(first["PasswordSalt"], second["PasswordSalt"]);
        Assert.NotEqual(first["PasswordHash"], second["PasswordHash"]);
        Assert.True(DevelopmentAccounts.Verify(DemoAccountProfiles.Password, first["PasswordSalt"], first["PasswordHash"]));
        Assert.False(DevelopmentAccounts.Verify("UNIT-TEST-wrong", first["PasswordSalt"], first["PasswordHash"]));
    }

    [Fact]
    public void Model_KeepsAccountsAndIdentityUniquenessInDedicatedSchema()
    {
        using var db = new PortalAccountDbContext(new DbContextOptionsBuilder<PortalAccountDbContext>()
            .UseNpgsql("Host=localhost;Database=unit_test_only;Username=unused").Options);
        var model = db.Model.FindEntityType(typeof(PortalAccount))!;
        Assert.Equal("portal_auth", model.GetSchema());
        Assert.Contains(model.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "ActorId");
        Assert.Equal("Username", model.FindPrimaryKey()!.Properties.Single().Name);
        Assert.Contains("20261021000100_AddPortalAccounts", db.Database.GetMigrations());
    }

    [Fact]
    public void DemoProfiles_PreserveKnownActorIdsWithoutCaseGrantsOrPasswordMaterial()
    {
        var profiles = DemoAccountProfiles.Create();
        Assert.Equal(9, profiles.Length);
        Assert.Equal(profiles.Length, profiles.Select(x => x["ActorId"]).Distinct().Count());
        Assert.Equal("44fe5f4d-c935-531d-9097-2f1a14bdada8", profiles.Single(x => x["Username"] == "ds")["ActorId"]);
        foreach (var profile in profiles)
        {
            Assert.DoesNotContain("PasswordHash", profile.Keys);
            Assert.DoesNotContain("CaseId", profile.Keys);
            Assert.DoesNotContain("Capabilities", profile.Keys);
            Assert.StartsWith("/", profile["Home"]);
        }
    }

    [Fact]
    public async Task Setup_Production_RejectsBeforeOpeningDatabase()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalAccountStore.SetupAsync(
            new ConfigurationBuilder().Build(), new Production(), "."));
    }
    private sealed class Production : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "UnitTest";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
