"""
Deterministic field-level semantic validation, normalization, and page priors
for all 17 WorkflowGovernance proposal extraction fields.

Adheres strictly to the 17-field catalog without adding, removing, or renaming fields.
Prioritizes precision, exact grounding, page priors, and deterministic rules over raw model confidence.
"""

import re
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Set, Tuple

# Canonical 25 Sri Lankan administrative districts
CANONICAL_DISTRICTS: List[str] = [
    "Colombo", "Gampaha", "Kalutara",
    "Kandy", "Matale", "Nuwara Eliya",
    "Galle", "Matara", "Hambantota",
    "Jaffna", "Kilinochchi", "Mannar", "Vavuniya", "Mullaitivu",
    "Batticaloa", "Ampara", "Trincomalee",
    "Kurunegala", "Puttalam",
    "Anuradhapura", "Polonnaruwa",
    "Badulla", "Monaragala",
    "Ratnapura", "Kegalle",
]

# Case-insensitive English and Sinhala alias mapping to Canonical District Name
DISTRICT_ALIASES: Dict[str, str] = {
    # Colombo
    "colombo": "Colombo", "කොළඹ": "Colombo",
    # Gampaha
    "gampaha": "Gampaha", "ගම්පහ": "Gampaha",
    # Kalutara
    "kalutara": "Kalutara", "කළුතර": "Kalutara",
    # Kandy
    "kandy": "Kandy", "මහනුවර": "Kandy",
    # Matale
    "matale": "Matale", "මාතලේ": "Matale",
    # Nuwara Eliya
    "nuwara eliya": "Nuwara Eliya", "නුවරඑළිය": "Nuwara Eliya", "නුවර එළිය": "Nuwara Eliya",
    # Galle
    "galle": "Galle", "ගාල්ල": "Galle",
    # Matara
    "matara": "Matara", "මාතර": "Matara",
    # Hambantota
    "hambantota": "Hambantota", "හම්බන්තොට": "Hambantota",
    # Jaffna
    "jaffna": "Jaffna", "යාපනය": "Jaffna",
    # Kilinochchi
    "kilinochchi": "Kilinochchi", "කිලිනොච්චිය": "Kilinochchi",
    # Mannar
    "mannar": "Mannar", "මන්නාරම": "Mannar",
    # Vavuniya
    "vavuniya": "Vavuniya", "වවුනියාව": "Vavuniya",
    # Mullaitivu
    "mullaitivu": "Mullaitivu", "මුලතිව්": "Mullaitivu",
    # Batticaloa
    "batticaloa": "Batticaloa", "මඩකලපුව": "Batticaloa",
    # Ampara
    "ampara": "Ampara", "අම්පාර": "Ampara",
    # Trincomalee
    "trincomalee": "Trincomalee", "ත්රිකුණාමලය": "Trincomalee", "ත්‍රිකුණාමලය": "Trincomalee",
    # Kurunegala
    "kurunegala": "Kurunegala", "කුරුණෑගල": "Kurunegala",
    # Puttalam
    "puttalam": "Puttalam", "පුත්තලම": "Puttalam",
    # Anuradhapura
    "anuradhapura": "Anuradhapura", "අනුරාධපුරය": "Anuradhapura", "අනුරාධපුර": "Anuradhapura",
    # Polonnaruwa
    "polonnaruwa": "Polonnaruwa", "පොළොන්නරුව": "Polonnaruwa",
    # Badulla
    "badulla": "Badulla", "බදුල්ල": "Badulla",
    # Monaragala
    "monaragala": "Monaragala", "මොණරාගල": "Monaragala", "මොනරාගල": "Monaragala",
    # Ratnapura
    "ratnapura": "Ratnapura", "රත්නපුර": "Ratnapura", "රත්නපුරය": "Ratnapura",
    # Kegalle
    "kegalle": "Kegalle", "කෑගල්ල": "Kegalle",
}

# Recognized standard land extent units
LAND_EXTENT_UNITS: Dict[str, str] = {
    "acre": "Acre", "acres": "Acre", "අක්කර": "Acre",
    "hectare": "Hectare", "hectares": "Hectare", "ha": "Hectare", "හෙක්ටයාර": "Hectare", "හෙක්ටයාර්": "Hectare",
    "perch": "Perch", "perches": "Perch", "පර්චස්": "Perch",
    "rood": "Rood", "roods": "Rood", "රූඩ්": "Rood",
    "sqm": "Square Metre", "square metre": "Square Metre", "square meter": "Square Metre",
    "m²": "Square Metre", "sq.m": "Square Metre",
    "a/r/p": "Composite (A/R/P)", "a r p": "Composite (A/R/P)",
}

# Recognized investment currencies
CURRENCY_MAPPINGS: Dict[str, str] = {
    "lkr": "LKR", "rs": "LKR", "rs.": "LKR", "රු": "LKR", "රු.": "LKR", "රු:": "LKR",
    "sri lankan rupees": "LKR", "rupees": "LKR",
    "usd": "USD", "us$": "USD", "$": "USD",
    "eur": "EUR", "€": "EUR",
    "gbp": "GBP", "£": "GBP",
}

# Recognized applicant types
APPLICANT_TYPES: Dict[str, str] = {
    "individual": "Individual", "තනි පුද්ගල": "Individual", "තනි පුද්ගලික": "Individual",
    "private company": "Private Company", "private limited": "Private Company",
    "(pvt) ltd": "Private Company", "pvt ltd": "Private Company", "පුද්ගලික සමාගම": "Private Company",
    "public company": "Public Company", "plc": "Public Company", "public limited company": "Public Company",
    "company": "Company", "ltd": "Company", "limited": "Company", "සමාගම": "Company",
    "government institution": "Government Institution", "government": "Government Institution", "රාජ්‍ය": "Government Institution",
    "cooperative": "Cooperative", "සමූපකාර": "Cooperative", "සමූපකාර සමිතිය": "Cooperative",
    "association": "Association", "සංගමය": "Association",
    "organization": "Organization", "සංවිධානය": "Organization",
    "ngo": "NGO",
}


@dataclass
class FieldValidationResult:
    """Outcome of validating a candidate fact against deterministic rules."""

    is_valid: bool
    status: str  # "Accepted", "Rejected", "NeedsReview"
    reasons: List[str] = field(default_factory=list)
    normalized_value: Optional[str] = None


# Generic officer and staff designation patterns (English and reliable Sinhala equivalents)
OFFICER_STAFF_PATTERN_EN = re.compile(
    r"\b(?:officer|director|secretary|engineer|surveyor|manager|inspector|commissioner|"
    r"consultant|supervisor|co-supervisor|examiner|evaluator|minister|dean|head|"
    r"development\s*officer|technical\s*officer|agricultural\s*officer)\b",
    re.IGNORECASE,
)

