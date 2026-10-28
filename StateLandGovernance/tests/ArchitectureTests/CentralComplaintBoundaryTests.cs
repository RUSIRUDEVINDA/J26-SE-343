namespace StateLandGovernance.ArchitectureTests;

public sealed class CentralComplaintBoundaryTests
{
    [Fact]
    public void ComplaintApplication_UsesDirectoryPortWithoutPeerOrHostDependencies()
    {
        var references = typeof(StateLandGovernance.GovernanceIntelligence.Application.Complaints.IComplaintOfficerDirectory)
            .Assembly.GetReferencedAssemblies().Select(x => x.Name ?? "");
        Assert.DoesNotContain(references, x => x.Contains("WorkflowGovernance", StringComparison.Ordinal) ||
            x.Contains("LandIntelligence", StringComparison.Ordinal) || x.Contains("LeaseFeasibility", StringComparison.Ordinal) ||
            x.EndsWith(".Api", StringComparison.Ordinal) || x.Contains("Infrastructure", StringComparison.Ordinal) || x.Contains("Presentation", StringComparison.Ordinal));
    }
    [Fact]
    public void DirectoryAdapter_IsOwnedByCompositionHost()
    {
        Assert.Equal("StateLandGovernance.Api", typeof(StateLandGovernance.Api.Security.CentralComplaintDirectory).Assembly.GetName().Name);
        Assert.Contains(typeof(StateLandGovernance.GovernanceIntelligence.Application.Complaints.IComplaintOfficerDirectory),
            typeof(StateLandGovernance.Api.Security.CentralComplaintDirectory).GetInterfaces());
    }
    [Fact]
    public void FreshCentralPermissionSource_IsOwnedByCompositionHost()
    {
        Assert.Equal("StateLandGovernance.Api",typeof(StateLandGovernance.Api.Security.CentralComplaintGrantFile).Assembly.GetName().Name);
        Assert.Contains(typeof(StateLandGovernance.GovernanceIntelligence.Application.Complaints.IComplaintCentralGrantSource),typeof(StateLandGovernance.Api.Security.CentralComplaintGrantFile).GetInterfaces());
    }
}
