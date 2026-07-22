# Application architecture and persistence

1. Add Application project and tests with immutable runtime snapshots, typed operation results, clock, use cases, and bounded event flow.
2. Move orchestration out of WPF ViewModels into comfort, display, feedback, configuration, and maintenance coordinators.
3. Add schema-4 envelope, atomic writes, known-good backup, corruption quarantine, and idempotent LightPilot import.
4. Add schema-5 display/profile/application/hotkey fields and schema-6 automation/safe-mode/update fields through explicit migrations.
5. Add validated export/import preview and stable display/learning IDs.
6. Gate: coordinator tests require no WPF or hardware; migration can run repeatedly without data loss.

