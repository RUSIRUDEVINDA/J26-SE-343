# AI-assisted review suggestions -- human decisions pending

**File:** `synthetic_family_review_batch01.md`
**Batch:** SYN-FAM-001 to SYN-FAM-020 (first 20 families, sorted by Synthetic_Family_ID)
**Prepared:** 2026-10-04
**Branch:** `Component_04_RD_AI_ML`
**Status:** AI-assisted suggestions only. No decisions have been entered into the review pack.

---

> **IMPORTANT -- READ BEFORE USING THIS DOCUMENT**
>
> This document contains AI-generated observations and suggestions to assist a human researcher
> in making their own review decisions. Nothing in this document constitutes an approved,
> rejected, or finalized decision.
>
> - Do NOT copy suggestions from this document directly into `synthetic_family_review_pack.csv`.
> - All fields (Review_Decision, Approved_Label, Reviewer, Review_Date, Variant_Consistency,
>   Related_Baseline_IDs, Review_Notes) must be filled by the human researcher based on
>   their own independent judgment.
> - These are researcher-level review observations, not independent domain-expert or
>   professional governance validation.
> - The AI assistant does not infer verified independence from blank relationship fields.
>   Where no relationship is identified, this is stated explicitly.

---

## Dataset integrity (pre-review SHA-256)

| File | SHA-256 |
|------|---------|
| candidate-data/v1/synthetic_family_review_pack.csv | e41a6bf12b780ac5ef60f3adc07b808e20f4521c5abd21cb6496d869a3cdbf8e |
| data/state_land_complaints_development_530.csv | 78877d77963d954f7f86eb5174e54f81013d0ad18b15763153c11ccfb0c39eb8 |

These values must be confirmed **unchanged** after review decisions are recorded.

---

## How to transfer your decisions into the review pack

1. Open `candidate-data/v1/synthetic_family_review_pack.csv`.
2. Locate the row by Synthetic_Family_ID.
3. Fill **only** these columns based on your own judgment:
   - Reviewer -- your name
   - Review_Date -- today's date (YYYY-MM-DD)
   - Review_Decision -- one of: Approved / Label_Change / Rejected_Out_of_Scope /
     Rejected_Ambiguous / Rejected_Multi_Issue / Rejected_Contradictory / Needs_Discussion
   - Approved_Label -- only if Review_Decision = Label_Change
   - Variant_Consistency -- Consistent / Minor_variation / Inconsistent
   - Related_Baseline_IDs -- semicolon-separated baseline Research_IDs, or leave blank
   - Review_Notes -- any free-text observations
4. Do **not** alter Proposed_Label, variant texts, or Research IDs.
5. After saving, verify SHA-256 of the pack changed only in the decision columns.

Families with Review_Decision = Approved or Label_Change (with Approved_Label filled)
become eligible for Experiment 4 Arm B training augmentation.
Families with Related_Baseline_IDs filled will be excluded from any fold where those
baseline records appear in the evaluation split.

---

## Taxonomy reference

| Short form | Full label |
|------------|-----------|
| Admin | Administrative / Procedural / Integrity |
| Revenue | Lease Revenue / Payment / Enforcement |
| Unauthorized | Unauthorized Allocation / Transfer / Use |
| Environmental | Protected / Environmental Lease Misuse |

---

## Review suggestions -- SYN-FAM-001 through SYN-FAM-020

---

### SYN-FAM-001

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0001 | The officer handling my state-land lease application asked for cash without a receipt before moving the file forward. |
| V2 | SYN-0002 | My lease application has stopped at the desk where an officer requested an unreceipted payment to process it. |
| V3 | SYN-0003 | An applicant reports being asked to pay the reviewing officer personally before a pending government-land lease file would proceed. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe the same governance scenario -- an officer demanding an unreceipted informal payment to advance a lease application. Clear integrity irregularity within administrative processing. No revenue shortfall, no unauthorized allocation, no environmental element.

**Variant consistency assessment (suggested):** Minor_variation -- V1 and V2 are first-person; V3 is third-person. Underlying incident (informal cash demand to advance a file) is identical across all three.

