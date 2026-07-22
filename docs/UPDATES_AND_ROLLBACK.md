# Updates And Rollback

Release packaging creates an application ZIP, Aptema Updater, and SHA-256 manifest. Updater rejects mismatched name/checksum and archive path traversal. It stages beside the installation, runs `--smoke-test --safe-mode --no-hardware`, retains current `App.previous`, switches directories, then repeats smoke validation. Failure restores the prior directory.

SHA-256 detects corruption but does not prove publisher identity. Aptema remains unsigned until a trusted OV/EV certificate is supplied. Windows SmartScreen may warn on unsigned builds.

Light Pilot startup and single-instance names are bridged during upgrade. Aptema removes the legacy startup entry only after a healthy Aptema start. Legacy settings and installation data are not automatically deleted.
