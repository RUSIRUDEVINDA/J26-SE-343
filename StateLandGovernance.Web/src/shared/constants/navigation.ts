export const SYSTEM_TITLE =
  "State Land Lease Information and Management System";

export const SYSTEM_TITLE_SINHALA =
  "රජයේ ඉඩම් බදු තොරතුරු සහ කළමනාකරණ පද්ධතිය";

export const SYSTEM_TITLE_TAMIL =
  "அரச காணி தொடர்பான தகவல் மற்றும் முகாமைத்துவ முறைமை";

export const LAND_INTELLIGENCE_BASE = "/land-intelligence";

export type NavItem = {
  label: string;
  href: string;
  icon: string;
  /** Emphasise the primary workflow entry in the sidebar. */
  primary?: boolean;
};

/** Only routes that are implemented and functional in this frontend. */
export const component01NavItems: NavItem[] = [
  { label: "Dashboard", href: LAND_INTELLIGENCE_BASE, icon: "▦" },
  {
    label: "Find Suitable Land",
    href: `${LAND_INTELLIGENCE_BASE}/find-suitable-land`,
    icon: "✦",
    primary: true,
  },
  {
    label: "Recommendations",
    href: `${LAND_INTELLIGENCE_BASE}/recommendations`,
    icon: "✦",
  },
  {
    label: "Parcel Intelligence",
    href: `${LAND_INTELLIGENCE_BASE}/parcels`,
    icon: "▤",
  },
  {
    label: "My requests",
    href: `${LAND_INTELLIGENCE_BASE}/my-requests`,
    icon: "☰",
  },
  {
    label: "Knowledge Graph",
    href: `${LAND_INTELLIGENCE_BASE}/knowledge-graph`,
    icon: "◎",
  },
];

/** Shown only when the API research-demo officer NIC lookup gate is active. */
export const officerLeaseInterestLookupNavItem: NavItem = {
  label: "Officer NIC lookup (Research demo)",
  href: `${LAND_INTELLIGENCE_BASE}/officer/lease-interest-lookup`,
  icon: "⚑",
};

export const officerLeaseInterestQueueNavItem: NavItem = {
  label: "Officer verification queue (Research demo)",
  href: `${LAND_INTELLIGENCE_BASE}/officer/lease-interest-queue`,
  icon: "⚑",
};

export const WORKFLOW_GOVERNANCE_BASE = "/workflow-governance";

export const workflowGovernanceNavItems: NavItem[] = [
  { label: "Intake assignment", href: `${WORKFLOW_GOVERNANCE_BASE}/assignments`, icon: "?" },
  { label: "My applications", href: `${WORKFLOW_GOVERNANCE_BASE}/applicant/cases`, icon: "?" },
  { label: "Commissioner rejection reports", href: `${WORKFLOW_GOVERNANCE_BASE}/commissioner`, icon: "▤" },
  {
    label: "Case Queue",
    href: `${WORKFLOW_GOVERNANCE_BASE}/cases`,
    icon: "▤",
    primary: true,
  },
  {
    label: "New Case Intake",
    href: `${WORKFLOW_GOVERNANCE_BASE}/applicant/intake`,
    icon: "✦",
  },
  {
    label: "Track Application",
    href: `${WORKFLOW_GOVERNANCE_BASE}/applicant/tracking`,
    icon: "☰",
  },
  {
    label: "OCR Intelligence Lab",
    href: `${WORKFLOW_GOVERNANCE_BASE}/ocr-demo`,
    icon: "◎",
  },
];

export const moduleLandingLinks = [
  {
    id: "component-01",
    title: "Land Intelligence and Spatial Recommendation",
    href: LAND_INTELLIGENCE_BASE,
    available: true,
  },
  {
    id: "component-02",
    title: "Financial Intelligence & Proposal Support",
    href: "/component-02",
    available: true,
  },
  {
    id: "component-03",
    title: "Workflow Governance",
    href: `${WORKFLOW_GOVERNANCE_BASE}/cases`,
    available: true,
  },
  {
    id: "component-04",
    title: "Governance Intelligence",
    href: "/component-04",
    available: true,
  },
];
