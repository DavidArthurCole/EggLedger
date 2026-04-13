# EggLedger Mobile Porting Guide

> **Status:** Research document - not an active roadmap.
> This doc analyzes what it would take to port EggLedger to Android (APK) and, secondarily, iOS. It is intentionally thorough: the goal is to help a future developer understand the full scope before committing to any approach.

---

## Table of Contents

1. [Current Architecture Summary](#1-current-architecture-summary)
2. [Portability Audit](#2-portability-audit)
3. [The Core Problem: lorca](#3-the-core-problem-lorca)
4. [Android: Recommended Approach](#4-android-recommended-approach)
5. [Android: Step-by-Step Implementation Plan](#5-android-step-by-step-implementation-plan)
6. [Android: What Stays, What Changes, What Goes](#6-android-what-stays-what-changes-what-goes)
7. [iOS: Additional Steps Beyond Android](#7-ios-additional-steps-beyond-android)
8. [Alternative Approaches Considered](#8-alternative-approaches-considered)
9. [Effort Estimate](#9-effort-estimate)

---

## 1. Current Architecture Summary

EggLedger is a desktop app built as a single Go binary. The architecture has three layers:

```
┌────────────────────────────────────────────────────┐
│  Chrome / Firefox window  (managed by lorca)       │
│  ┌──────────────────────────────────────────────┐  │
│  │  Vue 3 SPA  (www/dist, served via HTTP)      │  │
│  │  - TypeScript, Tailwind CSS                  │  │
│  │  - Calls Go functions via JS bridge          │  │
│  └──────────────────────────────────────────────┘  │
│            WebSocket relay (lorca/relay.go)         │
├────────────────────────────────────────────────────┤
│  Go backend  (single binary, package main)         │
│  - 54 JS↔Go bindings (ui.MustBind)                │
│  - Local HTTP server on 127.0.0.1:EPHEMERAL        │
│  - Fetch pipeline, export logic                    │
├────────────────────────────────────────────────────┤
│  Storage & Network                                 │
│  - modernc.org/sqlite  (pure Go SQLite)            │
│  - Egg Inc API  (protobuf over HTTPS)              │
│  - Local filesystem (JSON prefs, CSV/XLSX exports) │
└────────────────────────────────────────────────────┘
```

The frontend is a standard Vue 3 SPA with no special desktop APIs. The backend is pure Go with no CGO (uses `modernc.org/sqlite`). The glue between them is **lorca**, which:

- Launches a Chrome or Firefox subprocess via `exec.Command`
- Opens a CDP (Chrome DevTools Protocol) or WebDriver BiDi session
- Establishes a WebSocket relay at `ws://127.0.0.1:RANDOM/`
- Injects a small JS shim that turns `await window.someBinding(args)` into round-trip RPC calls to Go

This whole mechanism is fundamentally desktop-only. It requires a native browser process that can be owned and controlled by the Go parent process.

---

## 2. Portability Audit

### Fully portable (zero changes needed)

| Package / File | Why it's portable |
|---|---|
| `ei/` | Protobuf-generated types + pure Go game logic extensions. Zero platform assumptions. |
| `eiafx/` | JSON config loader. Reads an embedded file at package `init`. |
| `api/` | HTTP client using `net/http`. Makes HTTPS calls to Egg Inc API. |
| `db/` | SQLite via `modernc.org/sqlite` (pure Go). No CGO. Schema migrations via `golang-migrate`. |
| `data.go` | API+DB bridge. Pure Go. |
| `fetch.go` | Fetch orchestration, worker pool. Pure Go. |
| `missionpacking.go` | Mission struct assembly and capacity analysis. Pure Go. |
| `export.go` | CSV and XLSX export. Pure Go. |
| `mennodata.go` | Downloads community drop-rate JSON. Pure Go. |
| `version.go` | GitHub release check. Pure Go. |
| `utils.go` | Time helpers, rank table. Pure Go. |
| `ledgerdata/` | Display string loader. Pure Go. |
| `www/` (Vue SPA) | Standard Vue 3 + TypeScript. No localStorage, no browser-specific APIs beyond what WebView supports. |

### Needs a new implementation (platform-specific)

| Component | Desktop implementation | Mobile equivalent |
|---|---|---|
| `lorca` (window + RPC bridge) | Browser subprocess + CDP/BiDi WebSocket | Android WebView + `@JavascriptInterface` (or message channel) |
| `platform/platform*.go` (`Hide`, `OpenFolderAndSelect`, `Open`) | `SetFileAttributes`, `osascript`, `xdg-open` | Android `Intent` for file/URL actions; `Hide` becomes a no-op |
| `open-golang` | Shell-level URL/file opening | Android `ACTION_VIEW` Intent |
| Storage paths (`_rootDir`, `_internalDir`) | Relative to executable | `context.getFilesDir()` / `context.getExternalFilesDir()` |
| Log rotation (lumberjack) | Writes to `logs/app.log` | Logcat or internal files directory |
| Export destination | `exports/` beside executable | External storage / `Downloads/` folder |
| App restart (`restartApp` binding) | `exec.Command(os.Executable())` | `PackageManager.getLaunchIntentForPackage()` |
| Window size / fullscreen settings | lorca window sizing | Activity window flags |
| `getDetectedBrowsers` binding | Scans filesystem for Chrome/Firefox executables | Not applicable - remove entirely |
| `setPreferredBrowser` / `getLoadedBrowser` | Browser path management | Not applicable - remove entirely |

### Cannot be used on mobile (drop entirely)

| Component | Reason |
|---|---|
| `lorca` dependency | Requires native browser process control |
| Browser detection bindings (`getDetectedBrowsers`, `setPreferredBrowser`, `getLoadedBrowser`) | No embedded browser subprocess on mobile |
| `restartApp` binding | No shell process management on Android |
| `appIsTranslocated` (macOS Gateway) | macOS-only concept |
| `appIsInForbiddenDirectory` | Desktop file permission concept |
| `DefaultResolution` / `DefaultScalingFactor` window settings | Managed by the Android OS, not the app |

---

## 3. The Core Problem: lorca

The single biggest blocker is lorca. The entire JS↔Go communication model depends on it.

**How lorca works on desktop:**

```
Go                           lorca relay              JS (in browser)
─────                        ──────────────           ────────────────
ui.Bind("foo", goFn)    →   relay stores fn
                             relay starts WS server
                             injects JS shim
                                                  ←   await window.foo(args)
goFn(args) called       ←   relay dispatches
result returned         →   relay serializes
                                                  →   promise resolves
```

The relay is a WebSocket server on `127.0.0.1`. Chrome's CDP connection lets lorca evaluate arbitrary JS and intercept function calls. None of this machinery exists on Android.

**The Android replacement:**

Android WebView exposes a first-class bridge called `@JavascriptInterface`. A Java/Kotlin object annotated with this annotation can expose methods directly to JS running in the WebView:

```kotlin
class GoJsBridge(private val goBackend: GoBackend) {
    @JavascriptInterface
    fun fetchPlayerData(playerId: String) {
        goBackend.fetchPlayerData(playerId)
    }
    // ... all 54 bindings
}
webView.addJavascriptInterface(GoJsBridge(backend), "Android")
```

From the Vue frontend, all `await window.someBinding(args)` calls need to become `Android.someBinding(args)`. This is the only pervasive frontend change required - everything else stays the same.

Push callbacks (Go → JS) work differently on Android. The pattern is:

```kotlin
// From Kotlin, call back into JS:
webView.post {
    webView.evaluateJavascript("window.updateState('FetchingMissions')", null)
}
```

---

## 4. Android: Recommended Approach

**Recommended: Go mobile library + Android WebView Activity**

This preserves the maximum amount of existing code while producing a real Android APK.

### High-level architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Android APK                                                │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  MainActivity.kt                                    │   │
│  │  - WebView filling the screen                       │   │
│  │  - Loads assets://index.html (or http://localhost)  │   │
│  │  - addJavascriptInterface(GoJsBridge, "Android")    │   │
│  └───────────────────┬─────────────────────────────────┘   │
│                      │ JNI                                  │
│  ┌───────────────────▼─────────────────────────────────┐   │
│  │  EggLedger Go library (gomobile bind)               │   │
│  │  - All existing backend packages unchanged          │   │
│  │  - New mobile-specific entry points                 │   │
│  │  - SQLite at getFilesDir()/data.db                  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### Toolchain

- **gomobile**: `golang.org/x/mobile/cmd/gomobile` - compiles Go code to an Android `.aar` library callable from Kotlin/Java via JNI. No C compiler needed (EggLedger is pure Go).
- **Android Studio**: For the Kotlin wrapper, build system, and APK signing.
- **Minimum SDK**: Android 9 (API 28) is a reasonable floor. WebView on Android 9+ supports ES2017+ without polyfills.

---

## 5. Android: Step-by-Step Implementation Plan

### Phase 1 - Extract the Go backend into a mobile-safe library

**1.1 Create a new Go package `mobile/` at the repo root.**

This package is the JNI boundary. It exposes simple string/bool/int methods (gomobile only supports primitive types and a limited set of interfaces across the JNI boundary).

```
mobile/
  android.go        -- Android-specific entry points
  callbacks.go      -- Push callback wiring
  paths.go          -- Mobile path resolution
```

Example shape of `mobile/android.go`:

```go
package mobile

// InitAndroid must be called once at app startup.
// dataDir is the Android app's getFilesDir().absolutePath.
func InitAndroid(dataDir string) error { ... }

// FetchPlayerData starts an async fetch. Progress is pushed
// back via the registered callbacks.
func FetchPlayerData(playerId string) { ... }

func StopFetching() { ... }

func ViewMissionsOfEid(eid string) string { ... } // returns JSON

// ... one method per existing MustBind call
```

**1.2 Audit and fix all desktop-specific assumptions in the core packages.**

Things to fix:
- `_rootDir` resolution in `main.go` (currently uses `os.Executable()` - move this to `mobile/paths.go` for Android)
- Log file paths - redirect to `dataDir/logs/`
- Export paths - redirect to `dataDir/exports/` or Android `Downloads/`

**1.3 Build the Go library with gomobile.**

```bash
gomobile bind -target=android -o eggleder.aar ./mobile
```

This produces `eggleder.aar` + `eggleder-sources.jar` - standard Android library files.

---

### Phase 2 - Create the Android project

**2.1 Create a new Android project in Android Studio.**

- Language: Kotlin
- Minimum SDK: API 28 (Android 9)
- Activity: Empty Activity (single `MainActivity.kt`)
- No Jetpack Compose needed (the UI is Vue)

**2.2 Add `eggleder.aar` as a local dependency.**

In `app/build.gradle`:
```gradle
dependencies {
    implementation fileTree(dir: 'libs', include: ['*.aar'])
    // ... standard Android deps
}
```

**2.3 Implement `MainActivity.kt`.**

Core responsibilities:
- Fill the screen with a `WebView`
- Configure WebView: `javaScriptEnabled`, `allowFileAccessFromFileURLs`, `domStorageEnabled`
- Copy the `www/dist` assets into the app's `assets/` folder at build time
- Load `file:///android_asset/index.html` (or serve from localhost if assets need dynamic routing)
- Call `eggleder.Mobile.InitAndroid(filesDir.absolutePath)` at startup
- Register `GoJsBridge` with `addJavascriptInterface`

**2.4 Implement `GoJsBridge.kt`.**

One method per Go binding. Each method calls the corresponding `eggleder.Mobile.*` function, then (for async results) calls back into JS via `webView.evaluateJavascript`.

```kotlin
class GoJsBridge(private val activity: MainActivity) {

    @JavascriptInterface
    fun fetchPlayerData(playerId: String) {
        // runs on background thread
        EggledgerMobile.fetchPlayerData(playerId)
    }

    @JavascriptInterface
    fun viewMissionsOfEid(eid: String): String {
        return EggledgerMobile.viewMissionsOfEid(eid)
    }

    fun pushStateUpdate(state: String) {
        activity.runOnUiThread {
            activity.webView.evaluateJavascript(
                "window.updateState('$state')", null
            )
        }
    }
    // ...
}
```

---

### Phase 3 - Adapt the Vue frontend

**3.1 Update `www/src/types/bridge.ts`.**

Change all bindings from `globalThis.X(args)` to `Android.X(args)`. The simplest approach is a thin adapter at the top of `bridge.ts`:

```typescript
// Use the Android bridge when available, fall back to globalThis (desktop)
const bridge: GoBindings = typeof Android !== 'undefined'
  ? Android as unknown as GoBindings
  : globalThis as unknown as GoBindings;
```

All composables (`useAppState.ts`, `useFetch.ts`, etc.) call through `bridge.*` instead of `globalThis.*`. This keeps the frontend compatible with both desktop (lorca) and Android.

**3.2 Remove desktop-only bindings from the frontend.**

These bindings have no Android equivalent and should be hidden or no-ops on mobile:
- `setPreferredBrowser` / `getPreferredBrowser` / `getLoadedBrowser` / `getDetectedBrowsers` (browser selection UI)
- `restartApp`
- `appIsTranslocated`
- `appIsInForbiddenDirectory`
- `getDefaultResolution` / `setDefaultResolution` / `getDefaultScalingFactor` / `setDefaultScalingFactor`
- `getStartInFullscreen` / `setStartInFullscreen`

The simplest approach: in `bridge.ts`, stub these out to no-ops when running on Android. Remove the corresponding settings UI panels when `platform === 'android'`.

**3.3 Adjust the export UX.**

On desktop, exported files open in a folder. On Android, the right pattern is a share sheet or save-to-Downloads. The `openFileInFolder` binding should become an Android `ACTION_SEND` or `ACTION_CREATE_DOCUMENT` intent.

**3.4 Rebuild the frontend.**

```bash
npm run build:css   # Tailwind
npm run build       # Vite
```

Copy `www/dist/` into the Android project's `app/src/main/assets/` directory. This can be automated in the Android Gradle build script.

---

### Phase 4 - Permissions and manifest

**`AndroidManifest.xml` additions:**

```xml
<uses-permission android:name="android.permission.INTERNET" />
<!-- For exporting files to external storage: -->
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE"
    android:maxSdkVersion="28" />
```

Android 10+ (API 29+) uses scoped storage; `WRITE_EXTERNAL_STORAGE` is not needed. Use `MediaStore` or `Storage Access Framework` for saving exports on API 29+.

---

### Phase 5 - Build, sign, distribute

```bash
# In Android Studio or via Gradle:
./gradlew assembleRelease
# Sign with keystore
# Output: app/build/outputs/apk/release/app-release.apk
```

Distribution options:
- Direct APK sideload (easiest, no store review)
- Google Play (requires Play Developer account, $25 one-time fee, review process)
- F-Droid (open source store, free, slower review)

---

## 6. Android: What Stays, What Changes, What Goes

### Stays unchanged

- `ei/`, `eiafx/`, `api/`, `db/` - zero modifications
- `data.go`, `fetch.go`, `missionpacking.go`, `export.go`, `mennodata.go`, `version.go`, `utils.go`, `storage.go`, `ledgerdata/`
- All Vue component logic
- All filter, display, and table rendering code
- SQLite schema and all migrations
- The Egg Inc API integration
- Menno data download
- GitHub update check

### Changes (modified, not replaced)

| Component | What changes |
|---|---|
| `main.go` | Split: desktop entry point stays; backend logic moves to `mobile/` |
| `storage.go` (`AppStorage`) | Remove browser/window fields; add mobile-irrelevant guards |
| `platform/` | Add `platform_android.go` with Intent-based `Open` and no-op `Hide` |
| `www/src/types/bridge.ts` | Add platform adapter; desktop unchanged, Android uses `Android.*` |
| Settings UI components | Hide desktop-only panels on Android |
| Export UX | `openFileInFolder` → Android share/save flow |

### Goes away (removed for mobile)

| Component | Reason |
|---|---|
| `lorca` dependency | Entire package replaced by Android WebView + `@JavascriptInterface` |
| `open-golang` dependency | Replaced by Android `Intent` |
| Browser selection settings (all bindings) | No embedded browser subprocess |
| `restartApp` binding | No process management |
| `appIsTranslocated` | macOS-specific |
| Window size / fullscreen settings | OS-controlled on Android |
| `main.go`'s `net.Listen` HTTP server | No longer needed when loading from `assets://` |

---

## 7. iOS: Additional Steps Beyond Android

iOS requires all of the Android work plus several additional layers of complexity.

### 7.1 Go on iOS: gomobile bind

The same `gomobile bind` approach works for iOS, targeting `arm64`:

```bash
gomobile bind -target=ios -o EggLedger.xcframework ./mobile
```

This produces an `.xcframework` bundle consumable by Xcode.

### 7.2 WKWebView instead of Android WebView

iOS uses `WKWebView` (WebKit). The bridge mechanism is different:

- **No `@JavascriptInterface` equivalent.** WKWebView uses `WKScriptMessageHandler` for JS→Swift calls, and `evaluateJavaScript()` for Swift→JS.
- JS calls Swift via `window.webkit.messageHandlers.handlerName.postMessage(data)`.
- All 54 bindings need a Swift handler registration and a corresponding message handler.

```swift
// Swift side
webView.configuration.userContentController
    .add(self, name: "fetchPlayerData")

// JS side (bridge.ts)
window.webkit.messageHandlers.fetchPlayerData.postMessage({ playerId })
```

Because WKWebView's `postMessage` is fire-and-forget (returns `void`, not a promise), **all Go bindings become async by default**. The Vue frontend would need a callback/event pattern for all return values, not just the async ones.

### 7.3 App Store constraints

- **Mandatory App Store distribution** (no sideloading without a developer account). Enterprise distribution requires a $299/year Apple Developer Enterprise Program membership.
- **App Store Review Guidelines** - all apps must pass review, which typically takes 1-3 days.
- **Privacy labels** - must declare data usage in the App Store listing (network calls to Egg Inc API, local storage).
- **No background process execution** - iOS strictly limits background work. The fetch pipeline would need to complete within a background task time budget (~30 seconds), or the user must keep the app in the foreground.
- **File export** - iOS uses `UIDocumentPickerViewController` for saving files. No direct access to a shared `Downloads/` folder.

### 7.4 Network security (iOS ATS)

iOS App Transport Security (ATS) requires all connections use HTTPS. The Egg Inc API (`ctx-dot-auxbrainhome.appspot.com`) already uses HTTPS, so this is not a blocker. But if any internal or test connections use plain HTTP, they will be blocked.

### 7.5 Xcode toolchain

- **Requires macOS** - Xcode is macOS-only. Cross-compiling iOS targets from Linux/Windows is not officially supported.
- **Certificates and provisioning profiles** - even for development builds, an Apple Developer account ($99/year Individual or $299/year Enterprise) is required.
- **Simulator** - Testing without a physical device requires the iOS Simulator (part of Xcode, macOS-only).

### 7.6 Summary: iOS delta over Android

| Item | Android | iOS (additional) |
|---|---|---|
| Go library | gomobile → `.aar` | gomobile → `.xcframework` |
| WebView bridge | `@JavascriptInterface` | `WKScriptMessageHandler` (more complex) |
| JS bridge type | Returns values synchronously | All async via postMessage |
| Frontend changes | Platform adapter | Additional async adapter for all return values |
| Build environment | Any OS | macOS + Xcode required |
| Distribution | APK sideload or Play | App Store only (without enterprise cert) |
| Developer cost | Free to sideload, $25 for Play | $99/year minimum |
| Background fetch | No time limit | ~30 second window |
| File export | `ACTION_CREATE_DOCUMENT` | `UIDocumentPickerViewController` |

---

## 8. Alternative Approaches Considered

### A. Flutter + FFI

Rewrite the UI in Flutter (Dart), keep Go as a native library accessed via Dart FFI.

- **Pros:** Single codebase for Android + iOS + desktop. Good performance. Strong UI toolkit.
- **Cons:** Rewrites the entire Vue frontend in Dart. Loses all existing frontend work. Dart FFI for Go requires careful memory management (no garbage-collected types across FFI).
- **Verdict:** Only makes sense if the plan is to also unify the desktop UI under Flutter. High effort.

### B. React Native + Go library

Rewrite the UI in React Native, call Go via JSI or a native module.

- **Pros:** React Native is closer to the existing Vue/TypeScript stack than Flutter.
- **Cons:** React Native's bridge to native Go is not first-class. Requires a native module (Java/Kotlin on Android, Swift on iOS) regardless. Still a UI rewrite.
- **Verdict:** More complexity than the recommended approach for similar results.

### C. Full native rewrite (Kotlin/Swift)

Rewrite everything in platform-native languages, calling the Egg Inc API directly.

- **Pros:** Best platform integration, no bridge layer.
- **Cons:** Two completely separate codebases. All business logic (fetch pipeline, export, capacity analysis) rewritten twice. The existing Go code is all portable and should be reused.
- **Verdict:** Maximum effort, minimum reuse. Not recommended.

### D. Capacitor / Cordova

Wrap the existing Vue app in a Capacitor shell (a thin native wrapper around a WebView).

- **Pros:** The Vue frontend requires almost zero changes. Capacitor has plugins for file I/O, sharing, etc.
- **Cons:** The Go backend cannot run inside a Capacitor app. All backend logic would have to be rewritten in TypeScript and run in-process in the WebView, or moved to a remote server. Given that the Egg Inc API requires a player EID and all data is user-local, a remote server is a privacy concern.
- **Verdict:** Works only if the Go backend is eliminated entirely and rewritten in TypeScript. Feasible but loses type safety from protobuf and requires duplicating the fetch/cache logic.

### E. Progressive Web App (PWA)

Serve EggLedger as a website that users can add to their home screen.

- **Pros:** No APK distribution. Works on any platform with a browser. Zero native code.
- **Cons:** PWAs cannot make cross-origin HTTP requests to the Egg Inc API from a browser context without CORS cooperation from Auxbrain (which does not exist - the API returns no CORS headers). A server-side proxy would be required, which introduces privacy implications (player EIDs flowing through a third-party server).
- **Verdict:** The CORS blocker makes this impractical without a proxy.

---

## 9. Effort Estimate

Rough estimates assume a developer already familiar with the codebase.

| Phase | Android | iOS (additional) |
|---|---|---|
| Phase 1: Extract Go mobile library | ~3-5 days | +0 days (shared) |
| Phase 2: Android project + WebView wrapper | ~3-4 days | +2-3 days (Xcode + WKWebView) |
| Phase 3: Vue frontend bridge adapter | ~1-2 days | +1 day (async adapter) |
| Phase 4: Permissions, export UX | ~1 day | +1-2 days (ATS, UIDocumentPicker) |
| Phase 5: Build, sign, test | ~1-2 days | +2-3 days (Xcode certs, provisioning) |
| **Total** | **~9-13 days** | **+6-9 additional days** |

This does not include:
- Setting up CI for Android/iOS builds
- App Store submission and review iterations
- Tablet / iPad layout adaptation
- Accessibility work
- Localization

---

## Appendix: Files to Create / Modify at a Glance

```
New files:
  mobile/android.go              -- Go mobile entry points
  mobile/callbacks.go            -- Push callback registration
  mobile/paths.go                -- Platform-aware path resolution
  platform/platform_android.go  -- Android Hide/Open stubs
  android/                       -- Android Studio project root
    app/
      src/main/
        java/com/example/eggleder/
          MainActivity.kt
          GoJsBridge.kt
        assets/                  -- copy of www/dist/
    build.gradle
    settings.gradle

Modified files:
  www/src/types/bridge.ts        -- Platform adapter layer
  storage.go                     -- Remove browser/window fields
  main.go                        -- Thin desktop entry point only
```