OFFICER_STAFF_PATTERN_SIN = re.compile(
    r"(?:නිලධාරී|නිළධාරී|නිලධාරි|නිළධාරි|ලේකම්|අධ්‍යක්ෂ|අධ්‍යක්ෂක|කළමනාකරු|"
    r"ඉංජිනේරු|සමීක්ෂක|පරීක්ෂක|කොමසාරිස්|උපදේශක|පරීක්ෂකවරයා|මිනින්දෝරු|"
    r"සාක්ෂිකරු|සංවර්ධන\s*නිලධාරි|සංවර්ධන\s*නිළධාරි|ගොවිජන\s*සේවා)",
    re.IGNORECASE,
)

# Reusable personal name patterns
PERSONAL_NAME_ENG = re.compile(r"^(?:[A-Z]\.\s*){1,4}[A-Z][a-zA-Z\s]+$")
PERSONAL_NAME_SIN = re.compile(r"^(?:[\u0D80-\u0DFF]\.\s*){1,4}[\u0D80-\u0DFF\s]+$")
PERSONAL_HONORIFICS = re.compile(
    r"(?:^|\s)(?:mr\.?|mrs\.?|ms\.?|dr\.?|prof\.?|rev\.?|මයා\.?|මහතා|මහත්මිය|මෙනවිය)(?:$|\s)",
    re.IGNORECASE,
)

# Purpose / objective action keywords
PURPOSE_ACTION_PATTERN_EN = re.compile(
    r"\b(?:develop|development|establish|establishment|construct|construction|cultivate|cultivation|"
    r"operate|operation|provide|provision|improve|improvement|expand|expansion|promote|promotion|"
    r"produce|production|create|creation|support|facilitate|facilitation|objective|purpose|aim|intention|"
    r"rationale|farming|agriculture|commercial|industrial|residential|tourism|generate|generation|"
    r"build|building|plant|planting|breed|breeding|rearing|business|venture|facility)\b",
    re.IGNORECASE,
)

PURPOSE_ACTION_PATTERN_SIN = re.compile(
    r"(?:සංවර්ධන|සංවර්ධනය|වගා|වගාව|ඉදි|ඉදිකිරීම|ඉදි\s*කිරීම|ස්ථාපිත|නිෂ්පාදන|නිෂ්පාදනය|අරමුණ|අපේක්ෂිත|"
    r"අපේක්ෂා|සඳහා|කටයුතු|පවත්වාගෙන|ක්‍රියාත්මක|ප්‍රවර්ධනය|උපයා|භාවිතා|සැලසුම්|බලාපොරොත්තු|යොදා|කිරීම|"
    r"පාලනය|ප්‍රයෝජනයට|ආරම්භ|ව්‍යාපාර|ගොවිපළ|ගොවිපල)",
    re.IGNORECASE,
)


def is_officer_or_staff_designation(text: str) -> bool:
    """Return True if text matches an official designation, job title, or staff role."""
    if not text:
        return False
    return bool(OFFICER_STAFF_PATTERN_EN.search(text) or OFFICER_STAFF_PATTERN_SIN.search(text))


def is_personal_name(text: str) -> bool:
    """Return True if text structurally matches a personal name with initials or honorific."""
    if not text:
        return False
    val = text.strip()
    return bool(
        PERSONAL_NAME_ENG.match(val)
        or PERSONAL_NAME_SIN.match(val)
        or PERSONAL_HONORIFICS.search(val)
    )


