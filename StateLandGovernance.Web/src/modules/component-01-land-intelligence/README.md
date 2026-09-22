# Land Intelligence (Component 1)

Domain UI, API client mappings, types, and formatters for the Land Intelligence
module. Route files under `src/app/land-intelligence/` stay thin and import from
here.

Other platform components must not import this module. Use `src/shared` / `src/lib`
for cross-cutting UI and HTTP only.
