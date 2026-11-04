# Questlio Liquid Glass

The UI uses the original `GlassButton`, `GlassSegmented` `GlassSwitch` and `GlassCard` from `@zakisheriff/liquid-glass` 0.3.16, matching the package used by Yangi lotin. React and React DOM are pinned to 19.3.1. All runtime code and styles are local; no CDN, web server or Node.js runtime is needed by the shipped app.

`glass-ui/controls.jsx` mounts small React islands and forwards actions to the existing Questlio controls. The C# WebView bridge and SQLite schema remain unchanged. Original controls remain the fallback for opaque/reduced-motion/forced-colors modes. Only visible settings switches mount React; removed screen roots are unmounted. The library's own quality governor suspends refraction during scrolling and outside the viewport. Game cards, statistics and large dialogs remain opaque.

`ui/react-glass.js` and `ui/react-glass.css` are generated, production-minified assets. Rebuild them with `./build-glass.ps1`; its build tools install under `.glass-tools` and must not be published. Alternatively pass `-NodeModules` pointing at an existing installation with the pinned versions. Normal `./build.ps1` packages the already-built assets and does not require npm or internet access.

The previous custom optical adapter is no longer loaded. `glass.js` only retains existing ambient art/palette handling and compatibility methods; it does not activate any custom glass surfaces.

Validation uses synthetic libraries in headless Chromium, with a mocked native bridge. Personal library data is not used by tests. Native GPU performance has not been measured.

The sidebar now uses one original GlassCard material beneath the native navigation DOM. Reduced motion/opaque/forced colors unmount it. The native fallback switch's aria-checked state is synchronized from the input on every change and representation transition (0.30.3 fix).