def validate_applicant_name(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate applicant_name: applying legal entity, company, or individual applicant.
    Requires applicant/proponent context.
    Rejects project titles, document headings, public officers, witnesses, or signatories without applicant context.
    """
    val = value.strip()
    combined = f"{val} {quote}".strip()

    # Reject project titles, document headings, or activity phrases
    title_pattern = re.compile(
        r"(?:proposal|report|mark\s*sheet|table|agreement|application|project|title|"
        r"ව්‍යාපෘති|ව්‍ය්‍යංපෘති|වාර්තාව|චාර්නාව|යෝජනාව|පටුන|වගාව|farming|cultivation|estate|hotel)",
        re.IGNORECASE,
    )
    if title_pattern.search(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a project, report, or activity heading, not an applicant legal name."],
        )

    # Reject government officer, staff, or academic evaluator designations
    if is_officer_or_staff_designation(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value contains an official or staff designation (not the applicant)."],
        )

    # Reject financial / turnover / cost lines
    if re.search(r"\b(?:turnover|cost|budget|lkr|rs[\.:]?|රු[\.:]?|මිල|වියදම|පිරිවැටුම)\b", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents a financial or cost line, not an applicant name."],
        )

    if len(val) < 2 or len(val) > 200:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Applicant name length ({len(val)}) is outside valid range (2-200)."],
        )

    # Contextual evidence check:
    applicant_context = re.compile(
        r"(?:applicant|proponent|applied\s*by|company|pvt\s*ltd|plc|ltd|limited|individual|owner|client|"
        r"අයදුම්කරු|අයදුම්කාර|යෝජකයා|ඉල්ලුම්කරු|සමාගම|ආයතනය|නම\s*[:\-]|"
        r"\b(?:mr\.?|mrs\.?|ms\.?)\b|(?:^|\s)(?:මයා|මහතා|මහත්මිය)(?:$|\s|[,\._]))",
        re.IGNORECASE,
    )
    has_applicant_context = bool(applicant_context.search(combined))

    # Reject if evidence quote indicates project proposal or project title heading without applicant context
    if re.search(r"(?:project\s*proposal|project\s*title|project\s*name|ව්‍යාපෘති\s*නම|ව්‍යාපෘති\s*යෝජනාව)", quote, re.IGNORECASE) and not has_applicant_context:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Evidence citation indicates this candidate is a project, report, or activity heading, not an applicant legal name."],
        )

    # If quote only contains supervisor / evaluator designations without applicant context
    if re.search(r"(?:co-supervisor|supervisor|supervision|evaluator|examiner)", quote, re.IGNORECASE) and not has_applicant_context:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Evidence citation is associated with a supervisor or evaluator rather than an applicant."],
        )

    # Soft page prior: note if located on later pages
    status = "Accepted"
    reasons = []
    if page_number > 3:
        status = "NeedsReview"
        reasons.append(f"Applicant name appeared on page {page_number}; soft page prior expects early pages.")
    elif not has_applicant_context:
        status = "NeedsReview"
        reasons.append("Candidate lacks explicit applicant/proponent contextual labeling (e.g. 'Applicant', 'අයදුම්කරු'). Flagged for human verification.")

    return FieldValidationResult(is_valid=True, status=status, reasons=reasons, normalized_value=val)


def validate_applicant_type(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """Validate applicant_type: explicit category only."""
    val = value.strip().lower()
    norm = APPLICANT_TYPES.get(val)

    # Check for suffix in quote or value
    if not norm:
        if "(pvt) ltd" in val or "private limited" in val or "පුද්ගලික සමාගම" in val:
            norm = "Private Company"
        elif "plc" in val:
            norm = "Public Company"
        elif "ltd" in val or "limited" in val or "සමාගම" in val:
            norm = "Company"
        elif "individual" in val or "තනි පුද්ගල" in val:
            norm = "Individual"
        elif "cooperative" in val or "සමූපකාර" in val:
            norm = "Cooperative"

    # Reject job titles / business activities
    rejected_terms = re.compile(
        r"(?:manager|engineer|officer|agriculture|cultivation|development|කෘෂිකාර්මික|වගාව|කළමනාකරු)",
        re.IGNORECASE,
    )
    if rejected_terms.search(val) and not norm:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents a job title or business activity, not an applicant legal type."],
        )

    if not norm:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{value}' does not match any recognized applicant type category."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=norm)


def validate_project_title(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate project_title: official title on cover or early proposal pages.
    Reject cost/turnover tables, yield statements, and officer designations.
    Uses soft page prior for ranking without hard rejection of later pages.
    """
    val = value.strip()
    combined = f"{val} {quote}".strip()

    # Reject budget, turnover, project-cost, and monetary headings
    cost_keywords = re.compile(
        r"(?:පිරිවැටුම|පිරිවැය|වියදම|ආදායම|මිල|මුදල|turnover|project\s*cost|budget|capital|expenditure|investment|revenue|lkr|rs\.|රු[:\.]|\b1\s*අය\s*9)",
        re.IGNORECASE,
    )
    if cost_keywords.search(combined):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a financial turnover, project-cost, or budget heading, not the project title."],
        )

    # Reject staff / officer designations
    if is_officer_or_staff_designation(combined):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches an officer designation or recommendation section, not the project title."],
        )

    # Reject yield and production statistics
    yield_keywords = re.compile(
        r"(?:ගෙඩි|අස්වැන්න|පැල\s*ගණන|yield|production|coconuts\s*per\s*acre|kg\s*per\s*ha)",
        re.IGNORECASE,
    )
    if yield_keywords.search(combined):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches an agricultural yield or production metric, not the project title."],
        )

    # Reject table of contents, schedule headings, signatures
    meta_keywords = re.compile(
        r"(?:පටුන|සටහන|නිර්දේශ|ඇමුණුම|table\s*of\s*contents|contents|annexure|signature|schedule)",
        re.IGNORECASE,
    )
    if meta_keywords.search(combined):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a document section, index, or table heading, not the project title."],
        )

    # Reject legal statutes / acts
    if re.search(r"(?:ordinance|act|ආඥාපනත|පනත|gazette|ගැසට්)", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a legislative statute or ordinance, not a project title."],
        )

    if len(val) < 3 or len(val) > 250:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Title length ({len(val)}) is outside standard range (3-250 characters)."],
        )

    # Soft page prior: note if located on later pages, but do NOT reject
    reasons = []
    has_explicit_label = bool(re.search(r"(?:(?:ව්‍යාපෘතියේ|වා්‍යාපෘතියේ|චා්‍යාපෘතියේ|වා9ාාපෘතියේ)\s*)?නම|project\s*name|project\s*title", quote, re.IGNORECASE))
    if page_number > 5 and not has_explicit_label:
        reasons.append(f"Title located on page {page_number}; soft page prior expects cover or early pages.")

    status = "NeedsReview" if reasons else "Accepted"
    return FieldValidationResult(is_valid=True, status=status, reasons=reasons, normalized_value=val)


