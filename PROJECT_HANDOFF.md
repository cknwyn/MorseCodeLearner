# PROJECT_HANDOFF.md — Collaborator Onboarding & Handoff Guide

Welcome to the **Morseling** project repository (the product was renamed from "Morse Code Learner"; the repo, solution and `MorseApp` namespace keep their original names)! This guide is designed to get new developers, researchers, and contributors up to speed immediately.

---

## 1. Quick Start Guide (Build & Run in 2 Minutes)

### Prerequisites
* **Windows 10 / 11 x64**
* **Visual Studio 2022** with:
  * `.NET desktop development` workload (.NET 8.0 SDK)
  * `Desktop development with C++` workload (with **MASM** build tools enabled)

### Build Instructions
1. Open **`MorseCodeLearner.sln`** in Visual Studio 2022.
2. Ensure the top toolbar platform configuration is set to **`x64`** (do not use `Win32` or `Any CPU`).
3. Press **`Ctrl + Shift + B`** (Rebuild Solution).
   * `MorseApp.csproj` runs `src/MorseCore/build-native.ps1` first, which calls `ml64.exe` on the `.asm` files and `link.exe` (`/DLL /NOENTRY /NODEFAULTLIB /DEF:MorseCore.def`) to produce `MorseCore.dll`. There is no C/C++ project in the solution; the native core is pure MASM.
   * It then compiles the C# presentation layer into `MorseApp.exe`.
4. Right-click **`MorseApp`** in Solution Explorer and ensure it is selected as the **Startup Project**.
5. Press **F5** to run the live application.

Command-line build (used while developing the UI): `dotnet build src/MorseApp/MorseApp.csproj -p:Platform=x64`. **Close the running `MorseApp.exe` first**, otherwise the native-DLL link step fails with `LNK1104: cannot open file ...MorseCore.dll`.

---

## 2. Current Implementation Status

| Component | Status | Description |
| :--- | :---: | :--- |
| **MASM Core (`MorseCore.dll`)** | **COMPLETE** | Assembled with `ml64.exe`. Includes $O(1)$ ASCII table, Flattened Dichotomic tree decoder, microsecond quantizer, and `RDTSC` cycle counter. |
| **P/Invoke Interop Bridge** | **COMPLETE** | Zero-copy fastcall ABI calls configured with automatic resolution via `NativeLibrary.SetDllImportResolver`. |
| **Mode 1: Learning Drills** | **COMPLETE** | Real-time letter practice (A–Z, 0–9), phonetic hints, accuracy percentage, streak tracking, and a clickable 36-character alphabet strip that shows what you have mastered this session. |
| **Mode 2: Sentence Challenge** | **COMPLETE** | Guided transcription with character-level highlighting, presets, custom phrases, and a "full transmission" line showing the whole sentence in dits and dahs. |
| **Mode 3: Freeform Sandbox** | **COMPLETE** | Open telegraph dictation, copy-to-clipboard, live audio playback, an empty-state hint, and a Quick Reference chart (A–Z, 0–9) beside the stream. |
| **Mode 4: Ear Training** | **NEW** | Listening drill: a character is played in Morse, the user names it. Two tabs: **Study path** (10 lessons) and **Free practice** (Letters / Numbers filters). See §3. |
| **Live Key Scope** | **NEW** | Scrolling logic-analyser trace of the key line in the bottom console, visible in every mode. See §4. |
| **Light / Dark theme** | **NEW** | Light is the default; a rail button switches to dark with an animated colour fade. See §5. |
| **Collapsible sidebar + responsive layout** | **NEW** | Left rail collapses to icons; panels reflow for narrow and short windows. See §6. |
| **Automatic Space Detection** | **COMPLETE** | Unit-relative gaps evaluated by `Morse_EvaluateGap`: idle ≥ 2.5T commits the character, ≥ 6T commits a word space (≈ 200 ms / 480 ms at 15 WPM, 600 ms / 1440 ms at 5 WPM). |
| **Automated tests** | **NEW** | `src/MorseCore.Tests` (xUnit): 110 cases against the real DLL; table in `docs/Chapter3_TestCases.md`. Run `dotnet test src/MorseCore.Tests/MorseCore.Tests.csproj -p:Platform=x64`. |
| **Settings & Telemetry** | **COMPLETE** | Live WPM adjustment (5–30 WPM), sidetone 300–1500 Hz, and toggleable hardware latency stats. Controls moved from the header to the top bar above the workspace. |

---

## 3. Mode 4: Ear Training (new)

**Where**: `PanelListen` in `MainWindow.xaml`; logic in the `#region Mode 4: Ear Training` block of `MainWindow.xaml.cs`.

