namespace StateLandGovernance.LandIntelligence.Application.Security;

public static class LandIntelligenceAuth
{
    public static class Schemes
    {
        public const string JwtBearer = "Bearer";
        public const string DemoOwner = "LandIntelligence.DemoOwner";
    }

    public static class Roles
    {
        public const string Applicant = "land_intelligence_applicant";
        public const string Officer = "land_intelligence_officer";
        public const string InternalMaintainer = "land_intelligence_internal";
    }

    public static class Policies
    {
        public const string Applicant = "LandIntelligence.Applicant";
        public const string Officer = "LandIntelligence.Officer";
        public const string InternalMaintainer = "LandIntelligence.InternalMaintainer";
    }

    public static class ClaimTypes
    {
        public const string OwnerId = "land_intelligence_owner_id";
    }
}
