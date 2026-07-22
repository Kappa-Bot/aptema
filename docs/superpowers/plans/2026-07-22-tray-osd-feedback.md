# Tray, OSD, and feedback

1. Replace context-menu-first interaction with an anchored WPF tray flyout while retaining an accessible fallback menu.
2. Render state, pause, five feedback actions, display summary, next adaptation, Open, and Settings.
3. Add non-activating four-second OSD on the active display and ten-second feedback undo.
4. Add `Win+Alt+A` Quick Adjust default; keep direct feedback hotkeys unassigned until configured.
5. Detect hotkey conflicts and expose typed degraded results. Keep NotifyIcon text within 63 characters.
6. Gate: tray commands use the same Application use cases as the full app; paused/off states never imply future adaptation.

