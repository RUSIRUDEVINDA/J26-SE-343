export const investigationConcerns=[
 ["LegalDispute","Legal Dispute"],["UnauthorizedOccupation","Unauthorized Occupation"],["UnauthorizedConstruction","Unauthorized Construction"],["FamilyOrInheritanceClaim","Family or Inheritance Claim"],["MultipleClaimants","Multiple Claimants"],["UnresolvedObjection","Unresolved Objection"],["PreviousIllegalLandActivity","Previous Illegal Land Activity"]] as const;
export type InvestigationChoice="IssueFound"|"NoIssueFound"|"NotYetVerified";
export type InvestigationObservation={choice:InvestigationChoice;remarks:string};
export function investigationError(value:InvestigationObservation){return value.choice==="IssueFound"&&(value.remarks.trim().length<20||value.remarks.trim().split(/\s+/).length<3)?"Describe the issue and what you observed (at least 20 characters and three words).":"";}
/** Draft preview only: an officer observation cannot establish independently verified evidence. */
export function conservativeEvidenceState(value:InvestigationObservation):"Unverified"|"Missing" {return value.choice==="NotYetVerified"&&!value.remarks.trim()?"Missing":"Unverified";}