**Candidate related baseline IDs and evidence:**
- WEB-081: officer requested unofficial payment to accelerate approval of a state land lease application (Kandy). Structural parallel: informal payment to advance file.
- WEB-082: payment demanded outside the official fee schedule before processing a lease renewal (Hambantota). Structural parallel.
- WEB-083: intermediary linked to an official asked LKR 450,000 to secure a favourable lease decision. Structural parallel.
- WEB-084: officer sought a personal benefit in return for recommending approval. Structural parallel.

These are thematic parallels in incident type. The same specific incident cannot be confirmed. Shared vocabulary and scenario type alone do not establish same-incident identity.

**Researcher question to resolve:** Do you consider these baseline records sufficiently similar in incident type that the synthetic family should be flagged as potentially overlapping? If so, record the relevant baseline IDs. If not, leave Related_Baseline_IDs blank.

---

### SYN-FAM-002

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0004 | An officer suggested that my state-land lease would be approved only if I agreed to a sexual favour. |
| V2 | SYN-0005 | The applicant says an official linked approval of the government-land lease to an unwanted request for sexual contact. |
| V3 | SYN-0006 | I received an unwanted sexual proposition from the official reviewing my land lease, with approval presented as conditional on accepting. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe a single integrity violation -- an officer conditioning lease approval on a sexual favour. Clear integrity/corruption scenario within the Administrative class. All three are consistent in the essential incident. No alternative label is appropriate.

**Variant consistency assessment (suggested):** Minor_variation -- V1 is brief first-person, V2 is third-person, V3 is first-person with more detail. Underlying incident is identical.

**Candidate related baseline IDs and evidence:**
- WEB-086: officer requested a sexual favour in exchange for assisting with approval of a state-land lease application (Puttalam).
- WEB-087: official implied favourable treatment would depend on the complainant providing a sexual favour.
- WEB-088: lease-renewal applicant pressured to provide a sexual favour before file would be recommended for approval.
- WEB-089: officer linked progress on a state-land allocation request to an unwanted sexual demand.

All four are structurally identical in scenario type. Independent incidents cannot be confirmed.

**Researcher question to resolve:** Given the baseline population contains multiple similar incidents (WEB-086 to WEB-089), does the researcher wish to flag these as related? If so, record those baseline IDs in the review pack.

---

### SYN-FAM-003

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0007 | After I reported an unofficial payment request, the officer threatened to cancel my state-land lease application unless I withdrew the complaint. |
| V2 | SYN-0008 | A lease applicant alleges that an official used cancellation threats to pressure them into withdrawing a complaint about that official. |
| V3 | SYN-0009 | My complaint about the lease officer was followed by a warning that my application would be rejected if I continued. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe retaliation/intimidation -- an officer threatening to cancel a lease application to suppress a complaint about themselves. Clear integrity issue (coercion/retaliation) within lease administration. No competing label.

**Variant consistency assessment (suggested):** Minor_variation -- V1 is most specific (links to a prior payment demand). V2 is third-person. V3 is shortest. Underlying incident is the same.

**Candidate related baseline IDs and evidence:**
- WEB-090: officer threatened to obstruct a lease renewal unless the applicant withdrew a complaint about irregular handling. Closest parallel.
- WEB-091: lessee pressured to sign an unfavourable document after being threatened with lease cancellation.
- WEB-092: applicant told adverse information would be disclosed unless they abandoned an objection.
- WEB-093: land user alleges intimidation by an official who threatened enforcement action unless the user stopped questioning the lease decision.

**Researcher question to resolve:** Does the researcher consider WEB-090 sufficiently similar to warrant flagging? The thematic overlap is strong but specific scenario details differ.

---

### SYN-FAM-004

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0010 | The officer assessing government-land lease applications did not disclose that the selected applicant was their business partner. |
| V2 | SYN-0011 | A competing lease applicant reports that the reviewer and the successful applicant jointly own a business, but the assessment records omit this relationship. |
| V3 | SYN-0012 | The selection file for a state parcel contains no disclosure of the reviewing officer's business partnership with the successful applicant. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe undisclosed conflict of interest -- an officer assessing a lease application without declaring a business relationship with the winning applicant. Straightforward integrity/procedural failure. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same specific situation from slightly different perspectives (first-person competitor, third-person report, file observation). No phrasing drift into a different issue.

