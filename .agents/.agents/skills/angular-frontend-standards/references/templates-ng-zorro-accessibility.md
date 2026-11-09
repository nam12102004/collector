# Templates, Installed Components, Tables, And Accessibility

The filename is retained for compatibility with existing skill links. ASRS installs NG-ZORRO 22; use its widgets for interactive controls, tables, menus, overlays, feedback, and pagination. Keep Angular CDK primitives where needed and use native HTML for semantic content.

Read [dependency-catalog.md](dependency-catalog.md) before selecting or changing UI packages. Read [table-standard.md](table-standard.md) completely whenever a collection might have row-column semantics.

## Library-First Mapping

Before writing markup, map each requested interaction to the component library or established shared wrapper installed and used by the affected feature.

- `MUST-REVIEW`: when the installed library or a shared wrapper provides the capability, use it. Custom table, modal, drawer, tabs, dropdown, form control, pagination, tooltip, alert, loading, focus, or overlay behavior requires a documented capability gap.
- Raw HTML remains appropriate for semantic content such as headings, paragraphs, articles, labels, and static layout.
- Do not use CSS or native DOM handlers to recreate a library-supported interaction.

## Tab Boundary

- When the affected feature uses tabs, each tab body MUST render one feature-local component. Apply the rule to the installed tab implementation; `nz-tab` examples are conditional on NG-ZORRO being installed.
- The tab parent owns title, permission visibility, selected index, and lazy rendering. The child owns its cohesive surface and exposes typed inputs/outputs.

## Semantic Table Gate

A collection is tabular when users compare multiple records across stable fields. Tabular data MUST use `nz-table`, including inside tabs, drawers, and modals.

- Do not emulate rows and columns with `@for` plus `div`, `a`, flex/grid, or `float-right`.
- Timeline, feed, navigation, and genuinely independent cards may remain lists because cross-column comparison is not their purpose.
- Apply [table-standard.md](table-standard.md) after classifying a collection as tabular. Using `nz-table` alone does not prove containment.

## Declarative Templates

- Templates render prepared state and emit user intent. Do not sort, filter, map, reduce, build requests, or perform business calculations in HTML.
- Do not call methods with side effects or non-trivial computation from bindings.
- Move repeated or complex conditions into named `computed()` values, selectors, facade view models, or pure pipes.
- Avoid nested ternaries and duplicated long expressions.
- Use Angular control flow with stable `track` expressions.

## Accessibility

- Use semantic interactive elements, associated labels, keyboard access, visible focus, and accessible names.
- Click behavior on a non-native control requires the equivalent keyboard and focus contract; prefer an installed component or native semantic control.
- Preserve dialog focus behavior, escape/cancel behavior, contrast, and reduced-motion preferences.