def validate_project_purpose(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate project_purpose: stated objective, reason, intended outcome, or rationale for undertaking project.
    A valid purpose normally contains semantic/action meaning (e.g. develop, establish, cultivate, වගාව, සංවර්ධනය).
    Rejects:
    - Officer / staff designations
    - Personal names / signatories
    - Single numeric, currency, or financial cost lines
    - Legal statutes or acts
    - Document headings / table of contents / report titles
    - Pure administrative districts, DSDs, or pure geographic location names lacking purpose/action semantics
    - Isolated nouns or short labels without objective/action semantics
    """
    val = value.strip()
    val_lower = val.lower()
    combined = f"{val} {quote}".strip()

    # 1. Reject officer / staff designation (unless part of a complete narrative purpose sentence)
    if is_officer_or_staff_designation(val) and len(val.split()) <= 6:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches an official designation or staff title, not a project purpose."],
        )
    has_purpose_action = bool(PURPOSE_ACTION_PATTERN_EN.search(val) or PURPOSE_ACTION_PATTERN_SIN.search(val))
    if (is_officer_or_staff_designation(val) or is_officer_or_staff_designation(quote)) and not has_purpose_action:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value or evidence citation matches an official designation or staff title, not a project purpose."],
        )

    # 2. Reject personal names / honorifics
    if is_personal_name(val) or is_personal_name(quote):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a personal name rather than a project purpose."],
        )

    # 3. Reject pure numbers / currencies / cost lines
    cost_keywords = re.compile(
        r"(?:පිරිවැටුම|පිරිවැය|වියදම|ආදායම|මිල|මුදල|turnover|project\s*cost|budget|capital|expenditure|investment|revenue|lkr|rs\.|රු[:\.]|\b1\s*අය\s*9)",
        re.IGNORECASE,
    )
    if re.match(r"^(?:lkr|rs|usd|eur|\$|රු[\.\s]*)?[\d\.,\s]+(?:lkr|rs|usd|\%)?$", val, re.IGNORECASE) or cost_keywords.search(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Project purpose cannot be a single numeric, currency, or cost value."],
        )

    # 4. Reject pure legal acts / statutes
    if re.search(r"(?:ordinance|act|enactment|ආඥාපනත|පනත)$", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Project purpose cannot be a legal statute title."],
        )

    # 5. Reject document meta headings / table of contents / index
    meta_keywords = re.compile(
        r"(?:පටුන|සටහන|නිර්දේශ|ඇමුණුම|table\s*of\s*contents|contents|index|annexure|වාර්තාව|චාර්නාව|යෝජනාව)",
        re.IGNORECASE,
    )
    if meta_keywords.search(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a document section, index, or report heading, not a project purpose."],
        )

    # 6. Reject pure administrative division names (Districts or division phrases)
    cleaned_dist = re.sub(r"(?:district|දිස්ත්‍රික්කය|දිස්ත්‍රික්කයේ)", "", val_lower).strip()
    if val_lower in DISTRICT_ALIASES or cleaned_dist in DISTRICT_ALIASES:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{value}' is an administrative district name, not a project purpose."],
        )
    if re.search(r"(?:ප්‍රාදේශීය\s*ලේකම්\s*(?:කොට්ඨාසය|කොට්ඨාශය)|divisional\s*secretariat(?:\s*division)?|ds\s*division|gn\s*division|ග්‍රාම\s*නිලධාරි\s*වසම)", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{value}' is an administrative division reference, not a project purpose."],
        )

    # 7. Check length boundaries
    if len(val) < 3 or len(val) > 500:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Purpose length ({len(val)}) is outside valid range (3-500)."],
        )

    # 8. Action / purpose semantic indicators
    has_action_in_val = bool(PURPOSE_ACTION_PATTERN_EN.search(val) or PURPOSE_ACTION_PATTERN_SIN.search(val))
    word_count = len(val.split())

    if not has_action_in_val:
        # Isolated single words or short noun phrases without purpose semantics are rejected
        # (e.g. place names, DSD names, or isolated nouns without action/objective verbs)
        if word_count <= 3 or len(val) <= 30:
            return FieldValidationResult(
                is_valid=False,
                status="Rejected",
                reasons=[
                    f"Candidate '{value}' is an isolated noun, place name, or administrative reference lacking project objective or action semantics (e.g. develop, cultivate, construct, අරමුණ, වගාව, සංවර්ධනය)."
                ],
            )
        # For longer descriptions without explicit purpose keywords, flag for human verification
        return FieldValidationResult(
            is_valid=True,
            status="NeedsReview",
            reasons=["Candidate lacks explicit objective or action keywords (e.g. develop, establish, අරමුණ, වගාව). Flagged for human verification."],
            normalized_value=val,
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


def validate_land_location(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate land_location: geographic/site description of the land requested or proposed project site.
    Valid forms include: village, estate, GN division, DSD, district, road/site description, parcel locality.
    Rejects:
    - Officer designations, job titles, or department roles (e.g. 'Coconut Development Officer', 'Divisional Secretary')
    - Personal names or signatories (e.g. 'P.K. Gunasekara', 'Mr. Perera')
    - Document headings, report titles, or table of contents
    - Financial costs, turnover, or expenditure lines
    - Legislative enactments or statutes
    - Applicant's residential home address indicators
    - Government departments or institutional bodies (e.g. 'Agrarian Service Centre', 'Department of Agriculture')
    """
    val = value.strip()
    val_lower = val.lower()
    combined = f"{val} {quote}".strip()

    # 1. Reject officer designations, job titles, or department roles (unless Grama Niladhari territorial division context)
    is_gn_division = bool(re.search(r"(?:ග්‍රාම\s*නිලධාරි\s*(?:වසම|කොට්ඨාශය|බල\s*ප්‍රදේශය)|gn\s*division)", quote, re.IGNORECASE))
    if is_officer_or_staff_designation(val) or (is_officer_or_staff_designation(quote) and not is_gn_division):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value or evidence citation matches an official designation or staff title, not a land location or site description."],
        )

    # 2. Reject personal names or signatories
    if is_personal_name(val) or bool(PERSONAL_HONORIFICS.search(quote)):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a personal name or signatory rather than a land location."],
        )

    # 3. Reject applicant's residential address
    if re.search(r"(?:පදිංචි\s*ලිපිනය|residential\s*address|home\s*address)", quote, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Candidate represents the applicant's residential address rather than the land location."],
        )

    # 4. Reject financial / cost / turnover text
    cost_keywords = re.compile(
        r"(?:පිරිවැටුම|පිරිවැය|වියදම|ආදායම|මිල|මුදල|turnover|project\s*cost|budget|capital|expenditure|investment|revenue|lkr|rs\.|රු[:\.]|\b1\s*අය\s*9)",
        re.IGNORECASE,
    )
    if cost_keywords.search(val) or re.match(r"^(?:lkr|rs|usd|eur|\$|රු[\.\s]*)?[\d\.,\s]+(?:lkr|rs|usd|\%)?$", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a financial cost, turnover, or expenditure line, not a land location."],
        )

    # 5. Reject document section headings, report titles, or table of contents
    meta_keywords = re.compile(
        r"(?:පටුන|සටහන|නිර්දේශ|ඇමුණුම|table\s*of\s*contents|contents|annexure|signature|schedule|වාර්තාව|චාර්නාව|යෝජනාව)",
        re.IGNORECASE,
    )
    if meta_keywords.search(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a document heading, index, or report title, not a land location."],
        )

    # 6. Reject legislative enactments or statutes
    if re.search(r"(?:ordinance|act|enactment|ආඥාපනත|පනත)$", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a legislative enactment title, not a land location."],
        )

    # 7. Reject government departments or institutional bodies
    institution_keywords = re.compile(
        r"(?:දෙපාර්තමේන්තුව|මණ්ඩලය|අමාත්‍යාංශය|මධ්‍යස්ථානය|department|ministry|board|authority|centre|center)\b",
        re.IGNORECASE,
    )
    if institution_keywords.search(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents a government department or institutional body, not a land location."],
        )

    # 8. Length validation
    if len(val) < 2 or len(val) > 300:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Land location length ({len(val)}) is outside valid range (2-300)."],
        )

    # 9. Context check (District-only location is acceptable per Section 6)
    cleaned_dist = re.sub(r"(?:district|දිස්ත්‍රික්කය|දිස්ත්‍රික්කයේ)", "", val_lower).strip()
    is_district = val_lower in DISTRICT_ALIASES or cleaned_dist in DISTRICT_ALIASES

    location_context = re.compile(
        r"(?:land|site|parcel|lot|location|estate|village|road|district|province|division|dsd|gnd|situated|located|premises|acre|hectare|"
        r"ඉඩම|ඉඩමේ|පිහිටි|පිහිටීම|ස්ථානය|ගම|වසම|කොට්ඨාසය|කොට්ඨාශය|දිස්ත්‍රික්කය|මාර්ගය|වත්ත|අක්කර|හෙක්ටයාර්|බිම|භූමිය)",
        re.IGNORECASE,
    )
    has_location_context = is_district or bool(location_context.search(combined))

    if not has_location_context:
        return FieldValidationResult(
            is_valid=True,
            status="NeedsReview",
            reasons=["Candidate lacks explicit land or site location contextual indicators. Flagged for human verification."],
            normalized_value=val,
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


def validate_district(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate district: MUST resolve deterministically to one of the 25 official Sri Lankan administrative districts.
    Rejects DSDs, GNDs, villages, towns, or non-district entities.
    """
    val = value.strip().lower()

    # 1. Direct match or alias lookup
    canonical = DISTRICT_ALIASES.get(val)

    if not canonical:
        # Try stripping punctuation or trailing words like "district" / "දිස්ත්‍රික්කය"
        cleaned = re.sub(r"(?:district|දිස්ත්‍රික්කය|දිස්ත්‍රික්කයේ)", "", val, flags=re.IGNORECASE).strip()
        canonical = DISTRICT_ALIASES.get(cleaned)

    if not canonical:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{value}' does not resolve to any of the 25 official Sri Lankan administrative districts."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=canonical)


def validate_divisional_secretariat_division(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate DSD: Divisional Secretariat Division.
    Must have administrative-area context.
    Rejects person names (initials + surname, honorifics like Mr/Mrs/මයා/මහතා), officer designations, and district names.
    """
    val = value.strip()
    val_lower = val.lower()
    combined = f"{val} {quote}".strip()

    # 1. District substitution check: DSD must not simply be a district name
    if val_lower in DISTRICT_ALIASES:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{value}' is a District name, not a Divisional Secretariat Division."],
        )

    # 2. Reject personal names (initials + surname or personal honorifics)
    # English initials + surname (e.g. "P.K. Gunasekara", "A. B. Silva", "W.M. Perera")
    eng_personal_name = re.compile(r"^(?:[A-Z]\.\s*){1,4}[A-Z][a-zA-Z\s]+$")
    # Sinhala initials + surname (e.g. "පී.කේ. ගුණසේකර", "කේ. ඩී. සිල්වා")
    sin_personal_name = re.compile(r"^(?:[\u0D80-\u0DFF]\.\s*){1,4}[\u0D80-\u0DFF\s]+$")
    # Personal honorifics / titles
    honorifics = re.compile(
        r"(?:^|\s)(?:mr\.?|mrs\.?|ms\.?|dr\.?|prof\.?|rev\.?|මයා\.?|මහතා|මහත්මිය|මෙනවිය)(?:$|\s)",
        re.IGNORECASE,
    )

    if eng_personal_name.match(val) or sin_personal_name.match(val) or honorifics.search(val) or honorifics.search(quote):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{value}' structurally resembles a personal name and lacks DSD administrative context."],
        )

    # 3. Reject officer / staff designations
    officer_pattern = re.compile(
        r"(?:officer|secretary|surveyor|consultant|director|supervisor|නිලධාරි|නිළධාරි|ලේකම්|මිනින්දෝරු|උපදේශක|අධ්‍යක්ෂ|ගොවිජන)",
        re.IGNORECASE,
    )
    if officer_pattern.search(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value contains an official designation rather than an administrative division name."],
        )

    # 4. Reject Grama Niladhari division indicators
    if re.search(r"(?:ග්‍රාම\s*නිලධාරි|grama\s*niladhari|gn\s*division|\b\d{3}[a-z]?\b)", quote, re.IGNORECASE) and not re.search(r"(?:ප්‍රාදේශීය\s*ලේකම්|divisional\s*secretariat|ds\s*division)", quote, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value appears to be a Grama Niladhari division rather than a Divisional Secretariat division."],
        )

    if len(val) < 2 or len(val) > 150:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"DSD value length ({len(val)}) is outside expected range (2-150)."],
        )

    # 5. Clean common prefixes like 'ප්‍රාදේශීය ලේකම් කොට්ඨාශය :'
    cleaned = re.sub(
        r"^(?:ප්‍රාදේශීය\s*ලේක[\s\u200c\u200d]*[ම්ම][\s\u200c\u200d]*(?:කොට්ඨාසය|කොට්ඨාශය|බල\s*ප්‍රදේශය)|divisional\s*secretariat(?:\s*division)?|ds\s*division)\s*[:\-]\s*",
        "",
        val,
        flags=re.IGNORECASE,
    ).strip()

    # 6. Check for administrative division context in quote or cleaned string
    dsd_context = re.compile(
        r"(?:ප්‍රාදේශීය\s*ලේක[\s\u200c\u200d]*[ම්ම]|divisional\s*secretariat|ds\s*division|d\.s\.\s*division|dsd)",
        re.IGNORECASE,
    )
    if not dsd_context.search(combined):
        return FieldValidationResult(
            is_valid=True,
            status="NeedsReview",
            reasons=["Value lacks explicit administrative-area context (e.g. 'Divisional Secretariat' / 'ප්‍රාදේශීය ලේකම් කොට්ඨාසය'). Flagged for human verification."],
            normalized_value=cleaned,
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=cleaned)


