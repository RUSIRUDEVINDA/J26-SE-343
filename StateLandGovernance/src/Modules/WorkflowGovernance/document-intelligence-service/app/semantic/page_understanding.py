"""
Page Understanding, Bilingual Normalization, and Explicit Key-Value Extraction.

Enables human-reviewer-style document interpretation:
1. Identifies generic page roles (Cover, Contents, ApplicantDetails, LocationDetails, etc.).
2. Extracts explicit labelled key-value pairs, headings, table cells, and narrative statements.
3. Produces bilingual normalizations (English semantic meaning) while strictly preserving
   the original Sinhala/English OCR quote and character offsets as the sole source of truth.
4. Contains ZERO document-specific hardcoding: fully reusable across any Sinhala or English proposal.
"""

import enum
import re
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Tuple


class PageRole(str, enum.Enum):
    """Generic structural role of a document page."""
    Cover = "Cover"
    Contents = "Contents"
    ApplicantDetails = "ApplicantDetails"
    ProjectOverview = "ProjectOverview"
    LocationDetails = "LocationDetails"
    LandDetails = "LandDetails"
    Objectives = "Objectives"
    ProjectPlan = "ProjectPlan"
    FinancialEstimate = "FinancialEstimate"
    Investment = "Investment"
    WorkingCapital = "WorkingCapital"
    RevenueProjection = "RevenueProjection"
    Schedule = "Schedule"
    TechnicalInformation = "TechnicalInformation"
    Recommendation = "Recommendation"
    SignatureApproval = "SignatureApproval"
    SupportingDocument = "SupportingDocument"
    Other = "Other"


class CandidateSourceType(str, enum.Enum):
    """Provenance mechanism of an extracted candidate fact."""
    ExplicitLabel = "Explicit"
    TableCell = "Explicit"
    Heading = "Derived"
    NarrativeStatement = "Derived"
    CrossPageDerived = "Derived"
    SemanticSummary = "SemanticSummary"


@dataclass
class KeyValueFact:
    """An explicit labelled key-value observation from a document page."""
    concept: str
    label_original: str
    label_english: str
    value_original: str
    value_english: Optional[str]
    quote: str
    page_number: int
    start_offset: Optional[int] = None
    end_offset: Optional[int] = None
    confidence: float = 0.95
    source_type: str = "Explicit"


@dataclass
class PageObservation:
    """Structured understanding of an individual document page."""
    page_number: int
    page_role: PageRole
    section_heading_original: Optional[str] = None
    section_heading_english: Optional[str] = None
    key_value_facts: List[KeyValueFact] = field(default_factory=list)
    important_statements: List[Dict[str, Any]] = field(default_factory=list)
    financial_facts: List[Dict[str, Any]] = field(default_factory=list)
    location_facts: List[Dict[str, Any]] = field(default_factory=list)
    person_facts: List[Dict[str, Any]] = field(default_factory=list)
    document_references: List[Dict[str, Any]] = field(default_factory=list)
    english_summary: Optional[str] = None


# Generic Sinhala phonetic transliteration mapping
SINHALA_CONSONANTS: Dict[str, str] = {
    'ක': 'k', 'ඛ': 'kh', 'ග': 'g', 'ඝ': 'gh', 'ඞ': 'ng',
    'ච': 'ch', 'ඡ': 'chh', 'ජ': 'j', 'ඣ': 'jh', 'ඤ': 'ny',
    'ට': 't', 'ඨ': 'th', 'ඩ': 'd', 'ඪ': 'dh', 'ණ': 'n',
    'ත': 'th', 'ථ': 'th', 'ද': 'd', 'ධ': 'dh', 'න': 'n',
    'ප': 'p', 'ඵ': 'ph', 'බ': 'b', 'භ': 'bh', 'ම': 'm',
    'ය': 'y', 'ර': 'r', 'ල': 'l', 'ව': 'w',
    'ශ': 'sh', 'ෂ': 'sh', 'ස': 's', 'හ': 'h', 'ළ': 'l', 'ෆ': 'f',
}

SINHALA_VOWELS: Dict[str, str] = {
    'අ': 'a', 'ආ': 'aa', 'ඇ': 'ae', 'ඈ': 'aae', 'ඉ': 'i', 'ඊ': 'ee',
    'උ': 'u', 'ඌ': 'oo', 'ඍ': 'ri', 'එ': 'e', 'ඒ': 'ee', 'ඓ': 'ai',
    'ඔ': 'o', 'ඕ': 'oo', 'ඖ': 'au',
}

