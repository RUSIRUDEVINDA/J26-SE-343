using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.Api.Security;
public sealed class CommissionerReportGrantFile(IOptionsSnapshot<CommissionerReportReadOptions> options):ICommissionerReportGrantSource
{
 public Task<CentralComplaintGrant[]> ReadAsync(CancellationToken ct)=>new CentralComplaintGrantFile(new(){GrantFilePath=options.Value.GrantFilePath}).ReadAsync(ct);
}