def validate_land_extent(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate land_extent: area requested.
    Requires area context or recognized unit (acres, ha, perches, A/R/P notation).
    Rejects bare standalone numbers without area context, currency, durations, and employee counts.
    """
    val = value.strip()
    combined = f"{val} {quote}".strip()

    # Reject monetary amounts
    if re.search(r"(?:රු[:\.]|rs[\.:]|lkr|usd|\$|million|දහස|ලක්ෂ)", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents a monetary amount, not land extent."],
        )

    # Reject years / durations
    if re.search(r"(?:years|months|අවුරුදු|වසර|මාස)", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents a time duration, not land extent."],
        )

    # Reject employee counts
    if re.search(r"(?:employees|jobs|ශ්‍රමික|සේවක)", quote, re.IGNORECASE) and not re.search(r"(?:අක්කර|acre|extent)", quote, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents employment count, not land extent."],
        )

    # Reject crop yield / unit prices
    if re.search(r"(?:ගෙඩි|coconuts|yield|මිලකට)", quote, re.IGNORECASE) and not re.search(r"(?:අක්කර|acre|extent)", quote, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents agricultural yield or pricing, not land extent."],
        )

    # Check for cadastral composite extent (e.g. "2A 1R 10P", "2A-1R-10P", "2 Acres 1 Rood 10 Perches")
    cadastral_match = re.search(
        r"\b(\d+\s*[Aa](?:cres?)?\s*[\s,-]?\s*\d+\s*[Rr](?:oods?)?\s*[\s,-]?\s*\d+(?:\.\d+)?\s*[Pp](?:erches?)?)\b",
        val,
    )
    if cadastral_match:
        return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=cadastral_match.group(1).strip())

    # Check if value itself contains recognized area unit
    unit_in_val = re.search(
        r"\b(\d+(?:\.\d+)?)\s*(acres?|hectares?|ha|perches?|roods?|sqm|sq\.m|m²|අක්කර|හෙක්ටයාර්|හෙක්ටයාර|පර්චස්|රූඩ්)\b",
        val,
        re.IGNORECASE,
    )
    if unit_in_val:
        return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)

    # Check if the evidence quote clearly provides area unit context
    unit_in_quote = re.search(
        r"\b(?:acres?|hectares?|ha|perches?|roods?|sqm|sq\.m|m²|අක්කර|හෙක්ටයාර්|හෙක්ටයාර|පර්චස්|රූඩ්|extent)\b",
        quote,
        re.IGNORECASE,
    )
    num_match = re.match(r"^(\d+(?:\.\d+)?)$", val)
    if num_match:
        if not unit_in_quote:
            return FieldValidationResult(
                is_valid=False,
                status="Rejected",
                reasons=[f"Value '{val}' is a bare number lacking recognized land area units or A/R/P notation."],
            )
        return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)

    # If non-numeric and non-cadastral
    if not re.search(r"\d", val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{val}' contains no numeric land extent quantity."],
        )

    return FieldValidationResult(is_valid=True, status="NeedsReview", reasons=["Extent value format requires human verification."], normalized_value=val)


def validate_land_extent_unit(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """Validate land_extent_unit: recognized unit (Acre, Hectare, Perch, etc.)."""
    val = value.strip().lower()
    norm = LAND_EXTENT_UNITS.get(val)

    if not norm:
        # Check if quote contains a recognized unit
        for u_key, u_val in LAND_EXTENT_UNITS.items():
            if re.search(rf"\b{re.escape(u_key)}\b", val):
                norm = u_val
                break

    if not norm:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{value}' does not match any recognized land extent unit."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=norm)


def validate_requested_lease_period(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate requested_lease_period: lease duration (e.g. '30 years').
    Reject construction period, loan term, crop maturity, or payback period.
    """
    val = value.strip()
    combined = f"{val} {quote}".strip()

    # Reject project execution / construction / maturity terms
    reject_patterns = re.compile(
        r"(?:ක්‍රියාත්මක\s*සැලැස්ම|ණය\s*කාලය|අස්වනු\s*කාලය|loan\s*term|construction\s*period|payback|maturity)",
        re.IGNORECASE,
    )
    if reject_patterns.search(combined) and not re.search(r"(?:බදු|lease|tenure)", combined, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Duration relates to loan, construction, or crop maturity rather than lease tenure."],
        )

    # Check for duration pattern (e.g. 30 years, අවුරුදු 30)
    dur_match = re.search(r"(\d+)\s*(?:years?|වසර|අවුරුදු)", combined, re.IGNORECASE)
    norm = f"{dur_match.group(1)} years" if dur_match else val

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=norm)


