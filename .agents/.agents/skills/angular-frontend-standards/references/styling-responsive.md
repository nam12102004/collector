# Tailwind, SCSS, Tokens, And Responsive Layout

Read [aubot-ui-profile.md](aubot-ui-profile.md) when the user requests Aubot UI or the navy-and-emerald product language. Read [dependency-catalog.md](dependency-catalog.md) before changing styling or responsive dependencies.

## Ownership

- Use Tailwind for utility layout, spacing, sizing, alignment, and responsive variants.
- Use the established component library for high-level widgets and interaction state. Library-specific guidance applies only when that package is installed and used.
- Use repository design tokens for color, surface, border, typography, radius, and theme behavior.
- Keep reusable visual decisions in tokens or shared wrappers rather than repeated arbitrary classes.

## MUST-REVIEW

- Do not add inline styles when a token, Tailwind utility, component input, or scoped class can express the value.
- Do not use feature-local hex colors when a semantic token exists.
- Do not override component-library internals until component inputs, tokens, wrapper styles, and documented extension points have been exhausted.
- Any necessary deep/internal override must be narrowly scoped and explain the library gap it addresses.
- Layout must remain usable near 375px and at the repository's desktop container width.
- Text and rendered controls must not overflow their surface. Hiding overflow is not a substitute for correct width ownership.
- Avoid nested cards, decorative gradients, heavy shadows, and excessive rounding in operational screens unless the established design system requires them.

## Responsive Verification

Static class inspection is insufficient for tables, overlays, focus, drag/drop, or collision placement. Run browser/Playwright checks for those changes and verify both narrow and desktop widths.
