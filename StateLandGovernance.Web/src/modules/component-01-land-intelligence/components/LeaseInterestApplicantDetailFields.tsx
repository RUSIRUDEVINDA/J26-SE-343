import type { LeaseInterestApplicant } from "../types/landIntelligence";

function displayOrNotEntered(value: string | null | undefined): string {
  const trimmed = value?.trim();
  return trimmed ? trimmed : "Not entered";
}

export function LeaseInterestApplicantDetailFields({
  applicant,
}: {
  applicant: LeaseInterestApplicant;
}) {
  const isOrganization = applicant.kind === 2;

  return (
    <>
      <div>
        <dt>Applicant</dt>
        <dd>{displayOrNotEntered(applicant.legalName)}</dd>
      </div>
      <div>
        <dt>Mobile</dt>
        <dd>{displayOrNotEntered(applicant.contactPhone)}</dd>
      </div>
      <div>
        <dt>Email</dt>
        <dd>{displayOrNotEntered(applicant.contactEmail)}</dd>
      </div>
      <div>
        <dt>Postal address</dt>
        <dd>{displayOrNotEntered(applicant.postalAddress)}</dd>
      </div>
      {isOrganization ? (
        <>
          <div>
            <dt>Organisation</dt>
            <dd>{displayOrNotEntered(applicant.organizationName)}</dd>
          </div>
          <div>
            <dt>BRN</dt>
            <dd>{displayOrNotEntered(applicant.organizationRegistrationNumber)}</dd>
          </div>
          <div>
            <dt>Representative NIC</dt>
            <dd>
              {displayOrNotEntered(applicant.representativeNationalIdentityCardNumber)}
            </dd>
          </div>
        </>
      ) : (
        <div>
          <dt>NIC</dt>
          <dd>{displayOrNotEntered(applicant.nationalIdentityCardNumber)}</dd>
        </div>
      )}
    </>
  );
}