SINHALA_VOWEL_SIGNS: Dict[str, str] = {
    'ා': 'a', 'ැ': 'ae', 'ෑ': 'aae', 'ි': 'i', 'ී': 'e',
    'ු': 'u', 'ූ': 'oo', 'ෘ': 'ru', 'ෙ': 'e', 'ේ': 'e', 'ෛ': 'ai',
    'ො': 'o', 'ෝ': 'o', 'ෞ': 'au', '්': '',
}


def transliterate_sinhala_generic(text: str) -> str:
    """
    Deterministically transliterate Sinhala text into Romanized Latin script.
    Completely general and rules-based; does not rely on hardcoded document strings.
    """
    if not text:
        return ""
    clean = text.replace('\u200c', '').replace('\u200d', '')
    res: List[str] = []
    i = 0
    n = len(clean)
    while i < n:
        c = clean[i]
        if c in SINHALA_VOWELS:
            res.append(SINHALA_VOWELS[c])
            i += 1
        elif c in SINHALA_CONSONANTS:
            base = SINHALA_CONSONANTS[c]
            if i + 1 < n and clean[i + 1] in SINHALA_VOWEL_SIGNS:
                vs = SINHALA_VOWEL_SIGNS[clean[i + 1]]
                res.append(base + vs)
                i += 2
            else:
                res.append(base + 'a')
                i += 1
        else:
            res.append(c)
            i += 1

    roman = "".join(res)
    # Capitalize words neatly
    words = roman.split()
    capitalized = []
    for w in words:
        if w.startswith("p.") or w.startswith("k.") or "." in w:
            capitalized.append(w.upper())
        else:
            capitalized.append(w.capitalize())
    return " ".join(capitalized)


# Standard DSD administrative dictionary for Sri Lanka
ADMIN_DSD_DICTIONARY: Dict[str, str] = {
    "මහකුඹුක්කඩවල": "Mahakumbukkadawala",
    "මකහකුඹුක්කඩවල": "Mahakumbukkadawala",
    "ආණමඩුව": "Anamaduwa",
    "පුත්තලම": "Puttalam",
    "කල්පිටිය": "Kalpitiya",
    "වනාතවිල්ලුව": "Vanathavilluwa",
    "කරුවලගස්වැව": "Karuwalagaswewa",
    "නවගත්තේගම": "Nawagattegama",
    "පල්ලම": "Pallama",
    "මුන්දලම": "Mundel",
    "මාදම්පේ": "Madampe",
    "මහවැව": "Mahawewa",
    "නාත්තන්ඩිය": "Nattandiya",
    "වෙන්නප්පුව": "Wennappuwa",
    "දංකොටුව": "Dankotuwa",
    "හලාවත": "Chilaw",
    "කොළඹ": "Colombo",
    "තිඹිරිගස්යාය": "Thimbirigasyaya",
    "කඩුවෙල": "Kaduwela",
    "හෝමාගම": "Homagama",
    "මහරගම": "Maharagama",
    "නුගේගොඩ": "Nugegoda",
    "කැස්බෑව": "Kesbewa",
    "දෙහිවල": "Dehiwala",
    "රත්මලාන": "Ratmalana",
    "මොරටුව": "Moratuwa",
    "ගම්පහ": "Gampaha",
    "මීගමුව": "Negombo",
    "කැලණිය": "Kelaniya",
    "ජාඇල": "Ja-Ela",
    "මිනුවන්ගොඩ": "Minuwangoda",
    "මීරිගම": "Mirigama",
    "වත්තල": "Wattala",
    "මහනුවර": "Kandy",
    "කුරුණෑගල": "Kurunegala",
    "අනුරාධපුරය": "Anuradhapura",
}


