using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class DevelopmentCatalogueSourceMetadataTests
{
 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public async Task ImportedLabelsAreNotInventedPagesOrCryptographicChecksums(bool blocking)
 {
  using var db=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  var from=new DateTime(2019,1,1,0,0,0,DateTimeKind.Utc); var to=from.AddYears(20);
  var source=new RegulatorySourceEntity {Id=Guid.NewGuid(),SourceType="GovernmentOperationalManual",Authority="UNIT-TEST-AUTHORITY",DocumentTitle="UNIT-TEST-DOC",DocumentVersion="January 2019",DocumentReference="UNIT-TEST-REF",Checksum="2019.1",PublishedDate=from.AddYears(-1),EffectiveFrom=from,EffectiveTo=to};
  db.ComplianceRules.Add(new ComplianceRuleEntity {Id=Guid.NewGuid(),RuleCode="NPD-LOC-001",RuleVersion="1.0",Source=source,SourceId=source.Id,SourceSection="UNIT-TEST-SECTION",Category="Completeness",CalculationKey="ProjectLocationCheck",Severity="Low",Enabled=true,IsBlocking=blocking});
  await db.SaveChangesAsync();
  var rule=(await new PostgresComplianceRuleCatalogue(db).GetActiveRulesAsync(DateTime.UtcNow,default)).Single();
  Assert.Null(rule.SourceReference.SourcePage);
  Assert.Equal("January 2019",rule.SourceReference.SourceVersion);
  Assert.Equal("UNIT-TEST-REF",rule.SourceReference.SourceDocumentReference);
  Assert.Equal("2019.1",rule.SourceReference.SourceChecksumLabel);
  Assert.Equal(from,rule.SourceReference.EffectiveFrom);Assert.Equal(to,rule.SourceReference.EffectiveTo);
  var finding=StateLandGovernance.GovernanceIntelligence.Domain.Services.NpdRuleCatalogue.EvaluateAll(
      new StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects.ProposalComplianceInput("UNIT-TEST"),[rule]).Single();
  Assert.Equal("UNIT-TEST-SECTION",finding.SourceReference.SourceSection);
  Assert.Equal(blocking,finding.IsBlocking);
  Assert.Equal("Low",finding.Severity);
  Assert.Equal(rule.RuleVersion,finding.RuleVersion);
 }
}
