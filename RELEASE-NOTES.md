## Questlio 0.40.0

- Lower CPU usage: the tracker checks running programs once per second instead of once per game executable, and re-reads the library only when it changes.
- The tracker no longer stops for good after a single error; it retries automatically.
- Emulator tracking no longer crashes when a game record is missing.
- Time after a game closes is no longer added to the session.
- Database upgrades: future versions can update your library and restored backups automatically.
- Remaining error messages are now translated in the English interface.

### Windows downloads

- **Questlio-0.40.0-Setup.exe** — installs for the current user, adds a Start menu shortcut and includes an uninstaller in the application folder and Windows Installed apps.
- **Questlio-0.40.0-win-x64-portable.zip** — extract the entire archive and run Questlio.exe.
- **SHA256SUMS.txt** — checksums for both downloads.

Windows 10/11 x64, Microsoft Edge WebView2 Runtime. The .NET runtime is included; no .NET SDK is required. Exit Questlio from its tray menu before installing or uninstalling. Uninstall preserves the library, backups and preferences under `%LOCALAPPDATA%/Questlio`.

### По-русски

Трекер меньше нагружает процессор и больше не останавливается навсегда после одной ошибки. Исправлен сбой при отслеживании эмуляторов, время после закрытия игры больше не добавляется к сессии. База данных теперь умеет обновляться в новых версиях. Setup включает деинсталлятор; после удаления программы библиотека, резервные копии и настройки сохраняются. Перед обновлением выберите «Выход» в меню значка Questlio в трее.