**Candidate related baseline IDs and evidence:**
- WEB-094: lease given preferential treatment because the proposed lessee had a personal connection to an officer involved in the decision.
- WEB-095: competing lease applications not assessed consistently; related party received favourable treatment without documented justification.
- WEB-096: official participated in a lease decision involving a business linked to a close associate, without declaring a conflict of interest. Closest parallel.

**Researcher question to resolve:** Does the researcher consider WEB-096 a relevant baseline relationship to record? Scenario type is virtually identical (undisclosed business relationship, lease decision). Specific entities and geography are not stated in either.

---

### SYN-FAM-005

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0013 | The approval letter in my government-land lease file carries my signature even though I never signed that version. |
| V2 | SYN-0014 | I dispute the signature attributed to me on a revised state-land lease approval document that I did not receive. |
| V3 | SYN-0015 | A lease applicant says their signature was copied onto an amended approval letter without their knowledge or agreement. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe document forgery -- a signature placed on a lease document without the named party's knowledge or consent. Clear document-integrity failure. Scenario is consistent. No competing label.

**Variant consistency assessment (suggested):** Minor_variation -- V1 and V2 are first-person; V3 is third-person. V2 adds the applicant did not receive the revised document. All three describe the same core incident (forged/copied signature).

**Candidate related baseline IDs and evidence:**
- WEB-102: lease file contained a certificate stating an inspection was completed even though no inspection reportedly occurred. Broader parallel: fabricated documentation.
- WEB-103: dates in a land-lease approval record were altered. Broader parallel: document record alteration.
- WEB-104: supporting document in a lease file contained information not matching the official land register. Broader parallel.

None describes signature forgery specifically. These are thematic parallels at the document-falsification level only.

**Researcher question to resolve:** Is the specific scenario (signature copied/forged) sufficiently distinct from WEB-102 to WEB-104 that no baseline relationship should be recorded? Or does the researcher consider the shared document-falsification pattern sufficient to flag?

---

### SYN-FAM-006

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0016 | The inspection report for a state-land lease was dated before the visit, and the listed officer was absent on that date. |
| V2 | SYN-0017 | A lease review relies on an inspection dated last month, although the site visit occurred yesterday and involved different staff. |
| V3 | SYN-0018 | The recorded date and officer for the lease-site inspection do not match the visit witnessed by the applicant. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe the same record-falsification scenario -- inspection records showing a wrong date and wrong officer. Clear administrative record-integrity failure. No competing label.

**Variant consistency assessment (suggested):** Consistent -- V1 and V3 note both date and officer discrepancies; V2 notes date and staff discrepancy. The underlying incident is the same.

**Candidate related baseline IDs and evidence:**
- WEB-102: lease file contained a certificate stating an inspection was completed even though no inspection reportedly occurred. Structural parallel: false inspection documentation.
- WEB-103: dates in a land-lease approval record were altered. Structural parallel: date falsification.

SYN-FAM-006 combines both elements (false date and false officer name). These are thematic parallels; the same specific incident is not confirmed.

**Researcher question to resolve:** The combination of false date and officer name makes this a compound falsification scenario. Does the researcher wish to flag WEB-102 and WEB-103 as related baseline references?

---

### SYN-FAM-007

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0019 | The case checklist required another institution's concurrence before approving the state-land lease, but approval was recorded while that response was still pending. |
| V2 | SYN-0020 | A lease file was marked approved despite its own checklist showing an outstanding institutional clearance required before the decision. |
| V3 | SYN-0021 | The applicant received approval although the government-land lease checklist still showed an unreceived concurrence as a prerequisite. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe the same procedural failure -- a lease approved before a mandatory inter-institutional clearance was received, as evidenced by the file's own checklist. Procedural compliance failure. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same scenario (approval before required clearance received), with only phrasing differences. No variant drifts.