### Drill loop
1. A target character is picked and **auto-played** at the current WPM and sidetone (background thread, `PlaySequence`). "Play sound" replays it.
2. The user answers by **clicking a character chip, typing the character on the keyboard, or keying it in Morse** with Space (decoded characters are routed through `DispatchDecodedCharacter` → `SubmitGuess`).
3. Wrong guesses grey out the chip and allow a retry. "Show answer" reveals the answer and counts as a miss. **Enter** replays (before answering) or advances (after).
4. After answering, the **dit/dah diagram** (circles and pills), the Morse text and the phonetic are shown.
5. Score is "first try" hits / rounds plus streak (session only).

### Two tabs
* **Free practice** — **Letters** and **Numbers** toggle buttons (at least one always stays on). Answer chips shrink to the active set.
* **Study path** — 10 lessons (`Lessons` array): 1 E T · 2 I M S O · 3 A N R W · 4 D K G U · 5 L P J B · 6 X C Y Z · 7 Q F H V · 8 Numbers 1–5 · 9 Numbers 6–0 · 10 Full review.
  * **Hub**: lesson cards with characters + patterns, progress bar, "Next up" / "Mastered" badges, and a **Reset progress** button.
  * **Study guide** (lessons 1–9): written tip, one card per character with diagram, phonetic, auto-computed relations ("Reverse of N", "Opposite of T" — derived from the actual patterns via `Relations()`), per-character **Hear** and **Hear all**.
  * **Drill**: same loop as above restricted to the lesson. Answer chips are the lesson characters plus all earlier ones. ~20 % of rounds are **review** picks from earlier lessons.

### Mastery model (repetition / familiarity)
* Each character has a level `0..3` (`MasteryTarget = 3`).
* **First-try correct → +1.** First wrong guess or "Show answer" in a round → **−1** (applied once per round).
* Next-character selection is **weighted toward weak characters** (`Weight = 4 − level`) and never repeats the previous one.
* A lesson is mastered when every character reaches level 3 → "Lesson mastered!" message and a **Next lesson** button.
* **Persistence**: `%AppData%\MorseCodeLearner\ear-progress.json` (`{ "A": 2, "B": 0, ... }`). Load/save errors are swallowed on purpose; progress must never crash the trainer.

### Audio timing
`PlaySequence` uses a stopwatch-based `PreciseDelay` and the app calls `timeBeginPeriod(1)` on startup (`timeEndPeriod(1)` on close) so 1 ms sleeps are accurate; otherwise short units at high WPM jitter by ~15 ms.

---

## 4. Live Key Scope (new)

**File**: `src/MorseApp/Controls/KeyScope.cs` (custom `FrameworkElement`, drawn with `DrawingContext`, redrawn from `CompositionTarget.Rendering`). Placed in the bottom console as `<c:KeyScope x:Name="KeyScope" />`.

* Time flows right to left; the right edge is **NOW**. A raised bar = key down. Bars are coloured **dit (orange/lime), dah (blue), invalid/too long (red)**.
* **Dotted gridlines every Morse unit** (`1 unit = 1200 / WPM ms`), so a dit should fill one cell and a dah three. The window is `WindowUnits` (42) units wide, so it **rescales automatically when WPM changes**. A legend in the corner shows ghost "dit 1u" and "dah 3u" bars.
* **API**: `Begin(timestamp?)`, `End(kind, timestamp?)`, `UnitMs`, `LiveClassifier`. `timestamp` is a `Stopwatch.GetTimestamp()` value so background playback threads can record exact times.
* **Live readout** (`KeyReadout`, above the scope): while Space is held it shows `HOLD 142 ms → DAH`, using the **same MASM `Morse_QuantizePress`** that decodes the key, so what the learner sees matches what the app decides. After release it shows the last press. When idle it shows the unit/dit/dah lengths for the current WPM.
* Fed by: Space key down/up (`OnPreviewKeyDown/Up`) and Ear Training playback.

---

## 5. Theming (new)

* All colours are **brush resources** (`Bg, Rail, Surface, Raised, Line, Text, Muted, Signal, Accent, Ice, Mint, Alert, OnSignal, OnMint, Well`) referenced with **`DynamicResource`**. The `Palette` array in `MainWindow.xaml.cs` holds the light and dark value for each key; `ApplyTheme()` tweens between them (~280 ms).
* **Light (default)**: warm gray-cream, off-white cards, near-black text, deep orange/blue/green accents. Tuned for lower glare and ≥ 4.5:1 contrast (muted text ≈ 6:1). `Accent` is the text-safe accent; `Signal` is for fills only.
* **Dark**: graphite + electric lime.
* Colour for dynamically created elements and for anything set from code **must use `SetResourceReference`** (helper: `Ref(element, property, "Key")`), never a hard-coded brush, or it will not follow theme changes.

---

## 6. Layout, Navigation & Responsiveness (new)

