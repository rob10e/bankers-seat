# Accessibility Plan

## Target

WCAG 2.2 AA across the web application, with additional emphasis on fast table-side use, cognitive clarity, and motor accessibility.

## Core requirements

- Full keyboard navigation.
- Visible focus.
- Semantic headings and landmarks.
- Accessible names for all controls.
- Form instructions and error associations.
- Sufficient color contrast.
- No color-only meaning.
- Reduced-motion support.
- Text scaling and browser zoom.
- Screen-reader announcements for accepted or rejected transactions.
- Touch targets at least 44 by 44 CSS pixels.
- Predictable navigation.

## Balance and transaction presentation

- Read amount, source, destination, and result in a logical order.
- Use both sign/wording and color to distinguish income and expense.
- Avoid rapidly disappearing toasts as the only confirmation.
- Maintain a transaction status region with polite live announcements.
- Format large values with separators.
- Let users choose compact or large-text balance views.

## Custom fields

Template authors provide labels and optional descriptions. The application generates accessible controls based on field type and must not trust a template to provide complete accessibility metadata.

Images require meaningful alt text when informative and empty alt text when decorative.

## Motion

Animated balance changes and connection effects must:

- Respect `prefers-reduced-motion`.
- Avoid flashing.
- Not be required to understand state.
- Have equivalent text/status indicators.

## Color and identity

Players may choose colors, but identity also uses name, initials, icon, or pattern. Color selection must avoid confusingly similar options and support color-vision deficiencies.

## Error handling

Errors should state:

- What happened.
- Why, when known.
- What the user can do.
- Whether the command was applied.

Focus should move to the error summary for blocking form errors and return sensibly after dialogs close.

## Testing

- Automated axe checks.
- Keyboard-only test scripts.
- NVDA and VoiceOver manual passes.
- 200 percent zoom.
- phone screen reader.
- reduced motion.
- high-contrast mode where supported.
- cognitive walkthrough for transaction confirmation and correction.