**Candidate related baseline IDs and evidence:**
- WEB-098: land leased without obtaining required approval or completing the prescribed competitive selection process (Kalutara). Structural parallel.
- WEB-099: lease file proceeded despite missing mandatory approval from the competent authority. Closest parallel.
- WEB-100: long-term lease issued before required valuation and formal approval steps were completed. Structural parallel.

**Researcher question to resolve:** Should WEB-098, WEB-099, or WEB-100 be flagged as related? The procedural failure type is closely aligned. Specific entities and geography differ.

---

### SYN-FAM-008

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0022 | The selection score for my state-land lease application changed after assessment, with no explanation or signed amendment in the file. |
| V2 | SYN-0023 | Two copies of the lease assessment show different scores for the same application, and the decision log contains no authorized revision. |
| V3 | SYN-0024 | My government-land lease assessment was downgraded between versions without any recorded reason or reviewer sign-off. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe unauthorized alteration of assessment scores without documented authority -- clear integrity failure in the evaluation process. Scenario is coherent and consistent. No competing label.

**Variant consistency assessment (suggested):** Consistent -- V1 (score changed, first-person), V2 (two conflicting copies, third-person), V3 (score downgraded, first-person). All describe unexplained score changes without authorization. No drift.

**Candidate related baseline IDs and evidence:**
- WEB-103: dates in a land-lease approval record were altered. Broader parallel: record alteration (different element altered -- dates vs. scores).
- WEB-105: lease recommendation supported by an inaccurate certification. Broader parallel.

No baseline record specifically describes score manipulation in an assessment.

**Researcher question to resolve:** Should the researcher record any baseline relationship (document-integrity level), or leave blank as the specific scenario appears novel?

---

### SYN-FAM-009

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0025 | I have an acknowledgment for the documents submitted with my state-land lease request, but the office says they were never received. |
| V2 | SYN-0026 | The lease office is treating my application as incomplete even though its receipt lists the documents now missing from the file. |
| V3 | SYN-0027 | Documents acknowledged by the government-land lease office cannot be located, and the applicant has been asked to submit everything again. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe the same administrative integrity failure -- submitted documents acknowledged by receipt are subsequently claimed to be missing. Clear document-handling/records integrity issue. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same situation (acknowledged documents now missing) from different perspectives. No drift.

**Candidate related baseline IDs and evidence:**
No baseline record specifically describes acknowledged documents subsequently claimed missing.

Statement: No relationship identified by this review; independence not established.

**Researcher question to resolve:** If the researcher identifies any baseline records describing document disappearance or denial of receipt, those should be flagged.

---

### SYN-FAM-010

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0028 | The officer demanded the application processing fee again despite my receipt and the ledger confirming payment for the same lease application. |
| V2 | SYN-0029 | My state-land lease file is being withheld until I repay a processing charge already recorded as settled in the office ledger. |
| V3 | SYN-0030 | An applicant presents matching receipt and ledger evidence, but an official still insists on a second processing fee for the same application. |

**Suggested decision:** Needs_Discussion
**Suggested category:** Primary -- Administrative / Procedural / Integrity; Secondary -- Lease Revenue / Payment / Enforcement

**Rationale:** All three variants describe a double-payment demand despite documented settlement. The primary character is integrity/corruption (exploiting fee processes to extort a second payment). However, a Revenue / Payment / Enforcement reading is also defensible. The proposed label (Administrative) is reasonable but the researcher should confirm which governance dimension is primary before approving.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same scenario (double fee demand despite confirmed payment) with only minor phrasing differences.

**Candidate related baseline IDs and evidence:**
WEB-081 to WEB-085 all describe informal payment demands in exchange for lease processing. SYN-FAM-010 involves a formal fee, not an informal demand. The coercion pattern is thematically related but the mechanism differs.

Statement: No exact match identified; independence not established.

**Researcher question to resolve:** Is this primarily an integrity scenario (officer exploiting the fee process) or a revenue scenario (fee collection failure)? The researcher's judgment on the primary governance dimension determines the appropriate label.

---

