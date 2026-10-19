# Chapter 2 / 3 Paper Corrections (checked against commit on `experiment-jm`)

Statements in the submitted text that no longer match the code, with the wording the code supports. Verified by reading the sources and running `src/MorseCore.Tests`.

| # | Paper / review statement | What the code actually does | Suggested wording |
| :-: | :-- | :-- | :-- |
| 1 | Exports named `EncodeChar_MASM`, `DecodeChar_MASM`, `QuantizeDuration_MASM`, `BenchmarkLookup_MASM` | Exports are `Morse_EncodeChar`, `Morse_EncodeString`, `Morse_DecodeChar`, `Morse_QuantizePress`, `Morse_EvaluateGap`, `Morse_GetBenchmarkCycles` (`MorseCore.def`, `NativeMethods.cs`) | Use the real names in text and in the architecture diagram. |
| 2 | "Packing element counts and bitstreams into single 8-bit registers" | One **16-bit word** per character: AL = element count (1-6), AH = MSB-first pattern (0 = dit, 1 = dah). Count 6 is used only for `. , ?` | "A 16-bit packed word: element count in the low byte, pattern bits in the high byte." |
| 3 | "One to five temporal elements" | Letters 1-4, digits 5, punctuation (`. , ?`) 6 | "One to six elements." |
| 4 | Static tables in `.rdata` | Source declares `.const`; MASM places it in the read-only `.rdata` PE section | "Read-only data (`.const`, emitted into `.rdata`)." |
| 5 | Audio via `Kernel32!Beep` | `PcmAudioEngine` (winmm `waveOut`, looping 16-bit sine, start/stop from a background thread). `Beep` is no longer in the code | "PCM sidetone via the Windows multimedia `waveOut` API." |
| 6 | Timestamps via `QueryPerformanceCounter` | `System.Diagnostics.Stopwatch` (which uses QueryPerformanceCounter internally); RDTSC for native call telemetry | "High-resolution `Stopwatch` timestamps." |
| 7 | Character gap > 3 units, word gap > 7 units (or 300 ms / 1 s) | `Morse_EvaluateGap`: >= 2.5T commits a character, >= 6T commits a space (T = 1,200,000 / WPM us) | "Thresholds of 2.5 and 6 units, placed between the ideal 1/3/7-unit gaps to tolerate human timing error." |
| 8 | Dit/dah thresholds in managed code (review note) | All classification is native (`Morse_QuantizePress`: T/4 debounce, < 2T dit, <= 6T dah, > 6T invalid). Managed code only converts WPM to T | Keep the paper's claim; point to `quantizer.asm` and `MorseEngineService.cs`. |
| 9 | C++ `dllmain.cpp` / `pch.cpp` in the DLL | Removed. The DLL is built by `build-native.ps1` with `ml64` + `link /DLL /NOENTRY /NODEFAULTLIB`; `dllmain.asm` is an unused stub | "A pure-assembly DLL linked without the C runtime." |
| 10 | "Configurable font scaling, high-contrast modes" | Not implemented. The app has light/dark themes, redundant audio + visual cues and a live key scope; contrast was hand-estimated (>= 4.5:1), not audited | Describe the implemented features; list font scaling, a high-contrast theme and screen-reader names as future work. |
| 11 | "Tracking accuracy and timing consistency" | Accuracy and streaks are tracked; timing is shown live (key scope, hold readout) but not recorded | "Accuracy tracking with live timing feedback; session timing logs are future work." |
| 12 | Modules: Level 1 characters, Level 2 words/phrases | Also Mode 3 Sandbox and Mode 4 Ear Training (10-lesson study path, mastery model) | Add the two modes. |
| 13 | Architecture diagram labels | Layering is `MainWindow` -> `MorseEngineService` -> `NativeMethods` -> `MorseCore.dll`; audio box = `PcmAudioEngine`; add Key Scope and Ear Training | Redraw labels to match (see `CONTEXT.md` section 2). |

## Chapter 3 checklist
* **Code with inline comments:** `src/MorseCore/core/*.asm` and `tables/lookup_tables.asm` (header and per-instruction comments added to the routines; behaviour unchanged).
* **Diagram:** use the layering above.
* **Test cases:** `docs/Chapter3_TestCases.md` (158 checks, all passing at the time of writing) from `src/MorseCore.Tests`.