def validate_proposed_land_use(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """Validate proposed_land_use: intended activity on land."""
    val = value.strip()

    # Reject pure numbers / currencies
    if re.match(r"^[\d\.,\s]+$", val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Proposed land use cannot be a numeric value."],
        )

    if len(val) < 3 or len(val) > 200:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Land use value length ({len(val)}) is outside expected range (3-200)."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


def validate_investment_amount(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate investment_amount: capital investment monetary amount.
    Strictly rejects percentages (e.g. '5%'), time durations, employee headcounts,
    land area extents, interest rates, profit margins, and unit produce prices.
    Bare numeric figures are accepted only when surrounding evidence explicitly labels
    them as capital investment, project cost, or capex.
    """
    val = value.strip()
    combined = f"{val} {quote}".strip()

    # 1. Reject percentages (CRITICAL: 5%, 12.5%, etc. must NEVER be accepted as investment amount)
    if re.search(r"(?:%|percent|percentage|ප්‍රතිශත)", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{val}' represents a percentage allocation or rate, not a capital investment monetary amount."],
        )

    # 2. Reject time durations (e.g. '30 years', '10 months')
    if re.search(r"\b(?:years?|months?|days?|අවුරුදු|වසර|මාස|දින)\b", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents a time duration, not an investment amount."],
        )

    # 3. Reject employee counts (e.g. '10 employees', 'workers')
    if re.search(r"\b(?:employees?|workers?|jobs?|සේවක|ශ්‍රමික)\b", val, re.IGNORECASE) or (
        re.search(r"\b(?:employees?|workers?|jobs?|සේවක|ශ්‍රමික)\b", quote, re.IGNORECASE) and not re.search(r"(?:lkr|rs[\.:]|usd|\$|රු[\.:]|cost|පිරිවැය|ආයෝජන)", quote, re.IGNORECASE)
    ):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents employee count, not an investment monetary amount."],
        )

    # 4. Reject land area extents (e.g. '5 hectares', '20 perches')
    if re.search(r"\b(?:acres?|hectares?|ha|perches?|roods?|අක්කර|හෙක්ටයාර්|හෙක්ටයාර|පර්චස්|රූඩ්)\b", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents land area extent, not an investment amount."],
        )

    # 5. Reject unit produce price (e.g. '50-60 per coconut')
    if re.search(r"(?:මිලකට|ඒකකයක්|per\s*coconut|unit\s*price|රු[;\.\s]*50\s*-\s*60)", combined, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents a unit produce selling price, not capital investment."],
        )

    # 6. Reject annual rent or tax
    if re.search(r"(?:annual\s*rent|බදු\s*කුලිය|stamp\s*duty|වාර්ෂික\s*කුලිය)", combined, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents lease rent or tax rather than project capital investment."],
        )

    # 7. Reject pure interest rate or profit margin
    if re.search(r"\b(?:interest\s*rate|profit\s*margin|markup)\b", combined, re.IGNORECASE) and not re.search(
        r"(?:lkr|rs[\.:]|usd|\$|රු[\.:]|amount|capital|investment)", combined, re.IGNORECASE
    ):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents an interest rate or profit margin, not capital investment."],
        )

    # 8. Must contain numeric characters
    digits = re.sub(r"[^\d]", "", val)
    if not digits:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Investment amount must contain a numeric figure."],
        )

    # 9. Bare numeric values check (e.g. '5000000')
    has_currency = bool(re.search(r"(?:lkr|rs[\.:]|usd|\$|රු[\.:]|million|මිලියන|ලක්ෂ|දහස|rupees)", combined, re.IGNORECASE))
    is_bare_number = bool(re.match(r"^[\d\s,]+(?:\.\d+)?$", val))

    if is_bare_number and not has_currency:
        bare_monetary_context = re.compile(
            r"(?:investment|capital\s*investment|project\s*cost|capital\s*expenditure|total\s*project\s*cost|"
            r"estimated\s*investment|total\s*cost|මුළු\s*ආයෝජන|ආයෝජනය|ව්‍යාපෘති\s*පිරිවැය|මුළු\s*පිරිවැය|ප්‍රාග්ධන)",
            re.IGNORECASE,
        )
        if not bare_monetary_context.search(combined):
            return FieldValidationResult(
                is_valid=False,
                status="NeedsReview",
                reasons=[f"Bare numeric value '{val}' lacks explicit capital investment context or currency."],
            )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


def validate_investment_currency(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """Validate investment_currency: recognized currency denomination."""
    val = value.strip().lower()
    norm = CURRENCY_MAPPINGS.get(val)

    if not norm:
        for c_key, c_val in CURRENCY_MAPPINGS.items():
            if re.search(rf"\b{re.escape(c_key)}\b", val):
                norm = c_val
                break

    if not norm:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Currency '{value}' does not match any recognized monetary currency."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=norm)


def validate_expected_employment(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate expected_employment: job count created.
    Reject beneficiaries, population, farmer counts.
    """
    val = value.strip()
    combined = f"{val} {quote}".strip()

    # Reject beneficiary or population counts
    if re.search(r"(?:beneficiaries|households|families|population|පවුල්|ගොවීන්|ප්‍රජාව)", combined, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents community beneficiaries or population, not employment count."],
        )

    digits = re.sub(r"[^\d]", "", val)
    if not digits:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Expected employment must contain an employee number count."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


def validate_project_description(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate project_description: narrative overview.
    Reject isolated yield statistics, isolated cost lines, or single activity sentences.
    """
    val = value.strip()

    # Reject isolated single crop/unit yield sentence
    if re.search(r"(?:yield|production|per\s*acre|per\s*ha|units?\s*per|output\s*expected|අස්වැන්න|අක්කරයකට)", val, re.IGNORECASE) and len(val) < 120:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents an isolated crop yield or production metric, not a comprehensive project overview."],
        )

    # Reject isolated cost line
    if re.search(r"(?:මුළු\s*පිරිවැටුම|total\s*cost|රු[:\.]|lkr|budget|revenue)", val, re.IGNORECASE) and len(val) < 80:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value represents an isolated cost line, not a project description."],
        )

    if len(val) < 15:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Project description is too brief to constitute an overview."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


def validate_supporting_document_references(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate supporting_document_references: annexures, plans, certificates, deeds.
    Requires recognized document attachment or reference semantics.
    Rejects random OCR noise, phone numbers, or isolated punctuation tokens.
    """
    val = value.strip()
    combined = f"{val} {quote}".strip()

    # Reject pure statute citations
    if re.search(r"(?:ordinance|act|ආඥාපනත|පනත)$", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value is a legislative act title rather than a supporting document attachment."],
        )

    if len(val) < 3:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Supporting document reference is too short."],
        )

    # Reject if candidate is formatted merely as a main proposal title/heading without attachment indicators
    if re.search(
        r"(?:project\s*proposal|lease\s*application|mark\s*sheet|proposal\s*report|ව්‍යාපෘති\s*වාර්තාව|ව්‍යාපෘති\s*යෝජනාව)",
        val,
        re.IGNORECASE,
    ) and not re.search(
        r"(?:attached|enclosed|annex|annexure|attachment|schedule|reference|appendix|ඇමුණුම|අමුණා\s*ඇත)",
        combined,
        re.IGNORECASE,
    ):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{val}' represents the main document title or evaluation heading, not a referenced supporting document."],
        )

    # Require recognized document semantics or attachment context
    doc_semantics = re.compile(
        r"(?:annex|annexure|attachment|certificate|survey\s*plan|plan\s*no|report|approval|eia|"
        r"feasibility\s*study|registration|drawing|schedule|appendix|reference\s*no|ref\s*no|"
        r"deed|title\s*deed|deed\s*no|gazette|clearance|permit|enclosure|"
        r"ඇමුණුම|සැලැස්ම|සහතිකය|වාර්තාව|අනුමැතිය|ඔප්පුව|ඔප්පු\s*අංක|පිඹුර|පිඹුරු\s*අංක|පරිසර|ලියාපදිංචි|අංකය|ලේඛන|යොමු|බලපත්‍ර)",
        re.IGNORECASE,
    )
    if not doc_semantics.search(combined):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{val}' lacks recognized document attachment, certificate, or survey plan semantics."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


def validate_legal_statutory_basis(value: str, quote: str, page_number: int) -> FieldValidationResult:
    """
    Validate legal_statutory_basis: explicit legislative act, ordinance, or statutory provision.
    Rejects project titles, commercial activities, or officer titles.
    """
    val = value.strip()

    # Reject government officer titles
    if re.search(r"(?:නිලධාරි|officer|supervisor|secretary|ලේකම්)", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value is an official designation rather than a legislative statute."],
        )

    # Reject project titles or business activities
    if re.search(r"(?:project|company|estate|hotel|farm|resort|factory|cultivation|development|ව්‍යාපෘති|වගාව|සමාගම|හෝටල්)", val, re.IGNORECASE):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Value matches a project activity, commercial entity, or title rather than a statutory enactment."],
        )

    if len(val) < 4:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=["Statutory citation is too short."],
        )

    # Require legislative / statutory terms
    statute_pattern = re.compile(
        r"(?:act|ordinance|enactment|constitution|gazette|regulation|statute|law|section|provision|order|code|"
        r"පනත|ආඥාපනත|ව්‍යවස්ථාව|නියෝග|ගැසට්|වගන්තිය|නීතිය)",
        re.IGNORECASE,
    )
    if not statute_pattern.search(val):
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"Value '{val}' does not contain recognized legislative enactment terms (e.g., Act, Ordinance, පනත, ආඥාපනත)."],
        )

    return FieldValidationResult(is_valid=True, status="Accepted", normalized_value=val)