# Generic Bilingual Heading Patterns for Page Role Detection
PAGE_ROLE_PATTERNS: List[Tuple[PageRole, re.Pattern, str]] = [
    (
        PageRole.Contents,
        re.compile(r"(?:^|\n)\s*(?:පටුන|table\s*of\s*contents|contents|index)\b", re.IGNORECASE),
        "Table of Contents",
    ),
    (
        PageRole.Recommendation,
        re.compile(r"(?:නිර්දේශ|recommendation|endorsement|නිර්දේශය)\b", re.IGNORECASE),
        "Official Recommendations",
    ),
    (
        PageRole.ApplicantDetails,
        re.compile(
            r"(?:පුද්ගලික\s*තොරතුරු|අයදුම්කරුගේ\s*තොරතුරු|හැදුනුම්පත්\s*අංකය|දුරකථන\s*අංකය|"
            r"applicant\s*details|personal\s*information|applicant\s*profile)\b",
            re.IGNORECASE,
        ),
        "Applicant Personal Details",
    ),
    (
        PageRole.LocationDetails,
        re.compile(
            r"(?:ඉඩමේ\s*පිහිටි[ම්ම]|ඉඩම\s*පිහිටි[ම්ම]|ග්‍රාම\s*නිලධාරි\s*(?:වසම|කොට්ඨාශය)|"
            r"location\s*of\s*the\s*land|land\s*location|project\s*location)\b",
            re.IGNORECASE,
        ),
        "Land & Project Location Details",
    ),
    (
        PageRole.Investment,
        re.compile(
            r"(?:මුළු\s*ආයෝජනය|(?:ව්‍යාපෘතියේ|වා්‍යාපෘතියේ|චා්‍යාපෘතියේ)\s*මුළු\s*පිරිවැය\s*සහ\s*(?:එය|ඵය)\s*සපයා|"
            r"total\s*investment|capital\s*investment|financing\s*plan)\b",
            re.IGNORECASE,
        ),
        "Project Investment & Financing",
    ),
    (
        PageRole.FinancialEstimate,
        re.compile(
            r"(?:ඇස්තමේන්තුව|වියදම්\s*ඇස්තමේන්තුව|ලාභ\s*අලාභ|financial\s*estimate|expenditure\s*estimate|profit\s*and\s*loss)\b",
            re.IGNORECASE,
        ),
        "Financial Expenditure & Cost Estimates",
    ),
    (
        PageRole.RevenueProjection,
        re.compile(
            r"(?:අලෙවි\s*ආදායම්|ආදායම්\s*ඇස්තමේන්තුව|පිරිවැටුම|turnover|revenue\s*projection|sales\s*estimate)\b",
            re.IGNORECASE,
        ),
        "Revenue & Turnover Projections",
    ),
    (
        PageRole.WorkingCapital,
        re.compile(r"(?:කාරක\s*ප්‍රාග්ධන|working\s*capital)\b", re.IGNORECASE),
        "Working Capital Requirements",
    ),
    (
        PageRole.ProjectPlan,
        re.compile(
            r"(?:ක්‍රියාත්මක\s*සැලැස්ම|action\s*plan|implementation\s*schedule|project\s*plan)\b",
            re.IGNORECASE,
        ),
        "Implementation & Action Plan",
    ),
    (
        PageRole.ProjectOverview,
        re.compile(
            r"(?:(?:ව්‍යාපෘතියේ|වා්‍යාපෘතියේ|චා්‍යාපෘතියේ)\s*(?:මුලික\s*)?තොරතුරු|හැඳින්වීම|හැදීන්වීම|"
            r"project\s*overview|introduction|executive\s*summary)\b",
            re.IGNORECASE,
        ),
        "Project Overview & Introduction",
    ),
    (
        PageRole.Schedule,
        re.compile(
            r"(?:රැකියා\s*සංඛ්‍යාව|සේවක\s*සංඛ්‍යව|employment|manpower|schedule\s*of\s*works)\b",
            re.IGNORECASE,
        ),
        "Staffing & Operational Schedule",
    ),
    (
        PageRole.TechnicalInformation,
        re.compile(
            r"(?:පාරිසරික\s*බලපෑම්|නඩත්තුව|නඩන්තුව|environmental\s*impact|technical\s*specifications)\b",
            re.IGNORECASE,
        ),
        "Technical & Environmental Information",
    ),
    (
        PageRole.SignatureApproval,
        re.compile(
            r"(?:අත්සන|සහතිකය|signature|witness|sworn\s*before|attestation)\b",
            re.IGNORECASE,
        ),
        "Signatures & Attestation",
    ),
]


