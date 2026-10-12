using StateLandGovernance.LandIntelligence.Domain.Enums;



namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;



internal static class DeterministicRecommendationScenarioExpectations

{

    public static IReadOnlyList<ScenarioExpectation> SharedExpectations(string prefix) =>

    [

        new($"{prefix}-A", ExpectedRank: 1, ExpectedExcluded: false,

            "High suitability: exact purpose, area, location, soil, no restrictions."),

        new($"{prefix}-E", ExpectedRank: 2, ExpectedExcluded: false,

            "Far road distance no longer filters ranking; ties with high suitability and sorts by cadastral number."),

        new($"{prefix}-B", ExpectedRank: 3, ExpectedExcluded: false,

            "Medium suitability: same province, moderate spatial constraint."),

        new($"{prefix}-I", ExpectedRank: 4, ExpectedExcluded: false,

            "Regulatory complexity: five gazette references exceed configured limit."),

        new($"{prefix}-H", ExpectedRank: 5, ExpectedExcluded: false,

            "Missing data: soil characteristic absent so custom criterion fails."),

        new($"{prefix}-C", ExpectedRank: 6, ExpectedExcluded: false,

            "Low suitability: high-severity spatial constraint reduces score."),

        new($"{prefix}-D", ExpectedRank: 7, ExpectedExcluded: false,

            "Environmental restriction: prohibitive wetland triggers hard constraint rejection."),

        new($"{prefix}-F", ExpectedRank: null, ExpectedExcluded: true,

            "Wrong land use: filtered out by required land use search constraint."),

        new($"{prefix}-G", ExpectedRank: null, ExpectedExcluded: true,

            "Wrong location: outside preferred Western province search filter.")

    ];



    public static readonly IReadOnlyDictionary<LandUseType, string> PrefixByPurpose =

        new Dictionary<LandUseType, string>

        {

            [LandUseType.Agricultural] = DeterministicRecommendationScenarioFactory.AgriculturalPrefix,

            [LandUseType.Residential] = DeterministicRecommendationScenarioFactory.ResidentialPrefix,

            [LandUseType.Commercial] = DeterministicRecommendationScenarioFactory.CommercialPrefix

        };

}


