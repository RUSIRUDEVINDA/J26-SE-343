# State-land lease candidate screening (Component 1)

Decision-support screening for Find Suitable Land. **Not** lease approval,
ownership confirmation, or legal availability determination.

## Source fields that support screening

| Field | Source | Meaning in this system |
| --- | --- | --- |
| `pcl_if_clc` → `ClaimantCategoryCode` | Survey Department/iLAS cadastre import | Source-reported claimant category (State, Private, Not Checked, Other Authority*, …). **Screening signal only.** |
| `ClaimantCategoryEnglishLabel` | Decoded from official coded-value domain | Human-readable claimant category. |
| Prohibitive spatial / environmental restrictions | Parcel GIS enrichment | Existing **hard constraints** (documented conservation / prohibitive layers). Separate from ranking. |
| `ImportedAtUtc` | Import timestamp | When the source row was imported — **not** an official “last verified available” date. |

## Critical availability fields that are **absent**

| Need | Status |
| --- | --- |
| Authoritative “available to lease” / lease status | **Not present** in imported cadastre metadata |
| Current administrative availability confirmation | **Not present** |
| Official last-verified lease-availability date | **Not present** (`ImportedAtUtc` is import time only) |
| `pcl_pg_st` (ParcelStatus) as lease availability | Imported as raw provenance text; **no lease-status domain** is applied — never used to label a parcel available to lease |

Land use (`pcl_if_luc`), GIS road/water/soil distances, and ML scores **must not** be used to infer legal availability.

## Filtering rules (recommendation engine)

1. **Exclude** parcels whose source-reported claimant category is **Private** (code `3`).
2. **Include** **State** (code `2`) as **“Potential candidate — official availability not verified”**.
3. **Include** Not Checked, Not Known, Unsettled, Other Authority*, Praveni, Vihara/Dewalagam, Other, missing/unknown codes as **“Needs official verification”** (still shown; mandatory verification messaging).
4. Parcels **without** a `real_parcel_sources` row: **Needs official verification**.
5. Existing hard restrictions (prohibitive spatial/environmental) remain separate from claimant screening and are always applied — they are not optional applicant preferences.
6. Find Suitable Land does **not** filter or score by road distance, water proximity, soil group, environmental preference, road-access preference, or free-text notes. GIS enrichment may still appear on parcel details/maps.
7. Missing GIS evidence stays unavailable and is never treated as a positive match.
8. Experimental / production ML is never used to decide legal availability.

Original imported source values are preserved; screening does not overwrite provenance.