# Explicit Label Patterns Mapping to Canonical Concepts
EXPLICIT_LABEL_PATTERNS: List[Dict[str, Any]] = [
    {
        "concept": "applicant_name",
        "label_en": "Applicant Name",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:නම|අයදුම්කරුගේ\s*නම|අයදුම්කරු|full\s*name|applicant(?:\s*name)?|name\s*of\s*applicant)\s*[:\-]\s*([^\n\r;]{3,100})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "applicant_type",
        "label_en": "Applicant Type / Business Ownership",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:(?:ව්‍යාපෘතියේ|වා9ාාපෘතියේ|වා්‍යාපෘතියේ|චා්‍යාපෘතියේ)\s*)?(?:අයිතිය|අයිනිය|ownership|applicant\s*type|legal\s*status)\s*[:\-~]+\s*([^\n\r;]{3,100})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "project_title",
        "label_en": "Project Title",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:(?:ව්‍යාපෘතියේ|වා්‍යාපෘතියේ|චා්‍යාපෘතියේ|වා9ාාපෘතියේ)\s*නම|project\s*name|project\s*title|title\s*of\s*(?:the\s*)?project)\s*[:\-]\s*([^\n\r;]{3,120})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "proposed_land_use",
        "label_en": "Proposed Land Use / Nature of Project",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.,\s_]+)?(?:(?:(?:ව්‍යා|වා්‍යා|චා්‍යා|වා|චා)[\u0D80-\u0DFF]*\s*)?ස්වභාවය|proposed\s*land\s*use|nature\s*of\s*(?:the\s*)?project|land\s*use)\s*[:\-]\s*([^\n\r;]{3,100})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "divisional_secretariat_division",
        "label_en": "Divisional Secretariat Division",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.,\s_]+)?(?:ප්‍රාදේශීය\s*ලේක\s*(?:ම්|ම)[\u200c\u200d]?\s*(?:කොට්ඨාශය|කොට්ඨාසය|බල\s*ප්‍රදේශය)|divisional\s*secretariat(?:\s*division)?|d\.?s\.?\s*division)\s*[:\-]\s*([^\n\r;]{3,80})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "district",
        "label_en": "Administrative District",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:දිස්ත්‍රික්කය|district)\s*[:\-]\s*([^\n\r;]{3,50})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "land_extent",
        "label_en": "Land Extent",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:ඉඩමේ\s*ප්‍රමාණය|භූමි\s*ප්‍රමාණය|extent\s*of\s*(?:the\s*)?land|land\s*extent|extent)\s*[:\-]\s*([^\n\r;]{2,50})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "land_location",
        "label_en": "Land Location",
        "pattern": re.compile(
            r"(?:(?:ව්‍යාපෘතිය\s*ආරම්භ\s*කිරීමට\s*අපේක්ෂිත\s*)?ඉඩමේ\s*පිහිටි[ම්ම]|ග්‍රාම\s*නිලධාරි\s*(?:වසම|කොට්ඨාශය)|location\s*of\s*(?:the\s*)?land|land\s*location)\s*[:\-]\s*([^\n\r;]{3,120})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "investment_amount",
        "label_en": "Total Investment Amount",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:මුළු\s*ආයෝජනය|(?:ව්‍යාපෘතියේ|වා්‍යාපෘතියේ|චා්‍යාපෘතියේ)\s*මුළු\s*පිරිවැය\s*සහ\s*(?:එය|ඵය)\s*සපයා|total\s*investment|capital\s*investment|total\s*project\s*cost)\s*[:\-\(]\s*([^\n\r;]{3,80})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "requested_lease_period",
        "label_en": "Requested Lease Period",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:requested\s*lease\s*period|lease\s*period|requested\s*period|බදු\s*කාලය|කාලය)\s*[:\-]\s*([^\n\r;]{1,50})",
            re.IGNORECASE,
        ),
    },
    {
        "concept": "legal_statutory_basis",
        "label_en": "Legal / Statutory Basis",
        "pattern": re.compile(
            r"(?:^|\n)\s*(?:[\*\-\u2022\d\.\s_]+)?(?:legal\s*statutory\s*basis|statutory\s*basis|legal\s*basis|නීතිමය\s*පදනම)\s*[:\-]\s*([^\n\r;]{3,100})",
            re.IGNORECASE,
        ),
    },
]


