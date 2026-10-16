# Desktop integration

Shared orchestration lives in MainWindow and DesktopOverlay. OS-specific calls are isolated in Infrastructure: Windows uses Win32 for input/session state, MCI for audio, the current-user Run key for login startup, and DPAPI for the saved login. macOS uses Objective-C/AppKit input flags, CoreGraphics session/window information, afplay, a per-user LaunchAgent, and Keychain.

The character window is separate from the interactive banner. Passive input is applied only to the character. Core scheduling remains in Domain and has no Avalonia or OS dependency.

These adapters compile, but require actual desktop acceptance tests for click-through, focus, Spaces/fullscreen, lock/sleep, audio and startup. Catch-up notification attempts use native OS commands with in-app history/status fallback. See docs/VALIDATION.md for actual evidence and remaining work.
