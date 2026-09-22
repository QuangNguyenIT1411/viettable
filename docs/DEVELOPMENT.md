# VietTable development

The Git repository root is `D:\Quang\VietTable`. It contains the Android app,
backend services, web frontends, scripts, and project documentation.

## Local configuration

- Android SDK configuration belongs in the ignored root `local.properties`.
- Backend secrets belong in the ignored `SelfRestaurant-master/.env` file.
- Copy `SelfRestaurant-master/.env.example` when setting up a new machine, then
  replace every `CHANGE_ME` value locally. Never commit the resulting `.env`.
- Google OAuth and VNPAY secrets must be supplied through local/environment
  configuration. Public client identifiers are not a substitute for client
  secrets.

## Android

Use JDK 11 or the Android Studio bundled JBR configured for this workspace.

```powershell
cd D:\Quang\VietTable
.\gradlew.bat :app:assembleDebug :app:testDebugUnitTest
```

For a connected Android device, keep the Gateway tunnel active:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\connect-phone.ps1 -OpenApp
```

See `docs/ANDROID_TESTING.md` for instrumentation test commands and artifact
handling.

## Backend and web frontends

Backend services and Docker Compose live in `SelfRestaurant-master`. The mobile
app calls only the Gateway on port `5100`.

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
docker compose up -d --build
```

Each web frontend under `SelfRestaurant-master/src/Frontend` uses its committed
`package-lock.json`. Run `npm ci` in the selected frontend before `npm run build`.

## Git workflow

Keep `main` deployable and use short-lived `feature/<name>` or `fix/<name>`
branches. Generated builds, local credentials, device dumps, test artifacts,
container data, and runtime uploads are intentionally excluded from Git.