* **Left rail** (`RailBorder`, 256 px ↔ 78 px, animated): wordmark (blinking dit-dah logo), four mode buttons (`TabLearningBtn`, `TabSentenceBtn`, `TabSandboxBtn`, `TabListenBtn`), theme toggle, collapse toggle. Collapsed it shows only the `01–04` numbers (tooltips keep the names).
* **Top bar**: scrolling Morse ticker (drawn from real shapes spelling "MORSE CODE LEARNER"), SPEED and TONE steppers, Telemetry toggle.
* **Bottom console**: signal lamp, **key scope**, key buffer, and the toggleable telemetry card.
* **Responsive rules** (`ApplyResponsive()`, called on `SizeChanged`):
  * Window width < 1120 → rail auto-collapses.
  * Main area < 640 px → Learning/Ear Training stack the tile above the info.
  * Main area < 860 px → telemetry drops under the scope.
  * Window height < 720 → lamp shrinks, titles and alphabet strips hide, tile shrinks.
  * Sandbox Quick Reference hides below 780 px.
  * Min window size is 860 × 660.
* Visual extras: glass-style cards over blurred colour blobs, page-load reveal animation, breathing lamp ring, button hover/press feedback.

---

## 7. Critical Solved Pitfalls (Read Before Editing Code!)

### ⚠️ 1. `ml64.exe` File Encodings (BOM Hazard)
* **The Rule**: All `.asm` and `.inc` files **MUST be saved as pure 7-bit ASCII or UTF-8 without BOM**.
* **The Pitfall**: PowerShell's default `Set-Content -Encoding UTF8` prepends a 3-byte Byte Order Mark (`0xEF, 0xBB, 0xBF`). `ml64.exe` cannot parse BOMs and will throw `syntax error : invalid character in file` and `must be in segment block`.

### ⚠️ 2. MASM Segment Directives
* **The Rule**: Use **`.const`** (or `.data`) for read-only static tables.
* **The Pitfall**: Never use `.rdata` in MASM source files. `.rdata` is not a MASM directive and causes `syntax error : .`. MASM automatically emits `.const` items into the `.rdata` PE section.

### ⚠️ 3. Spacebar Button Focus in WPF
* **The Rule**: All interactive buttons must have **`Focusable="False"`**, and Spacebar must be intercepted in **`OnPreviewKeyDown`** and **`OnPreviewKeyUp`** with `e.Handled = true`.
* **The Pitfall**: If buttons have focus, Windows defaults to clicking the focused button on Spacebar press, which steals keypresses from the Morse engine.
* **Still true after the redesign**: the shared `KeyBtn` style sets `Focusable=False`; any button you create in code must use that style (`DynButton()` does). Letter keys are only captured by the Ear Training drill view; Space is always the Morse key.

### ⚠️ 4. Dichotomic Tree Bit Order (MSB vs LSB)
* **The Rule**: User keypresses must be accumulated **MSB to LSB**:
  ```csharp
  _patternMask = (byte)((_patternMask << 1) | bitVal);
  ```
* **The Pitfall**: Shifting bits into the least significant position (`LSB`) inverts the tree traversal (e.g., `A` (`.-`) gets decoded as `N` (`-.`)).

### ⚠️ 5. Native DLL Loading
* **The Rule**: `Program.cs` registers an automatic **`NativeLibrary.SetDllImportResolver`** on startup.
* **The Pitfall**: You do not need to manually copy `MorseCore.dll` to build folders; the custom resolver searches the solution tree automatically.

### ⚠️ 6. WPF resource brushes are frozen (theme switching)
* **The Pitfall**: Brushes declared in `Window.Resources` end up frozen, so `brush.BeginAnimation(Color…)` / `brush.Color = …` throws *"Cannot animate … because the object is sealed or frozen"* (this crashed the app once).
* **The Rule**: Change a theme by **replacing** the resource (`Resources[key] = new SolidColorBrush(c)`), as `SetPalette()` does; `DynamicResource` consumers update automatically. Gradient stops use separate `Color` resources (`SignalColor`, `IceColor`, `RaisedColor`, `WellColor`) bound with `DynamicResource`.

### ⚠️ 7. XAML template gotchas hit during the redesign
* Inside a `Style`, a template is set with `<Setter Property="Template"><Setter.Value><ControlTemplate>…`. `<Setter.Template>` does not exist (error MC3074).
* A `Trigger` cannot target a named **transform** (error MC4111). Set `RenderTransform` on the element instead (see the press-scale in `KeyBtn`).

### ⚠️ 8. Timer resolution for audible timing
* `Thread.Sleep` granularity is ~15 ms by default. The app calls `timeBeginPeriod(1)` in the constructor and `timeEndPeriod(1)` in `OnClosed`; keep them paired if you touch startup/shutdown.