# Registry of individual field validator functions
FIELD_VALIDATOR_REGISTRY = {
    "applicant_name": validate_applicant_name,
    "applicant_type": validate_applicant_type,
    "project_title": validate_project_title,
    "project_purpose": validate_project_purpose,
    "land_location": validate_land_location,
    "district": validate_district,
    "divisional_secretariat_division": validate_divisional_secretariat_division,
    "land_extent": validate_land_extent,
    "land_extent_unit": validate_land_extent_unit,
    "requested_lease_period": validate_requested_lease_period,
    "proposed_land_use": validate_proposed_land_use,
    "investment_amount": validate_investment_amount,
    "investment_currency": validate_investment_currency,
    "expected_employment": validate_expected_employment,
    "project_description": validate_project_description,
    "supporting_document_references": validate_supporting_document_references,
    "legal_statutory_basis": validate_legal_statutory_basis,
}


def validate_candidate_field(
    field_code: str,
    value: str,
    quote: str,
    page_number: int,
) -> FieldValidationResult:
    """Execute the deterministic field validator for a specific catalog field."""
    validator = FIELD_VALIDATOR_REGISTRY.get(field_code)
    if not validator:
        return FieldValidationResult(
            is_valid=False,
            status="Rejected",
            reasons=[f"No validator defined for unknown field code '{field_code}'."],
        )
    return validator(value=value, quote=quote, page_number=page_number)


