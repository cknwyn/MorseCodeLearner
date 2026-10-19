# MorseCodeLearner

An Assembly-accelerated Morse code assistive and pedagogical desktop platform built with .NET 8 WPF and MASM x64.

## System Architecture

The application adopts a decoupled hybrid architecture:
- Presentation Layer: C# / .NET 8 WPF (MorseApp.exe) provides accessible high-contrast UI, responsive switch handling, and pedagogical drill modes.
- Computational Core: MASM x64 (MorseCore.dll) executes time-critical tasks with deterministic low latency:
  - O(1) ASCII to Morse lookup table
  - O(1) Flattened Dichotomic tree reverse decoder
  - ITU-R M.1677-1 microsecond single-switch quantizer
  - Real-time idle gap evaluator for character and word boundaries
  - Cycle-accurate RDTSC latency telemetry

## Modes of Operation

1. Learning Drills: Practice character recognition (A-Z, 0-9) with phonetic mnemonics, real-time feedback, and streak tracking.
2. Sentence Challenge: Interactive phrase transcription with visual character progression and automated pause-to-space detection.
3. Freeform Sandbox: Open telegraph dictation with live 700 Hz sidetone audio synthesis and clipboard export.

## Prerequisites

- Windows 10 / 11 x64
- .NET 8.0 SDK (or later)
- Visual Studio 2022 (with MASM build tools)

## Build and Run

### Command Line (.NET CLI)

From the project root:

```cmd
dotnet build MorseCodeLearner.sln -c Debug
dotnet run --project src/MorseApp/MorseApp.csproj
```

The build automatically invokes `ml64.exe` to assemble all native routines into `MorseCore.dll` before launching the WPF presentation layer.

### Visual Studio 2022

1. Open `MorseCodeLearner.sln` in Visual Studio 2022.
2. Set configuration to `Debug` or `Release` and platform to `x64`.
3. Press `F5` to build and run.