### ⚠️ 9. Editing the code-behind from scripts
* `MainWindow.xaml.cs` uses **CRLF** line endings. Multi-line `string.Replace` patches silently fail to match when written with LF; use regexes with `\r?\n` or edit in the IDE. Keep the file saved as UTF-8 **with BOM**.

---

## 8. Source Map (UI layer)

| Path | Purpose |
| :--- | :--- |
| `src/MorseApp/MainWindow.xaml` | Entire UI: theme resources, styles (`KeyBtn`, `SignalBtn`, `NavBtn`, `StepBtn`, `Card`, `Eyebrow`, `H1`), rail, top bar, four mode panels, key console. |
| `src/MorseApp/MainWindow.xaml.cs` | Window logic. Regions: *Key scope helpers*, *Mode 4: Ear Training*, *Filler content* (alphabet chips, quick reference, full-morse line), *Theme, Rail, Responsive Layout*, *Navigation & Settings*, *Hardware Key Event Processing*, one region per mode. |
| `src/MorseApp/Native/NativeMethods.cs` | The only `[DllImport("MorseCore.dll")]` declarations (Cdecl). Names must match `src/MorseCore/MorseCore.def`. |
| `src/MorseApp/Services/MorseEngineService.cs` | Managed gateway used by `MainWindow`: `UnitUs(wpm)`, `EncodePacked`, `DecodeRaw`, `QuantizePressUs`, `EvaluateGapUs`, `GetCycles`. **Do not add timing thresholds here or in the UI; classification stays in the MASM core.** |
| `src/MorseCore/core/*.asm`, `tables/lookup_tables.asm` | MASM sources (inline-commented). `dllmain.asm` is a stub because the DLL is linked `/NOENTRY`. |
| `src/MorseCore.Tests/` | xUnit tests calling the real DLL; `MORSE_TEST_REPORT=<file>` regenerates the results table. |
| `src/MorseApp/Controls/KeyScope.cs` | Live key scope control (§4). |
| `src/MorseApp/Audio/PcmAudioEngine.cs` | winmm `waveOut` looping sine sidetone with `StartTone()` / `StopTone()` and adjustable frequency (300–1500 Hz). |
| `%AppData%\MorseCodeLearner\ear-progress.json` | Saved Ear Training mastery (created at runtime). |

---

## 9. Known Gaps / Not Yet Verified

* Audio output was **not heard** during UI testing (visual checks only); confirm the Ear Training sound is audible and correctly timed on your machine.
* The tone starts and stops abruptly (no attack/decay), so a click is possible on some audio interfaces — see roadmap item 3.
* Ear Training mastery persistence, level up/down rules and the lesson-complete flow were implemented but not exercised end-to-end.
* Narrow/short window layouts were checked in a few sizes only; the Ear Training drill scrolls slightly in the smallest windows.
* The Learning mode "mastered" markers are **session-only** (not persisted); only Ear Training mastery is saved.
* UI changes are uncommitted on the `experiment-jm` branch at the time of writing.

---

## 10. Next Roadmap & Contribution Tasks

If you are collaborating on the next phase of development, here are the priority objectives:

1. **Empirical Benchmarking Harness (`MorseBenchmark`)**:
   * Create a CLI benchmarking tool using `BenchmarkDotNet` to record nanosecond execution times comparing:
     * Native MASM $O(1)$ table lookup vs C# `Dictionary<char, string>`.
     * Native Dichotomic tree decoder vs C# string regex matching.
   * Read the hardware cycle counter via `Morse_GetBenchmarkCycles` (`RDTSC`).

2. **Telemetry Jitter Logger (`docs/experiments/`)**:
   * Add a "Record Session" button to log keystroke intervals ($\Delta t$) and jitter deltas into a local `.csv` file for statistical analysis in Chapter 4 of the thesis. The new `KeyScope` already receives every press start/end timestamp, so it is a natural hook point.

3. **Audio engine upgrade**:
   * The current engine is a `waveOut` loop (`PcmAudioEngine`). Implement a low-latency WASAPI sink with smooth ~5 ms cosine attack/decay ramps to remove start/stop clicks.

4. **Study path polish**:
   * Persist Learning-mode mastery too, add per-lesson timing/accuracy history, and optional Farnsworth spacing (slower gaps, normal element speed) for beginners.

5. **Accessibility pass**:
   * Add a high-contrast theme, a UI scale option, and screen-reader names for the generated chips/cards (the audience includes users with impairments).

6. **Academic Thesis Alignment**:
   * Use `docs/Chapter3_TestCases.md` (generated verification table) and the layering in `CONTEXT.md` §7 to update diagrams and timing tables. `docs/Paper_Corrections.md` lists the Chapter 2 statements that must be reworded to match the code.
