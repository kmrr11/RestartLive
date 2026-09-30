# Narrative Life Game

Approved scope: a narrative life simulator with persistent consequences, milestone events,
relationship development, an ending biography, local saves and reproducible lives.
Keep attributes as narrative checks. Do not add resource-management loops.

## Implementation
- [ ] Persist a versioned seed and player-command journal. Reconstruct the exact pending
  choice/continuation by replaying commands against matching content. Reject incompatible
  content and damaged saves without replacing the current session.
- [ ] Connect automatic saves and Continue to the existing Unity UI. Preserve logs on load.
- [ ] Add eligible milestone priority, consequence events, relationship progression and
  world-return consequences through the existing CSV system.
- [ ] Restrict repeated confession rewards, show relationship stages, generate a biography.
- [ ] Fail closed on invalid conditions and validate narrative references.
- [ ] Run a standalone core test harness using production sources, including save/load at
  choice and continuation boundaries, deterministic replay and full-life simulations.

## Validation Boundaries
Core tests use minimal Unity API substitutes. They verify domain behavior, not Unity
layout or editor compilation. Attempt editor connectivity separately; report unavailable
runtime verification explicitly. Existing Packages/packages-lock.json edits are unrelated.
