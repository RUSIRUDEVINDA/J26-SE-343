export const SYSTEM_TITLE =
  "State Land Lease Information and Management System";

export const SYSTEM_TITLE_SINHALA =
  "රජයේ ඉඩම් බදු තොරතුරු සහ කළමනාකරණ පද්ධතිය";

export const SYSTEM_TITLE_TAMIL =
  "அரச காணி தொடர்பான தகவல் மற்றும் முகாமைத்துவ முறைமை";

export const LAND_INTELLIGENCE_BASE = "/land-intelligence";

export type NavItem = {
  label: string;
  href?: string;
  icon: string;
  disabled?: boolean;
  disabledReason?: string;
};

export const component01NavItems: NavItem[] = [
  { label: "Dashboard", href: LAND_INTELLIGENCE_BASE, icon: "▦" },
  {
    label: "Find Suitable Land",
    href: `${LAND_INTELLIGENCE_BASE}/find-suitable-land`,
    icon: "✦",
  },
  {
    label: "Recommendations",
    href: `${LAND_INTELLIGENCE_BASE}/recommendations`,
    icon: "✦",
  },
  {
    label: "Parcel Intelligence",
    href: `${LAND_INTELLIGENCE_BASE}/parcel-intelligence`,
    icon: "▤",
  },
  {
    label: "Reserve Land",
    icon: "▣",
    disabled: true,
    disabledReason: "Owned by another component",
  },
  {
    label: "Knowledge Graph",
    href: `${LAND_INTELLIGENCE_BASE}/knowledge-graph`,
    icon: "◎",
  },
  {
    label: "Tax Checker",
    icon: "",
    disabled: true,
    disabledReason: "Owned by another component",
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
    title: "Component 2",
    href: "/component-02",
    available: false,
  },
  {
    id: "component-03",
    title: "Component 3",
    href: "/component-03",
    available: false,
  },
  {
    id: "component-04",
    title: "Component 4",
    href: "/component-04",
    available: false,
  },
];
