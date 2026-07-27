# LOOT-PRESENTATION-001C targeted self-audit correction

This stacked correction extracts bindable projection views from the development showcase without changing reward, pickup, holdings, opening, persistence, XP, navigation, or gameplay authorities.

## Self-audit findings addressed

- HUD, grouped-box selection, opening-stage rendering, and reward cards are reusable bindable components rather than fixture-owned drawing code.
- The showcase composes those same components from disposable immutable fixture projections.
- Open 5 fails closed when the selected group contains fewer than five exact instances.
- The opening surface now has visible closed, opening, reveal, and complete states driven by the existing immutable session.

## Authority boundaries

- `LootRunHudViewV1` consumes immutable totals only.
- `OwnedStrongboxGroupsViewV1` owns only presentation selection state.
- `StrongboxOpeningPresentationViewV1` reads one existing opening session and does not invoke BOX/RAP.
- `StrongboxRewardCardsViewV1` reads immutable reveal items only.
- No production composition or persistence file is changed.

## Validation status

Static source reread and branch comparison were completed. Unity compilation, test execution, manual scene acceptance, and performance testing remain unexecuted. The PR must remain draft.
