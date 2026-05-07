# Code Signing

Light Pilot can run unsigned, but Windows SmartScreen trust improves with a real code-signing certificate.

## Recommended Path

1. Buy an OV or EV code-signing certificate.
2. Sign `LightPilot.App.exe` after publish.
3. Sign the Inno Setup installer.
4. Add signing secrets to GitHub Actions only after certificate handling is decided.

## Local Signing Example

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a artifacts\LightPilot-0.2.1-win-x64\LightPilot.App.exe
```

`signtool.exe` is not currently detected in this workstation PATH.
