# Product Evolution

## v0.3.0 Premium Adaptive Assistant

Light Pilot v0.3.0 changes the product posture.

Before: compact brightness utility with visible controls.

After: tray-first adaptive eye-comfort assistant.

## User Flow

1. Install.
2. Auto starts with Windows.
3. Tray keeps comfort active in the background.
4. User opens Home only when they want to know what is happening.
5. User clicks Quick Adjust when the screen feels wrong.
6. Light Pilot learns the local context and applies safer future adjustments.

## UX Changes

- Home focuses on comfort state, mode, reason, display summary, and pause.
- Quick Adjust makes feedback the primary interaction.
- Settings holds monitor control, DDC/CI, content analysis, app overrides, safety limits, diagnostics, and reset.
- Main UI avoids raw control-layer names and percentage-heavy display.
- Tray menu includes feedback commands so most interactions do not need the window.

## Adaptive Learning Model

Feedback is aggregated by:

- monitor id
- app category
- day phase
- fullscreen state
- luminance class

The aggregate stores samples, net brightness score, net warmth score, derived offsets, confidence, and last update time. Offsets are capped and still pass through engine safety limits.

## Safety

- Automatic changes remain capped to 3 brightness points and 200K warmth per decision.
- Manual feedback remains capped to 6 brightness points and 300K warmth.
- Fullscreen game/video/presentation contexts suppress automatic visible changes.
- Failed DDC/CI degrades per monitor and falls back without crashing the loop.

## Privacy

Learning never stores screenshots, raw pixels, window titles, website titles, document names, or typed content. Optional content analysis processes tiny in-memory samples and keeps only luminance aggregates.

## Reset

Settings includes:

- disable preference learning
- reset learned comfort
- reset defaults

Settings are stored in `%LOCALAPPDATA%\LightPilot\settings.json`.
