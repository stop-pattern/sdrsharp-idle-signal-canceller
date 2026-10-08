# Idle Signal Canceller Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. User authorized continuous execution without further review gates; no push.

**Goal:** Build, debug and package an SDR# audio idle-tone muting plugin with GitHub CI.
**Architecture:** An independent DSP core detects a persistent tone and gates audio. An SDR# adapter owns stream lifecycle, and a WinForms panel displays state and applies immutable settings.
**Tech Stack:** C#/.NET 9, WinForms, dependency-free executable tests, Python standard library for bundle extraction and anonymized audio evaluation, PowerShell and GitHub Actions.
**Spec:** `docs/superpowers/specs/2026-10-09-idle-signal-canceller-design.md`

## Global Constraints

- Center 1500 Hz, tolerance ±50 Hz; startup disabled; disable passes the next buffer unchanged.
- Analyze pre-mute samples; no UI calls, disk I/O, waits or steady-state allocation on the audio thread.
- Reset on stream changes; unregister and dispose on close.
- Reference actual SDR# assemblies. Never distribute host DLLs or captured data.
- Only repository content and anonymous numerical waveform results may be committed; no remote push.

## Review Focus

- Stereo layout must not halve the analyzed frequency; characterize the actual host hook.
- Noise around a weak tone must not cause silence/noise-only muting.
- Invalid/non-finite samples and settings must not latch mute indefinitely.
- Stream changes and disabled processing must clear stale detection before resuming.
- CI must build against actual host assemblies and package only our DLLs.

### Task 1: Detector and core setup

**Files:** `Directory.Build.props`, `NuGet.Config`, `src/IdleSignal.Core/*.cs`, `tests/IdleSignal.Tests/*`.
**Interfaces:** `DetectorSettings` immutable validated options; `ToneDetector(double sampleRate, DetectorSettings settings)`; `Push(float sample)` returns whether a frame completed; `IsIdle`, `ProminenceDb`, `LevelDb`, `Reset()`.

- [ ] Write executable tests: pure and noisy 1500 Hz become idle; 1000/2000 Hz, silence, white noise and speech-like mixture do not; tolerance endpoints work; short tone does not latch.
- [ ] Run `dotnet run --project tests/IdleSignal.Tests -c Release` and confirm missing behavior fails.
- [ ] Implement 40 ms Hann Goertzel bank and adjacent noise comparison; acquire 160 ms, release 80 ms, configurable thresholds.
- [ ] Run all tests, inspect anonymous waveform metrics, refine evidence-based defaults and commit.

### Task 2: Audio gate

**Files:** `src/IdleSignal.Core/AudioGate.cs`, `tests/IdleSignal.Tests/GateTests.cs`.
**Interfaces:** `AudioGate.Process(Span<float>)`, `SetEnabled(bool)`, `Reset()`, `Snapshot`; consumes detector settings and actual sample rate.

- [ ] Tests first: stable tone output is exactly zero; bypass preserves samples; tone disappearance opens audio; disabled/re-enabled streams reset; chunk boundaries produce identical output; NaN does not latch mute.
- [ ] Observe failures before implementation.
- [ ] Implement pre-mute detection and 5 ms fade into silence; configuration is applied at buffer boundaries.
- [ ] Run full suite and commit.

### Task 3: SDR# adapter and panel

**Files:** `src/SDRSharp.IdleSignalCanceller/*.cs`, `tests/IdleSignal.IntegrationTests/*`, `tools/extract_bundle.py`.
**Interfaces:** `IdleSignalPlugin : ISharpPlugin`, `IdleAudioProcessor : IRealProcessor`; register actual verified mono audio hook, expose panel and unregister on Close.

- [ ] Characterize actual host hook layout and lifecycle from local DLL metadata/IL.
- [ ] Write integration tests against real SDR# interfaces; verify processing, reset on selection/stream changes, GUI startup disabled, UI disable unmutes, repeated close is safe.
- [ ] Run failing tests, implement adapter and panel with bounded settings, then run full suite and host integration tests.
- [ ] Build Release against actual host DLLs and commit.

### Task 4: Evaluation, packaging and CI

**Files:** `tools/build.ps1`, `tools/evaluate_audio.py` or `tools/IdleSignal.Evaluate/*`, `.github/workflows/build.yml`, `README_JA.md`, `docs/validation.md`.
**Interfaces:** build accepts an external SDR# folder/executable; evaluation accepts explicit WAV paths but writes no source identifiers; package contains plugin, core, documentation only.

- [ ] Evaluate provided WAV with actual detector and verify every fully muted output window is zero; record anonymous metrics.
- [ ] Exercise synthetic tone→speech→tone through the actual adapter; inspect panel rendering and host loading in a workspace-owned instance if possible.
- [ ] Add Windows CI that obtains host package, extracts DLLs, builds, tests and uploads only our package.
- [ ] Run build/packaging locally, inspect ZIP file list, check all tracked content and history for personal paths/identifiers and captured data.
- [ ] Request one independent whole-branch review, fix important findings with regression tests, rerun checks and commit.
- [ ] Record exact verified behavior and limitations; deliver local artifacts without push.