def execute_cross_field_validation(
    facts: List[Dict[str, Any]],
) -> List[str]:
    """
    Perform cross-field consistency checks across extracted candidates.
    Returns list of consistency warnings.
    """
    warnings: List[str] = []
    by_code: Dict[str, List[Dict[str, Any]]] = {}
    for f in facts:
        c = f.get("fieldCode") or f.get("field_code")
        if c:
            by_code.setdefault(c, []).append(f)

    # 1. Applicant Name != Project Title
    app_facts = by_code.get("applicant_name", [])
    title_facts = by_code.get("project_title", [])
    for af in app_facts:
        a_val = (af.get("canonicalValue") or af.get("canonical_value") or "").strip().lower()
        for tf in title_facts:
            t_val = (tf.get("canonicalValue") or tf.get("canonical_value") or "").strip().lower()
            if a_val and t_val and a_val == t_val:
                warnings.append(
                    f"Cross-field conflict: 'applicant_name' ('{a_val}') is identical to 'project_title'. "
                    "Applicant name and project title cannot be identical."
                )

    # 2. District != DSD
    dist_facts = by_code.get("district", [])
    dsd_facts = by_code.get("divisional_secretariat_division", [])
    for df in dist_facts:
        d_val = (df.get("canonicalValue") or df.get("canonical_value") or "").strip().lower()
        for dsf in dsd_facts:
            ds_val = (dsf.get("canonicalValue") or dsf.get("canonical_value") or "").strip().lower()
            if d_val and ds_val and d_val == ds_val:
                warnings.append(
                    f"Cross-field consistency warning: 'district' ('{d_val}') matches 'divisional_secretariat_division'. "
                    "Administrative district and DS division must not be identical."
                )

    # 3. Project Title != Project Cost text or Officer Designation
    for tf in title_facts:
        t_val = (tf.get("canonicalValue") or tf.get("canonical_value") or "").strip()
        if re.search(r"(?:පිරිවැටුම|turnover|cost|පිරිවැය|budget)", t_val, re.IGNORECASE):
            warnings.append(
                f"Cross-field consistency warning: 'project_title' ('{t_val}') contains cost/turnover terms."
            )
        if re.search(r"(?:නිලධාරි|officer|supervisor)", t_val, re.IGNORECASE):
            warnings.append(
                f"Cross-field consistency warning: 'project_title' ('{t_val}') contains officer designation terms."
            )

    # 4. Land Extent and Unit correspondence check
    extent_facts = by_code.get("land_extent", [])
    unit_facts = by_code.get("land_extent_unit", [])
    if extent_facts and not unit_facts:
        for ef in extent_facts:
            e_val = (ef.get("canonicalValue") or ef.get("canonical_value") or "").strip()
            if re.match(r"^\d+(?:\.\d+)?$", e_val):
                warnings.append(
                    f"Cross-field consistency warning: 'land_extent' ('{e_val}') is a bare number without a corresponding 'land_extent_unit'."
                )

    # 5. Supporting Document References != Project Title
    supp_facts = by_code.get("supporting_document_references", [])
    for sf in supp_facts:
        s_val = (sf.get("canonicalValue") or sf.get("canonical_value") or "").strip().lower()
        s_quote = sf.get("quote", "") or ""
        for tf in title_facts:
            t_val = (tf.get("canonicalValue") or tf.get("canonical_value") or "").strip().lower()
            if s_val and t_val and (s_val == t_val or s_val in t_val):
                if not re.search(r"(?:attached|enclosed|annex|annexure|attachment|schedule|reference|appendix|ඇමුණුම|අමුණා\s*ඇත)", s_quote, re.IGNORECASE):
                    warnings.append(
                        f"Cross-field conflict: 'supporting_document_references' ('{s_val}') matches 'project_title' "
                        "without explicit attachment or reference context."
                    )

    # 6. Investment Currency binding check
    inv_facts = by_code.get("investment_amount", [])
    curr_facts = by_code.get("investment_currency", [])
    if curr_facts and inv_facts:
        for cf in curr_facts:
            c_page = cf.get("pageNumber") or cf.get("page_number")
            c_val = (cf.get("canonicalValue") or cf.get("canonical_value") or "").strip()
            # Verify if any investment fact shares the page or mentions the currency
            shares_context = any(
                (inf.get("pageNumber") or inf.get("page_number")) == c_page or
                c_val.lower() in (inf.get("quote", "") or "").lower() or
                c_val.lower() in (inf.get("canonicalValue", "") or "").lower()
                for inf in inv_facts
            )
            if not shares_context:
                warnings.append(
                    f"Cross-field consistency warning: 'investment_currency' ('{c_val}') on page {c_page} "
                    "does not share source context with extracted investment_amount."
                )

    # 7. Land Location != Officer Designation or Applicant Name
    loc_facts = by_code.get("land_location", [])
    for lf in loc_facts:
        l_val = (lf.get("canonicalValue") or lf.get("canonical_value") or "").strip()
        if is_officer_or_staff_designation(l_val):
            warnings.append(
                f"Cross-field conflict: 'land_location' ('{l_val}') matches an officer designation."
            )
        for af in app_facts:
            a_val = (af.get("canonicalValue") or af.get("canonical_value") or "").strip().lower()
            if l_val.lower() == a_val:
                warnings.append(
                    f"Cross-field conflict: 'land_location' ('{l_val}') is identical to 'applicant_name'."
                )

    # 8. Project Purpose != Pure District or DSD
    purpose_facts = by_code.get("project_purpose", [])
    for pf in purpose_facts:
        p_val = (pf.get("canonicalValue") or pf.get("canonical_value") or "").strip()
        p_lower = p_val.lower()
        has_purpose_action = bool(PURPOSE_ACTION_PATTERN_EN.search(p_val) or PURPOSE_ACTION_PATTERN_SIN.search(p_val))
        if not has_purpose_action:
            for df in dist_facts:
                d_val = (df.get("canonicalValue") or df.get("canonical_value") or "").strip().lower()
                if p_lower == d_val:
                    warnings.append(
                        f"Cross-field conflict: 'project_purpose' ('{p_val}') matches 'district' without purpose semantics."
                    )
            for dsf in dsd_facts:
                ds_val = (dsf.get("canonicalValue") or dsf.get("canonical_value") or "").strip().lower()
                if p_lower == ds_val:
                    warnings.append(
                        f"Cross-field conflict: 'project_purpose' ('{p_val}') matches 'divisional_secretariat_division' without purpose semantics."
                    )

    return warnings