### SYN-FAM-011

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0031 | Details from my confidential government-land lease application were sent to a competing applicant without my permission or a recorded disclosure reason. |
| V2 | SYN-0032 | A competitor received the applicant's private financial documents from the state-land lease file, and no authorized release is recorded. |
| V3 | SYN-0033 | I learned that the lease office shared my application attachments with a competing applicant without explaining the authority for doing so. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe unauthorized disclosure of confidential lease application materials to a competitor -- clear administrative integrity failure (confidentiality breach). Scenario is coherent and consistent. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same incident (confidential materials leaked to a competitor) from slightly different angles. No drift.

**Candidate related baseline IDs and evidence:**
No baseline record in the reviewed set describes unauthorized disclosure of confidential application documents to competing applicants.

Statement: No relationship identified by this review; independence not established.

**Researcher question to resolve:** Does the researcher wish to search the full baseline for records involving confidentiality breaches in competitive lease selection? If found, those IDs should be recorded.

---

### SYN-FAM-012

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0034 | Meeting minutes for a state-land lease decision omit an objection about the chair's relationship with the selected applicant. |
| V2 | SYN-0035 | The signed lease review minutes do not contain the conflict-of-interest objection that attendees say was raised during the meeting. |
| V3 | SYN-0036 | A participant reports that an objection concerning the selection chair's personal connection was removed from the government-land lease decision minutes. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe suppression of a conflict-of-interest objection from meeting minutes -- integrity failure combining minute falsification with conflict-of-interest concealment. Coherent and consistent. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same incident (objection omitted from minutes). V3 uses 'removed' implying active suppression rather than passive omission. The researcher should judge whether this distinction is significant for eligibility.

**Candidate related baseline IDs and evidence:**
- WEB-096: official participated in a lease decision involving a business linked to a close associate, without declaring a conflict of interest. Thematic parallel.

**Researcher question to resolve:** Is V3's language ('removed from the minutes') strong enough to require the researcher to assess whether this implies deliberate falsification beyond an omission? If so, does that affect the eligibility decision?

---

### SYN-FAM-013

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0037 | A lease clearance email appears to come from the required institution, but that institution denies sending it or approving the application. |
| V2 | SYN-0038 | The government-land lease file contains an emailed approval that the supposed issuing office says is not authentic. |
| V3 | SYN-0039 | An institutional clearance used to approve a lease is disputed by the institution whose name appears on the message. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe a forged or fabricated institutional clearance used to obtain a lease approval -- serious document falsification / integrity failure. Scenario is coherent and consistent. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same incident (clearance document denied by its alleged issuing institution). V1 and V2 are more specific (email format); V3 is more general. Core scenario is identical.

**Candidate related baseline IDs and evidence:**
- WEB-102: lease file contained a certificate stating an inspection was completed even though no inspection reportedly occurred. Broader parallel: fabricated certification.

SYN-FAM-013 involves a fabricated institutional clearance email vs. WEB-102's false inspection certificate. Same document-falsification type, different document.

**Researcher question to resolve:** Should the researcher flag WEB-102 given the shared document-falsification pattern, or treat the synthetic scenario as sufficiently distinct (email clearance vs. inspection certificate)?

---

### SYN-FAM-014

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0040 | The case procedure required a public application notice, but the lease file records publication without a copy or confirmation from the publisher. |
| V2 | SYN-0041 | A government-land allocation was processed using a claimed public notice that the named publisher says it never issued. |
| V3 | SYN-0042 | The stated publication of the lease opportunity cannot be confirmed, although the decision relied on the notice period being completed. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe a procedural failure -- a required public notice is either fabricated (V2) or unconfirmed (V1, V3). All are within the Administrative class.

**Variant consistency assessment (suggested):** Minor_variation -- V2 makes a stronger claim (publisher denies issuing the notice) compared to V1 and V3 (unconfirmed notice). All describe the same governance failure.

**Candidate related baseline IDs and evidence:**
No baseline record specifically describes a fabricated or unconfirmed public notice for lease applications.

Statement: No relationship identified by this review; independence not established.

