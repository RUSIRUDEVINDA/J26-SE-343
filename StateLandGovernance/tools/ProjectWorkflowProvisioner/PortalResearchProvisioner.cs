using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.Shared.Infrastructure.Configuration;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

internal static class PortalResearchProvisioner
{
 public static async Task RunAsync(bool reviewFixture = false)
 {
  // Explicit operator command only. Never called by host startup; creates no C4 result or real-case review.
  EnvFileLoader.LoadFromRepositoryRoot();
  var connection=Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION")??throw new InvalidOperationException("Missing C3 connection");
  var target=new Npgsql.NpgsqlConnectionStringBuilder(connection);
  if(target.Host is not ("localhost" or "127.0.0.1") || target.Database!="state_land_governance")throw new InvalidOperationException("Local Development database only.");
  var projectRoot=Path.GetFullPath("StateLandGovernance");
  using var config=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(projectRoot,"src/Api/development-accounts.local.json")));
  var account=config.RootElement.GetProperty("LocalDevelopmentSignIn").GetProperty("Accounts").EnumerateArray().Single(a=>a.GetProperty("Username").GetString()==(reviewFixture ? "reviewapplicant" : "research"));
  var caseId=Guid.Parse(reviewFixture ? "de4c0000-0000-4000-8000-000000000021" : "de4c0000-0000-4000-8000-000000000001"); var docId=Guid.Parse(reviewFixture ? "de4c0000-0000-4000-8000-000000000024" : "de4c0000-0000-4000-8000-000000000004");var actor=Guid.Parse(account.GetProperty("ActorId").GetString()!);
  var sc=new ServiceCollection();sc.AddLogging();sc.AddWorkflowGovernanceApplication().AddWorkflowGovernanceInfrastructure(true).AddWorkflowGovernancePostgresPersistence(connection);
  sc.AddWorkflowGovernanceLocalDocumentStorage(configureOptions:o=>o.RootPath=Path.Combine(projectRoot,"src/Api/.local-data/workflow-governance"));
  using var sp=sc.BuildServiceProvider();using var scope=sp.CreateScope();var services=scope.ServiceProvider;
  var cases=services.GetRequiredService<ILeaseCaseRepository>();
  if(await cases.GetByIdAsync(new(caseId)) is not null){Console.WriteLine("Separate Development research case already exists; preserved without replacement.");return;}
  var db=services.GetRequiredService<WorkflowGovernanceDbContext>();await using var tx=await db.Database.BeginTransactionAsync();
  var now=DateTime.UtcNow;const string label="DEVELOPMENT RESEARCH FIXTURE ONLY; synthetic source, not OCR/Gemini, not an actual applicant or approval";
  var caseAuthority=new VerifiedAuthorityContext(actor,["LeaseInitiator","DocumentSubmitter","ProposalReviewer"],"LeaseCase",caseId.ToString("D"),now.AddMinutes(-1),now,now.AddHours(1));
  var c=new LeaseCase(new(caseId),reviewFixture ? "DEVELOPMENT-UI-REVIEW-001" : "DEVELOPMENT-C4-RESEARCH-001",actor,now,caseAuthority.ToDomain());await cases.AddAsync(c);
  var facts=new Dictionary<string,string>{["applicant_name"]="DEVELOPMENT SYNTHETIC APPLICANT",["proposed_land_use"]="Agriculture",["land_extent"]="51",["land_extent_unit"]="acres",["requested_lease_period"]="30 years",["investment_amount"]="1000000",["investment_currency"]="LKR",["district"]="DEVELOPMENT SYNTHETIC DISTRICT",["divisional_secretariat_division"]="DEVELOPMENT SYNTHETIC DS"};
  var lines=new[]{label}.Concat(facts.Select(f=>$"SYNTHETIC FIXTURE {f.Key}={f.Value}")).ToArray();var bytes=Pdf(lines);var version=Guid.NewGuid();
  var receipt=await services.GetRequiredService<IDocumentContentWriter>().WriteAsync(new MemoryStream(bytes),"DEVELOPMENT_SYNTHETIC_RESEARCH.pdf","application/pdf",docId,version);
  var document=new GovernedDocument(new(docId),c.Id,"Proposal",new(version),new(receipt.ChecksumAlgorithm,receipt.ChecksumValue),new(receipt.ContentReference),receipt.OriginalFileName,receipt.MediaType,receipt.FileSizeInBytes,actor,now,caseAuthority.ToDomain());
  await services.GetRequiredService<IGovernedDocumentRepository>().AddAsync(document);
  var analysis=new DocumentAnalysis(new(Guid.NewGuid()),document.Id,document.ActiveVersionId,document.Versions.Single().Checksum,1,1,now);
  var runId=new AnalysisRunId(Guid.NewGuid());var resultId=new AnalysisRunResultId(Guid.NewGuid());
  analysis.RequestRun(runId,new("EXPLICIT_DEVELOPMENT_FIXTURE","Not OCR or Gemini","1"),[new("SYNTHETIC_FIXTURE")],now);analysis.StartRun(runId,now);
  var artifactKey=$"analysis-artifacts/{version:D}/{runId.Value:D}/development-fixture.txt";
  var artifactReceipt=await services.GetRequiredService<IAnalysisArtifactWriter>().WriteAsync(artifactKey,"text/plain",Encoding.UTF8.GetBytes(string.Join("\n",lines)));
  var artifact=new AnalysisResultArtifactReference(new(Guid.NewGuid()),"DEVELOPMENT_SYNTHETIC_SOURCE",artifactKey,"text/plain",new("SHA-256",artifactReceipt.ChecksumValue));
  var inputs=facts.Select(f=>new ExtractedFactInput(new(Guid.NewGuid()),new(f.Key),new(AnalysisFactValueKind.Text,f.Value),new(1m),new(artifact.Id,1,$"SYNTHETIC FIXTURE {f.Key}={f.Value}"))).ToArray();
  analysis.CompleteRun(runId,resultId,AnalysisResultOutcome.OutputsProduced,[artifact],inputs,now);
  var docAuthority=caseAuthority with{Capabilities=["FactVerifier","FactSnapshotPublisher"],ScopeKind="GovernedDocument",ScopeTargetIdentifier=docId.ToString("D")};
  if (!reviewFixture) {
  foreach(var fact in inputs)analysis.RecordFactVerification(new(Guid.NewGuid()),resultId,fact.Id,FactVerificationDecision.Confirmed,null,null,actor,now,docAuthority.ToDomain());
  analysis.PublishVerifiedFactSnapshot(new(Guid.NewGuid()),resultId,actor,now,docAuthority.ToDomain());c.UpdateCurrentVerifiedFactSnapshot(analysis.VerifiedFactSnapshots.Single().Id.Value);
  }
  await services.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(analysis);await services.GetRequiredService<IWorkflowGovernanceUnitOfWork>().CommitAsync();
  if (reviewFixture) { await tx.CommitAsync(); Console.WriteLine(JsonSerializer.Serialize(new { label, caseId, documentId=docId, version, reference=c.ApplicationReference, reviewsCreated=false, snapshotCreated=false, realCaseModified=false })); return; }
  var requirement=new DocumentRequirementSnapshot(new(Guid.NewGuid()),new("Proposal"),RequirementCriticality.Mandatory,RequirementApplicability.Required,1,"DEVELOPMENT_FIXTURE_ONLY",label);
  var writer=services.GetRequiredService<CompletenessConfigurationWriter>();await writer.PublishDocumentsAsync("DEV-RESEARCH-DOCUMENTS","1",label,actor,[requirement]);
  var binding=new AssessedDocumentBinding(document.Id,document.ActiveVersionId,document.Versions.Single().Checksum);
  var classification=new ClassifiedDocument(new(Guid.NewGuid()),binding,new("Proposal"),new(1m),DocumentClassificationStatus.Accepted,runId,resultId,new("EXPLICIT_DEVELOPMENT_FIXTURE","Not OCR or Gemini","1"),now);
  await services.GetRequiredService<IDocumentCompletenessAssessmentRepository>().AddAsync(new(new(Guid.NewGuid()),c.Id,"DEV-RESEARCH-DOCUMENTS","1",[binding],[classification],[requirement],now));
  await services.GetRequiredService<IWorkflowGovernanceUnitOfWork>().CommitAsync();
  var template=new ProposalTemplate(new("DEV-RESEARCH-TEMPLATE"),"1",label,label,ProposalTemplateStatus.Active,[new(new("DEVELOPMENT_CONTENT"),ProposalRequirementKind.Section,label,true,new("DEV-RESEARCH-TEMPLATE"),"1",label,1)]);
  await writer.PublishTemplateAsync(template,actor);
  var p=await services.GetRequiredService<ICommandHandler<AssessProposalContentCommand,ProposalContentAssessmentDto>>().HandleAsync(new(caseId,docId,version,receipt.ChecksumAlgorithm,receipt.ChecksumValue,"DEV-RESEARCH-TEMPLATE","1",runId.Value.ToString("D"),[new("DEVELOPMENT_CONTENT","Present",1,label,artifactKey,runId.Value.ToString("D"))],c.Revision));
  await services.GetRequiredService<ICommandHandler<ConfirmProposalContentAssessmentCommand,ProposalContentAssessmentDto>>().HandleAsync(new(caseId,p.Id,actor,caseAuthority,c.Revision,label));
  await tx.CommitAsync();Console.WriteLine(JsonSerializer.Serialize(new{label,caseId,documentId=docId,version,analysisId=analysis.Id.Value,snapshotId=c.CurrentVerifiedFactSnapshotId,reference=c.ApplicationReference,realCaseModified=false,c4AssessmentCreated=false}));
 }
 private static byte[] Pdf(IEnumerable<string> lines)
 {
  var text="BT /F1 8 Tf 35 790 Td 14 TL "+string.Join(" ",lines.Select(l=>"("+l.Replace("(","[").Replace(")","]")+") Tj T*"))+" ET";
  var objects=new[]{"<< /Type /Catalog /Pages 2 0 R >>","<< /Type /Pages /Kids [3 0 R] /Count 1 >>","<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>","<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",$"<< /Length {text.Length} >>\nstream\n{text}\nendstream"};
  var b=new StringBuilder("%PDF-1.4\n");var offsets=new List<int>();for(var i=0;i<objects.Length;i++){offsets.Add(b.Length);b.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n");}var xref=b.Length;b.Append("xref\n0 6\n0000000000 65535 f \n");foreach(var o in offsets)b.Append(o.ToString("D10")+" 00000 n \n");b.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");return Encoding.ASCII.GetBytes(b.ToString());
 }
}