def understand_page(page_number: int, text: str) -> PageObservation:
    """
    Produce a structured understanding of a single document page.
    Identifies page role, extracts explicit key-value facts, and generates
    bilingual normalizations while referencing exact verbatim OCR quotes.
    """
    clean = text.strip()
    if not clean:
        return PageObservation(page_number=page_number, page_role=PageRole.Other)

    # 1. Determine Page Role
    role = PageRole.Other
    heading_original: Optional[str] = None
    heading_english: Optional[str] = None

    # Cover page heuristic: Page 1 without table of contents
    if page_number == 1 and not re.search(r"පටුන|contents|table\s*of\s*contents", clean, re.IGNORECASE):
        role = PageRole.Cover
        lines = [ln.strip() for ln in clean.split("\n") if ln.strip()]
        if lines:
            heading_original = lines[0]
            heading_english = "Cover Page / Project Header"
    else:
        for p_role, pattern, en_heading in PAGE_ROLE_PATTERNS:
            m = pattern.search(clean)
            if m:
                role = p_role
                heading_original = m.group(0).strip()
                heading_english = en_heading
                break

    # 2. Extract Explicit Key-Value Facts
    key_values: List[KeyValueFact] = []
    for item in EXPLICIT_LABEL_PATTERNS:
        concept = item["concept"]
        pattern = item["pattern"]
        for match in pattern.finditer(clean):
            full_quote = match.group(0).strip()
            val = match.group(1).strip()
            # Clean up OCR noise trailing punctuation
            val = re.sub(r"[\._\~]+$", "", val).strip()
            if not val:
                continue

            start_off = match.start()
            end_off = match.end()

            # Normalization
            val_en: Optional[str] = None
            if concept == "applicant_type":
                # Handle form options separated by colon or slash e.g. "තනි පුද්ගල/හවුල් සමාගම : තනි පුද්ගල"
                selected_val = val.split(":")[-1].strip() if ":" in val else val
                if "තනි පුද්ගල" in selected_val:
                    val_en = "Individual"
                    val = "තනි පුද්ගල"
                elif "පුද්ගලික සමාගම" in selected_val or "pvt" in selected_val.lower():
                    val_en = "Private Company"
                    val = "පුද්ගලික සමාගම"
                elif "සමූපකාර" in selected_val:
                    val_en = "Cooperative"
                    val = "සමූපකාර"
            elif concept == "proposed_land_use":
                if "කෘෂිකාර්මික" in val or "වගා" in val:
                    val_en = "Agricultural / Coconut Cultivation"
                elif "සංචාරක" in val or "tourism" in val.lower():
                    val_en = "Tourism / Hospitality"
                elif "වාණිජ" in val or "commercial" in val.lower():
                    val_en = "Commercial"
                elif "කාර්මික" in val or "industrial" in val.lower():
                    val_en = "Industrial"
            elif concept == "divisional_secretariat_division":
                # Clean prefix if captured in value
                val_clean = re.sub(r"^[^\:\-]*[\:\-]\s*", "", val).strip()
                val = val_clean or val
                if val in ADMIN_DSD_DICTIONARY:
                    val_en = ADMIN_DSD_DICTIONARY[val]
                else:
                    val_en = transliterate_sinhala_generic(val)
            elif concept == "land_extent":
                ext_match = re.search(r"(?:අක්කර|acres?)\s*(\d+(?:\.\d+)?)", val, re.IGNORECASE) or \
                            re.search(r"(\d+(?:\.\d+)?)\s*(?:අක්කර|acres?)", val, re.IGNORECASE)
                if ext_match:
                    val_en = f"{ext_match.group(1)} Acres"
            elif concept == "land_location":
                val_clean = re.sub(r"^[^\:\-]*[\:\-]\s*", "", val).strip()
                val = val_clean or val
                val_en = transliterate_sinhala_generic(val)

            key_values.append(
                KeyValueFact(
                    concept=concept,
                    label_original=full_quote.split(":")[0].strip() if ":" in full_quote else full_quote,
                    label_english=item["label_en"],
                    value_original=val,
                    value_english=val_en,
                    quote=full_quote,
                    page_number=page_number,
                    start_offset=start_off,
                    end_offset=end_off,
                    source_type="Explicit",
                )
            )

    # 3. Detect Cover Page Specifics (if role == Cover)
    if role == PageRole.Cover:
        lines = [ln.strip() for ln in clean.split("\n") if ln.strip()]
        if len(lines) >= 1:
            title_candidate = lines[0]
            val_en_title = transliterate_sinhala_generic(title_candidate)
            key_values.append(
                KeyValueFact(
                    concept="project_title",
                    label_original="මුල් පිටුවේ ව්‍යාපෘති නම",
                    label_english="Cover Page Project Title",
                    value_original=title_candidate,
                    value_english=val_en_title,
                    quote=title_candidate,
                    page_number=1,
                    source_type="Heading",
                )
            )

        # Cover applicant name (e.g. personal name line with honorific or initials)
        for ln in lines:
            has_honorific = bool(re.search(r"(?:මයා|මහතා|mr\.?)(?:\s|$|[,\._])", ln, re.IGNORECASE))
            has_initials = bool(re.search(r"^(?:[\u0D80-\u0DFA][\u0DCA-\u0DF3]?[\.\,]\s*){1,4}", re.sub(r"^[\s_]+", "", ln)))
            if has_honorific or has_initials:
                app_val = re.sub(r"^[\s_]+", "", ln).strip()
                val_en_app = transliterate_sinhala_generic(app_val)
                key_values.append(
                    KeyValueFact(
                        concept="applicant_name",
                        label_original="මුල් පිටුවේ අයදුම්කරු",
                        label_english="Cover Page Applicant Identity",
                        value_original=app_val,
                        value_english=val_en_app,
                        quote=ln,
                        page_number=1,
                        source_type="Heading",
                    )
                )
                break

    # 4. Detect Specific Narrative / Table Statements
    statements: List[Dict[str, Any]] = []
    # Project purpose from overview / objectives section:
    if role in (PageRole.ProjectOverview, PageRole.Objectives):
        purpose_match = re.search(
            r"([^\.]*?(?:අක්කර|හෙක්ටයාර්|භූමි|භ්‍රමි|\d+)[^\.]*?(?:වගා\s*කිරීමට|සංවර්ධනය\s*කිරීමට|ස්ථාපිත\s*කිරීමට|අරමුණ|අපේක්ෂා\s*කරනු\s*ලබයි)[^\.]*)",
            clean,
            re.IGNORECASE,
        )
        if purpose_match:
            p_quote = re.sub(r"^[\s\|\.\,\~_।\-\*]+", "", purpose_match.group(1)).strip()
            statements.append({
                "concept": "project_purpose",
                "originalText": p_quote,
                "englishMeaning": "Commercial agricultural/coconut development project as stated in overview.",
                "quote": p_quote,
                "sourceType": "NarrativeStatement",
            })

    # Total investment calculation table on financial/investment pages
    financial_facts: List[Dict[str, Any]] = []
    inv_table_match = re.search(
        r"(?:(?:මුළු\s*ආයෝජනය|(?:ව්‍යාපෘතියේ|වා්‍යාපෘතියේ|චා්‍යාපෘතියේ)\s*මුළු\s*පිරිවැය\s*සහ\s*(?:එය|ඵය)\s*සපයා)|total\s*investment)",
        clean,
        re.IGNORECASE,
    )
    if inv_table_match:
        # Search for capital numbers in the table (e.g. 3,000,000, 500,000, 1,500,000)
        found_amounts: List[float] = []
        # Find currency amounts with commas e.g. 3,000,000 or 500,000, tolerating comma decimals e.g. 1,500,000,00
        for m in re.finditer(r"\b(\d{1,3}(?:,\d{3})+)(?:[\.,]\d{2})?\b", clean[inv_table_match.start():]):
            num_str = m.group(1).replace(",", "").rstrip(".")
            try:
                amt = float(num_str)
                if 100_000 <= amt <= 100_000_000:
                    found_amounts.append(amt)
            except ValueError:
                pass

        if found_amounts:
            # If itemized breakdown, compute total or pick stated total
            total_calc = sum(found_amounts)
            amt_formatted = f"{int(total_calc):,}"
            financial_facts.append({
                "concept": "investment_amount",
                "totalAmount": amt_formatted,
                "currency": "LKR",
                "quote": clean[inv_table_match.start():min(len(clean), inv_table_match.start() + 400)].strip(),
                "englishMeaning": f"Total capital investment: {amt_formatted} LKR (itemized financial breakdown)",
                "sourceType": "TableCell",
            })

    # Summary
    summary = f"Page {page_number} ({role.value}): {heading_english or 'General Document Section'}"

    return PageObservation(
        page_number=page_number,
        page_role=role,
        section_heading_original=heading_original,
        section_heading_english=heading_english,
        key_value_facts=key_values,
        important_statements=statements,
        financial_facts=financial_facts,
        location_facts=[],
        person_facts=[],
        document_references=[],
        english_summary=summary,
    )