**Researcher question to resolve:** Does the strength difference between V2 ('denied by publisher') and V1/V3 ('unconfirmed') rise to Minor_variation or Inconsistent? The researcher should judge whether V2's stronger falsification allegation is consistent with the other variants' procedural-gap framing.

---

### SYN-FAM-015

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0043 | The official selection minutes name an unsuccessful applicant, but a later private meeting produced a different lease decision without recorded authority. |
| V2 | SYN-0044 | A state-land lease award changed after an undocumented meeting, contradicting the signed selection record supplied to applicants. |
| V3 | SYN-0045 | Applicants received a government-land lease decision that conflicts with the recorded selection meeting, with no approved replacement decision available. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe an undocumented reversal of a lease selection decision -- a decision override outside the formal recorded process. Procedural integrity failure. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same incident (lease award changed outside the official record). V1 names the mechanism (private meeting); V2 and V3 describe the outcome.

**Candidate related baseline IDs and evidence:**
- WEB-101: district land administration changed key lease conditions after selection without recording the required authorization. Structural parallel.

SYN-FAM-015 involves the entire award decision being changed; WEB-101 involves conditions changed post-selection. The authorization-failure type is shared.

**Researcher question to resolve:** Should WEB-101 be flagged? The scenario type is structurally very similar (post-selection change without authorization), though WEB-101 involves conditions while SYN-FAM-015 involves the entire award decision.

---

### SYN-FAM-016

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0046 | The English translation in my lease file says I withdrew my objection, but my original Sinhala statement says I maintained it. |
| V2 | SYN-0047 | A lease decision relied on a translated withdrawal that contradicts the applicant's original statement retaining the objection. |
| V3 | SYN-0048 | My objection to the state-land lease was treated as withdrawn because the translation reversed the meaning of my signed original. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe an integrity failure in translation -- a document translation that reversed the applicant's stated position, materially affecting the lease decision. Clear document-integrity / procedural-integrity failure. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe translation reversal of an objection into a withdrawal. V1 is first-person (Sinhala to English); V2 and V3 describe the same from different perspectives. No drift.

**Candidate related baseline IDs and evidence:**
No baseline record in the reviewed set describes a translation that reversed the meaning of an applicant's statement in a lease matter.

Statement: No relationship identified by this review; independence not established.

**Researcher question to resolve:** This scenario (translation falsification in a multilingual administrative context) appears novel relative to the reviewed baseline. Does the researcher wish to confirm no baseline records involve translation discrepancies before finalizing?

---

### SYN-FAM-017

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0049 | The lease review record lists my attendance and consent, although I was not invited and did not participate. |
| V2 | SYN-0050 | An applicant disputes minutes stating that they attended the government-land lease hearing and accepted the proposed conditions. |
| V3 | SYN-0051 | My name was recorded as consenting at a lease hearing that I did not attend, and the office has not corrected it. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe false recording of a person's attendance and consent at a lease hearing -- document falsification within the administrative process. Scenario is coherent, consistent, and unambiguous. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same incident (false attendance/consent record). V1 adds the detail of not being invited; V2 and V3 describe the outcome.

**Candidate related baseline IDs and evidence:**
No baseline record in the reviewed set describes false recording of attendance and consent at a lease hearing.

Statement: No relationship identified by this review; independence not established.

**Researcher question to resolve:** Does the researcher wish to review WEB-102 to WEB-105 (document integrity records) to determine whether any describes false recording of participant presence? If not, leave Related_Baseline_IDs blank.

---

### SYN-FAM-018

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0052 | An officer instructed me to send the state-land lease application charge to a personal bank account rather than the designated collection account. |
| V2 | SYN-0053 | Payment instructions for a government-land lease application name the reviewing officer as the account holder, unlike the official payment notice. |
| V3 | SYN-0054 | The applicant received a personal-account payment request from the lease officer that conflicts with the institution's written collection instructions. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe an officer diverting official lease application fees to a personal bank account -- clear integrity/corruption failure (misappropriation of government fees). Distinct from SYN-FAM-001 (informal unreceipted cash demand) -- here the mechanism is a formal fee redirected to a personal account.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same core act (personal account substituted for the official collection account). V2 adds the detail that the officer's own name is on the account. No drift.

