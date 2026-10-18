# PROJECT_HANDOFF.md — Collaborator Onboarding & Handoff Guide

Welcome to the **MorseCodeLearner** project repository! This guide is designed to get new developers, researchers, and contributors up to speed immediately.

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
   * Visual Studio invokes `ml64.exe` to assemble the native assembly files into `MorseCore.dll`.
   * It then compiles the C# presentation layer into `MorseApp.exe`.
4. Right-click **`MorseApp`** in Solution Explorer and ensure it is selected as the **Startup Project**.
5. Press **F5** to run the live application.

---

## 2. Current Implementation Status

| Component | Status | Description |
| :--- | :---: | :--- |
| **MASM Core (`MorseCore.dll`)** | **COMPLETE** | Assembled with `ml64.exe`. Includes $O(1)$ ASCII table, Flattened Dichotomic tree decoder, microsecond quantizer, and `RDTSC` cycle counter. |
| **P/Invoke Interop Bridge** | **COMPLETE** | Zero-copy fastcall ABI calls configured with automatic resolution via `NativeLibrary.SetDllImportResolver`. |
| **Mode 1: Learning Drills** | **COMPLETE** | Real-time letter practice (A–Z, 0–9), phonetic hints, accuracy percentage, and streak tracking. |
| **Mode 2: Sentence Challenge** | **COMPLETE** | Guided transcription with character-level color highlighting (Green/Yellow/Gray), presets, and custom phrase support. |
| **Mode 3: Freeform Sandbox** | **COMPLETE** | Open telegraph dictation, copy-to-clipboard, and live 700 Hz audio playback synthesis. |
| **1-Second Space Detection** | **COMPLETE** | Pausing for 1.0 second automatically commits word spaces (`␣`) without requiring special keys. |
| **Settings & Telemetry** | **COMPLETE** | Live WPM adjustment (5–30 WPM), 700 Hz sidetone, and toggleable hardware latency stats. |

---

## 3. Critical Solved Pitfalls (Read Before Editing Code!)

### ⚠️ 1. `ml64.exe` File Encodings (BOM Hazard)
* **The Rule**: All `.asm` and `.inc` files **MUST be saved as pure 7-bit ASCII or UTF-8 without BOM**.
* **The Pitfall**: PowerShell's default `Set-Content -Encoding UTF8` prepends a 3-byte Byte Order Mark (`0xEF, 0xBB, 0xBF`). `ml64.exe` cannot parse BOMs and will throw `syntax error : invalid character in file` and `must be in segment block`.

### ⚠️ 2. MASM Segment Directives
* **The Rule**: Use **`.const`** (or `.data`) for read-only static tables.
* **The Pitfall**: Never use `.rdata` in MASM source files. `.rdata` is not a MASM directive and causes `syntax error : .`. MASM automatically emits `.const` items into the `.rdata` PE section.

### ⚠️ 3. Spacebar Button Focus in WPF
* **The Rule**: All interactive buttons must have **`Focusable="False"`**, and Spacebar must be intercepted in **`OnPreviewKeyDown`** and **`OnPreviewKeyUp`** with `e.Handled = true`.
* **The Pitfall**: If buttons have focus, Windows defaults to clicking the focused button on Spacebar press, which steals keypresses from the Morse engine.

### ⚠️ 4. Dichotomic Tree Bit Order (MSB vs LSB)
* **The Rule**: User keypresses must be accumulated **MSB to LSB**:
  ```csharp
  _patternMask = (byte)((_patternMask << 1) | bitVal);
  ```
* **The Pitfall**: Shifting bits into the least significant position (`LSB`) inverts the tree traversal (e.g., `A` (`.-`) gets decoded as `N` (`-.`)).

### ⚠️ 5. Native DLL Loading
* **The Rule**: `Program.cs` registers an automatic **`NativeLibrary.SetDllImportResolver`** on startup.
* **The Pitfall**: You do not need to manually copy `MorseCore.dll` to build folders; the custom resolver searches the solution tree automatically.

---

## 4. Next Roadmap & Contribution Tasks

If you are collaborating on the next phase of development, here are the priority objectives:

1. **Empirical Benchmarking Harness (`MorseBenchmark`)**:
   * Create a CLI benchmarking tool using `BenchmarkDotNet` to record nanosecond execution times comparing:
     * Native MASM $O(1)$ table lookup vs C# `Dictionary<char, string>`.
     * Native Dichotomic tree decoder vs C# string regex matching.
   * Read the hardware cycle counter via `Morse_GetBenchmarkCycles` (`RDTSC`).

2. **Telemetry Jitter Logger (`docs/experiments/`)**:
   * Add a "Record Session" button to log keystroke intervals ($\Delta t$) and jitter deltas into a local `.csv` file for statistical analysis in Chapter 4 of the thesis.

3. **WASAPI Audio Synthesis Engine**:
   * Implement a low-latency unbuffered WASAPI audio sink with smooth 5ms cosine attack/decay ramps to eliminate potential sound clicks on audio interfaces that struggle with Win32 `Beep`.

4. **Academic Thesis Alignment**:
   * Refer to `docs/Chapter3_Draft.md` to update implementation diagrams and timing tables for documentation submissions.
