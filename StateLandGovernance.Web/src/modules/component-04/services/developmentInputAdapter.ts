import {parseWorkflowAnomalyRequest,type WorkflowAnomalyRequest} from "./governanceService";
export const INPUT_FIXTURE_VERSION="C4-INDEPENDENT-INPUTS-1";
export const scenarioOptions=[
 {id:"complete",label:"Complete supplied input / no supplied conflicts"},
 {id:"overlap",label:"Overlap and concurrent-application evidence"},
 {id:"complaint",label:"Synthetic reviewed complaint / governance concerns"},
 {id:"incomplete",label:"Incomplete evidence / unresolved inputs"},
 {id:"ordinary-workflow",label:"Completed workflow / ordinary recorded events"},
 {id:"revised-workflow",label:"Completed workflow / delays and decision revisions"},
] as const;
export type InputScenario=typeof scenarioOptions[number]["id"];
const fixtureId=(n:number)=>`da4c0000-0000-4000-8000-${String(n).padStart(12,"0")}`;
export function buildDevelopmentInput(caseId:string,scenario:InputScenario){
 if(!caseId)throw new Error("Select an authorized designated synthetic case. Fixture identifiers do not grant access.");
 const concern=scenario==="complaint",overlap=scenario==="overlap",incomplete=scenario==="incomplete",revised=scenario==="revised-workflow";
 const at="2030-01-01T08:00:00.000Z";const snapshotId=fixtureId(1),documentId=fixtureId(2),versionId=fixtureId(3),actor=fixtureId(4);
 const values:{code:string;value:string;kind:string}[]=[{code:"applicant_name",value:"Synthetic Research Individual",kind:"Text"},{code:"proposed_land_use",value:"Agriculture",kind:"Text"},{code:"district",value:"DEVELOPMENT_MOCK_DISTRICT",kind:"Text"},{code:"requested_lease_period",value:"15",kind:"Integer"},{code:"investment_amount",value:"12000000",kind:"Decimal"},{code:"investment_currency",value:"LKR",kind:"Text"},{code:"land_extent",value:"12",kind:"Decimal"},{code:"land_extent_unit",value:"Acre",kind:"Text"}];
 const verifiedSnapshot={id:snapshotId,documentAnalysisId:fixtureId(5),governedDocumentId:documentId,documentVersionId:versionId,
  checksumAlgorithm:"SHA-256",checksumValue:"a".repeat(64),analysisRunId:fixtureId(6),analysisRunResultId:fixtureId(7),runNumber:1,resultOutcome:"OutputsProduced",snapshotOutcome:"VerifiedFacts",
  entries:values.map((v,i)=>({sourceExtractedFactId:null,verificationId:fixtureId(100+i),factCode:v.code,effectiveValue:{kind:v.kind,canonicalValue:v.value},decision:"Confirmed",verifyingActorId:actor,verifiedAt:at,sourceKind:"HumanDocumentEvidence",
   humanEvidence:{observationId:fixtureId(200+i),documentId,versionId,checksum:"a".repeat(64),pageNumber:1,quote:`Synthetic fixture source: ${v.code}=${v.value}`,reviewReason:"SIMULATED human-source review in an input fixture; not OCR or a real authorized publication.",actorId:actor,recordedAtUtc:at}})),
  sourceFactCount:0,publishedFactCount:values.length,confirmedFactCount:values.length,correctedFactCount:0,unsupportedFactCount:0,publishedAt:at,publishingActorId:actor};
 const screeningInput={caseId,applicationReference:"SYNTHETIC-INPUT-ONLY",proposalVersion:versionId,verifiedFactSnapshotId:snapshotId,snapshotTimestampUtc:at,parcelId:fixtureId(8),cadastralNumber:"DEVELOPMENT-MOCK-12A-001",applicantName:"Synthetic Research Individual",proposedUse:"Agriculture",extentHectares:4.85622770688,leaseDurationYears:15,investmentAmount:12000000,investmentCurrency:"LKR",zoningArea:null,district:"DEVELOPMENT_MOCK_DISTRICT",divisionalSecretariat:"DEVELOPMENT_MOCK_DS",province:"DEVELOPMENT_MOCK_PROVINCE_A",
  documentReferences:[{documentType:"Proposal",documentTitle:"SYNTHETIC INPUT FIXTURE - no extracted provenance",storageReference:"development-input-fixture://proposal/v1",sourcePageNumber:1,extractedFactCode:"district"}],
  verifiedLegalDisputeFlag:concern,unauthorizedOccupationFlag:concern,objectionReceivedFlag:concern,familyOrInheritanceClaimFlag:false,documentedPriorActivityFlag:false,
  contemporaneousComplaints:concern?[{complaintId:fixtureId(9),category:"LandDispute",summary:"SYNTHETIC reviewed test evidence of a disputed boundary.",verificationStatus:"Verified",receivedAtUtc:at}]:[],
  spatialEvidence:incomplete?null:{parcelId:fixtureId(8),cadastralNumber:"DEVELOPMENT-MOCK-12A-001",extentHectares:4.85622770688,province:"DEVELOPMENT_MOCK_PROVINCE_A",district:"DEVELOPMENT_MOCK_DISTRICT",divisionalSecretariat:"DEVELOPMENT_MOCK_DS",gramaNiladhariDivision:"DEVELOPMENT_MOCK_GN_001",coordinateSystem:"EPSG:4326",centroidLatitude:7,centroidLongitude:80,boundaryGeoJson:null,spatialConstraints:[],adjacentLeases:[],hasOverlappingParcels:overlap,overlapDetails:overlap?"Synthetic overlap supplied as input, not a real spatial search":null},
  concurrentApplications:overlap?[{concurrentCaseId:fixtureId(10),applicantName:"Synthetic Other Individual",proposedUse:"Agriculture",submittedAtUtc:at,workflowStatus:"Submitted"}]:[],evidenceVersion:INPUT_FIXTURE_VERSION+":"+scenario,isDevelopmentSpatialMock:true};
 const history=revised?[
  ["DS:Started","DS",0,"Started","Synthetic intake"],["DS:Approved","DS",2,"Approved","Synthetic review"],
  ["PROVINCIAL_LC:Started","PROVINCIAL_LC",5,"Started","Synthetic assigned request"],["PROVINCIAL_LC:Rejected","PROVINCIAL_LC",30,"Rejected","Synthetic rejection reason"],
  ["PROVINCIAL_LC:Approved","PROVINCIAL_LC",75,"Approved","Governed synthetic decision revision preserving prior rejection"],["PROVINCIAL_LC:Approved","PROVINCIAL_LC",95,"Approved","Further governed synthetic decision revision with updated reason"],
  ["COMMISSIONER:Started","COMMISSIONER",108,"Started","Synthetic assigned final review"],
  ["COMMISSIONER:Approved","COMMISSIONER",110,"Approved","Synthetic final decision"],["DS Final Notification","DS",115,"Notified","Separate simulated DS notification evidence; not a renamed Commissioner decision"]
 ]:[
  ["DS:Started","DS",0,"Started","Synthetic intake"],["DS:Approved","DS",2,"Approved","Synthetic review"],["PROVINCIAL_LC:Started","PROVINCIAL_LC",3,"Started","Synthetic assigned review"],
  ["PROVINCIAL_LC:Approved","PROVINCIAL_LC",7,"Approved","Synthetic decision"],["COMMISSIONER:Started","COMMISSIONER",8,"Started","Synthetic assigned final review"],["COMMISSIONER:Approved","COMMISSIONER",9,"Approved","Synthetic final decision"],["DS Final Notification","DS",10,"Notified","Separate simulated DS notification source"]
 ];
 const annotations=history.map((h,i)=>({eventSequence:i+1,stage:String(h[0]).split(":")[0],institution:String(h[1]),decision:["Approved","Rejected","ApprovedWithConditions"].includes(String(h[3]))?String(h[3]):null,reason:String(h[4]),revision:revised&&i>=4?2:1}));
 const postWorkflowTrace={caseId,executionId:fixtureId(revised?31:30),revision:revised?2:1,inputFingerprint:"UNBOUND-FIXTURE-"+INPUT_FIXTURE_VERSION+":"+scenario,
  events:history.map((h,i)=>({sequence:i+1,activity:String(h[0]),institution:String(h[1]),actor:fixtureId(400+(String(h[1])==="DS"?1:String(h[1])==="PROVINCIAL_LC"?2:3)),atUtc:new Date(Date.parse(at)+Number(h[2])*86400000).toISOString()})),isDevelopmentSpatialMock:true};
 const indicators=["LegalDispute","UnauthorizedOccupation","UnauthorizedConstruction","FamilyOrInheritanceClaim","MultipleClaimants","UnresolvedObjection","PreviousIllegalLandActivity"].map((indicatorType,i)=>({indicatorType,evidenceState:incomplete?"Unavailable":concern&&i<2?"VerifiedPresent":"VerifiedAbsent",evidenceReference:incomplete?null:"SYNTHETIC-FIXTURE-REVIEW:"+INPUT_FIXTURE_VERSION,recordedAtUtc:at}));
 return {provenance:{kind:"DevelopmentInputFixture",version:INPUT_FIXTURE_VERSION,scenario,synthetic:true,notOcrOrGemini:true,timestampSemantics:"UTC fixture instants; model naive timestamps explicitly retain UTC wall-clock semantics",notice:"Input simulation only. No engine outputs, real authority grants or real evidence clearance are included."},verifiedSnapshot,screeningInput:incomplete?null:screeningInput,missingInputs:incomplete?["Spatial evidence is unavailable", "Governance coverage is unknown; the consolidated boolean contract cannot serialize unknown as false"]:[],postWorkflowTrace,historyAnnotations:annotations,indicators,
  complaintText:"SYNTHETIC RESEARCH TEST - NOT A REAL COMPLAINT. A fictional occupant has erected a temporary fence on the fictional test land without permission. This is classifier input, not a verified allegation."};
}
export function toAnomalyRequest(trace:ReturnType<typeof buildDevelopmentInput>["postWorkflowTrace"]):WorkflowAnomalyRequest{
 if(trace.events.some(e=>!e.atUtc.endsWith("Z")||Number.isNaN(Date.parse(e.atUtc))))throw new Error("C3 AtUtc events require explicit valid UTC instants; no timezone is inferred.");
 if(trace.events.at(-1)?.activity!=="DS Final Notification")throw new Error("Missing DS Final Notification; Commissioner approval is not that event.");
 return parseWorkflowAnomalyRequest({caseId:trace.caseId,events:trace.events.map(e=>({eventSeq:e.sequence,activity:e.activity,institution:e.institution,resource:e.actor,timestamp:e.atUtc.replace(/Z$/,"")}))});
}