**Candidate related baseline IDs and evidence:**
- WEB-081 to WEB-083: informal payment demands in exchange for lease processing. Thematic parallel in corruption type but the mechanism differs (personal bank account substitution vs. informal cash demand).

**Researcher question to resolve:** Is this scenario sufficiently distinct from SYN-FAM-001 (unreceipted cash demand) to be treated as a separate family for training purposes? Both involve improper payment extraction but the mechanism differs. Researcher judgment on training value is needed.

---

### SYN-FAM-019

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0055 | The submission date on my acknowledged lease application was changed to a later date, making it appear to have missed the closing date. |
| V2 | SYN-0056 | My receipt shows that I applied for the state-land lease on time, but the file now records a date after applications closed. |
| V3 | SYN-0057 | A lease applicant reports that the office altered the recorded receipt date despite an earlier stamped acknowledgment. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe alteration of an application submission date to disqualify a timely application -- document falsification to manipulate eligibility. Clear integrity failure. Scenario is coherent, consistent, and unambiguous. No competing label.

**Variant consistency assessment (suggested):** Consistent -- all three describe the same incident (date altered to after the closing deadline). V1 and V2 are first-person; V3 is third-person. No drift.

**Candidate related baseline IDs and evidence:**
- WEB-103: dates in a land-lease approval record were altered to make an application appear to have been processed earlier. Structural parallel: date alteration in lease records, but in the opposite direction (earlier vs. later).

The falsification type (date alteration) is identical; the direction and purpose differ.

**Researcher question to resolve:** Should WEB-103 be flagged? The falsification type is identical but the direction differs (earlier vs. later). Whether this constitutes a meaningful relationship is a researcher judgment.

---

### SYN-FAM-020

**Proposed label:** Administrative / Procedural / Integrity

| Variant | Research ID | Exact text |
|---------|-------------|------------|
| V1 | SYN-0058 | A rejected state-land lease application was later marked approved without a new assessment or any recorded authority for overriding the rejection. |
| V2 | SYN-0059 | The decision history jumps from rejection to approval on a government-land lease file with no explanation, reconsideration record or authorized override. |
| V3 | SYN-0060 | An officer reviewing the lease record cannot find the decision that supposedly reversed the earlier rejection. |

**Suggested decision:** Approved
**Suggested category:** Administrative / Procedural / Integrity

**Rationale:** All three variants describe an unexplained reversal of a lease rejection -- a change from rejected to approved with no recorded authority, assessment, or reconsideration process. Clear procedural integrity failure. No competing label.

**Variant consistency assessment (suggested):** Consistent -- V1 and V2 describe the outcome from the file record; V3 describes an officer finding the reversal decision missing. All three describe the same governance failure (unexplained rejection override). No drift.

**Candidate related baseline IDs and evidence:**
- WEB-101: district land administration changed key lease conditions after selection without recording the required authorization. Structural parallel.

SYN-FAM-020 involves a rejection-to-approval flip rather than condition changes. The authorization-failure type is shared.

**Researcher question to resolve:** Should WEB-101 be flagged? The authorization-failure type is shared but the specific scenarios differ (conditions changed vs. rejection reversed). Researcher judgment required.

---

## Summary table -- first 20 families

