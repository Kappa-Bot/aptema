# Onboarding and displays

1. Build six-step onboarding covering privacy, detection, identification, safe test, comfort calibration, protection, and startup.
2. Derive stable display IDs from DisplayConfig and EDID, with WMI and legacy aliases as fallbacks.
3. Constrain overlays and tests to each physical display rectangle.
4. Add display enablement, offset, safe limits, method status, reversible test, and plain-language degraded guidance.
5. Handle connect, disconnect, sleep, wake, topology changes, and individual native failures without stopping other displays.
6. Gate: no hardware test runs without explicit opt-in; simulated topology tests cover reconnect and ID migration.

