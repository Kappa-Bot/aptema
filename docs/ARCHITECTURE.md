# Architecture And Decision Flow

`Aptema.App` observes immutable `ComfortRuntimeSnapshot` values from `Aptema.Application`. Coordinators gather foreground context, low-frequency luminance, power state, displays, settings, and session duration. `Aptema.Core.AdaptiveEngine` returns deterministic targets, reason, confidence, source, responsible rule, and transition. Infrastructure applies each display independently through DDC/CI, Windows brightness, or bounded overlay fallback.

Preference corrections are aggregate signals keyed by monitor, app category, day phase, fullscreen state, and luminance class. They are capped before engine smoothing and safety clamps. Screenshots, pixels, window titles, and content never enter the model.

Configuration is atomic and recoverable. Startup health is separate from settings so repeated incomplete starts can force recovery mode even when configuration is corrupt.