| Family ID | Proposed category | Suggested decision | Main concern |
|-----------|-------------------|--------------------|--------------|
| SYN-FAM-001 | Admin / Procedural / Integrity | Approved | Informal cash demand; thematic parallel WEB-081 to WEB-084 |
| SYN-FAM-002 | Admin / Procedural / Integrity | Approved | Sexual favour conditional on approval; close parallels WEB-086 to WEB-089 |
| SYN-FAM-003 | Admin / Procedural / Integrity | Approved | Retaliation / complaint suppression; close parallel WEB-090 |
| SYN-FAM-004 | Admin / Procedural / Integrity | Approved | Undisclosed conflict of interest; close parallel WEB-096 |
| SYN-FAM-005 | Admin / Procedural / Integrity | Approved | Forged signature; broader parallel WEB-102 to WEB-104; no exact match |
| SYN-FAM-006 | Admin / Procedural / Integrity | Approved | False inspection record (date + officer); parallel WEB-102, WEB-103 |
| SYN-FAM-007 | Admin / Procedural / Integrity | Approved | Approval before mandatory clearance; parallel WEB-098 to WEB-100 |
| SYN-FAM-008 | Admin / Procedural / Integrity | Approved | Assessment score altered without authority; broader parallel WEB-103, WEB-105 |
| SYN-FAM-009 | Admin / Procedural / Integrity | Approved | Acknowledged documents denied by office; no close baseline match |
| SYN-FAM-010 | Admin / Procedural / Integrity | Needs_Discussion | Double fee demand -- Admin vs. Revenue boundary; label judgment required |
| SYN-FAM-011 | Admin / Procedural / Integrity | Approved | Confidential materials leaked to competitor; no close baseline match |
| SYN-FAM-012 | Admin / Procedural / Integrity | Approved | Conflict-of-interest objection removed from minutes; parallel WEB-096 |
| SYN-FAM-013 | Admin / Procedural / Integrity | Approved | Forged institutional clearance email; parallel WEB-102 |
| SYN-FAM-014 | Admin / Procedural / Integrity | Approved | Unconfirmed / false public notice; V2 vs. V1/V3 specificity -- consistency judgment needed |
| SYN-FAM-015 | Admin / Procedural / Integrity | Approved | Undocumented decision reversal; close parallel WEB-101 |
| SYN-FAM-016 | Admin / Procedural / Integrity | Approved | Translation reversal of objection; no close baseline match |
| SYN-FAM-017 | Admin / Procedural / Integrity | Approved | False attendance/consent in meeting minutes; no close baseline match |
| SYN-FAM-018 | Admin / Procedural / Integrity | Approved | Fee redirected to personal bank account; thematic parallel WEB-081 to WEB-083 |
| SYN-FAM-019 | Admin / Procedural / Integrity | Approved | Application date altered to disqualify; parallel WEB-103 (opposite direction) |
| SYN-FAM-020 | Admin / Procedural / Integrity | Approved | Rejection reversed without authority; parallel WEB-101 |

---

## Verification

**Families covered:** 20 distinct families (SYN-FAM-001 to SYN-FAM-020) confirmed
**Variants covered:** 60 variants (SYN-0001 to SYN-0060) confirmed
**Review pack approval fields changed:** None -- all decision fields in synthetic_family_review_pack.csv remain blank as delivered

To confirm source files are unchanged, run from the ML directory:

    python -c "import hashlib; files={'candidate-data/v1/synthetic_family_review_pack.csv':'e41a6bf12b780ac5ef60f3adc07b808e20f4521c5abd21cb6496d869a3cdbf8e','data/state_land_complaints_development_530.csv':'78877d77963d954f7f86eb5174e54f81013d0ad18b15763153c11ccfb0c39eb8'}; [print('OK' if hashlib.sha256(open(p,'rb').read()).hexdigest()==e else 'CHANGED',p) for p,e in files.items()]"

---

## Genuinely ambiguous families requiring researcher judgment

**SYN-FAM-010** is the only family in this batch where the label boundary requires a researcher
decision before a decision can be recorded:

> The double fee demand despite confirmed payment scenario sits on the boundary between
> Administrative / Procedural / Integrity (officer exploiting the fee process dishonestly) and
> Lease Revenue / Payment / Enforcement (fee collection irregularity). The proposed label
> (Administrative) is defensible, but the researcher should confirm which governance dimension
> is primary. Record Needs_Discussion if uncertain.

All other families (001 to 009, 011 to 020) are suggested as **Approved**, but several have
baseline relationship questions that require researcher judgment before filling Related_Baseline_IDs.
See the individual family sections above for specific questions.

---

*AI-assisted review suggestions -- human decisions pending.*
*This document does not constitute approval, rejection, or any formal review decision.*
*All review decisions must be entered by the human researcher into synthetic_family_review_pack.csv.*
