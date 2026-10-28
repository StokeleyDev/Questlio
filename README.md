<p align="center"><img src="images/cover.png" alt="Questlio — your games, your time, one place"></p>

# Questlio

A Windows desktop app for organizing your game library and tracking playtime. Questlio keeps your data locally and supports English and Russian.

![Questlio overview with an empty library](images/screenshot.png)

## Features

- Track game sessions and playtime on Windows.
- Organize games by status, browse session history, and view statistics.
- Import a Steam library and playtime, and configure emulator tracking.
- Back up and restore your local library.
- Switch between English and Russian and choose a light or dark theme.

## Build

Requirements: Windows, .NET 8 SDK, and Microsoft Edge WebView2 Runtime.

```powershell
cd desktop
.\build.ps1
```

Launch the generated `Questlio.exe` in the repository root. The first launch starts with an empty library. Steam import may require a Steam Web API key and an accessible Steam profile.

## Data

Questlio stores its library and preferences locally in `%LOCALAPPDATA%\Questlio`. The screenshot shows an empty library; no personal library data is included.
