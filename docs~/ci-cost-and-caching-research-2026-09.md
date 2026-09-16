# CI cost and caching — research record (September 2026)

*Maintainer document, not shipped with the package. Written 2026-09-15 from
the `unity` workflow's own run data (runs 73, 75, 77, 78) and primary sources
fetched the same day; every claim carries a confidence label. Nothing below is
implemented yet — this is the record behind the decision, the way
[`ios-toolchain-and-ui-test-research-2026-09.md`](./ios-toolchain-and-ui-test-research-2026-09.md)
is for the iOS legs. Labels: **[verified]** read from the cited source or log;
**[plausible]** inferred from verified facts; **[unverified]** not checked.*

## 1. The question and the short answer

The owner asked whether every push really needs a 40–50-minute full run, and
whether caching could shorten it.

**The Library cache is already at its floor.** In run 73 all 15 Unity
container jobs hit their *primary* cache key (`Cache hit occurred on the
primary key Library-<leg>-<kind>-…, not saving cache` in every log; 5.47 GB
restored, 169 MB–1,358 MB per leg), and what an editor still does after a
hit is a package-script recompile of 2–15 s. GameCI's entire published speed
advice is that one cache, and this repo keys it more strictly than GameCI's
template and than every surveyed project. **[verified]**

What the minutes actually are: the `needs:` chain plus `max-parallel: 2`
(self-imposed, beyond anything GameCI or Unity requires for a Personal
licence), a per-job GameCI editor-image pull of 68–146 s (26 of 108
job-minutes) and activation launch of 24–45 s (both structural on fresh
runners), and — since the macOS matrix grew to ten jobs — the iOS simulator's
first boot (0.65–7.9 min on the same image) behind GitHub's cap of five
concurrent macOS jobs. None of that is a cache. **[verified]**

## 2. Decisions

| decision | status | why |
|---|---|---|
| Keep the Library cache exactly as it is (per leg, per kind, keyed on `Packages/**` + `ProjectSettings/**`, restore ladders) | keep | 100 % primary-key hits; a rolling key would write 5.47 GB per generation into a 10 GB budget; sharing one entry across kinds costs each small leg 15–18 s of restore to save an 8–15 s import |
| Do not cache the GameCI editor image, DerivedData, ccache / Xcode 26 compilation cache, or AVD snapshots; do not switch the tests legs to the `base` image | no | each ≤ 1 min or net negative; §7 |
| (0) Export the simulator app as **ARM64** (`PlayerSettings.iOS.simulatorSdkArchitecture`) in Testbed2022 and Testbed6 | to do first | the exports are x86_64-only today because only the *device* architecture is set; removes Rosetta from install and first launch and is a prerequisite for measuring anything else on macOS; §6.3 |
| (1) Un-chain the Unity jobs, keep `gate-off`'s two artifact `needs`, keep `UNITY_MAX_PARALLEL=2` for one cycle, add a free Docker Hub login | to do | the chain *is* the wall clock: run 75 (main) 47.65 → ~22 min; PR shape 47–53 → ~31–38 min; 0 job-minutes; §5, §8 |
| (2) Move the four Xcode 27 canaries to the weekly cron / `workflow_dispatch` | to do | 22 of run 78's 91.5 macOS job-minutes, four of the ten slots-worth of jobs, all red today and `continue-on-error`; no PR coverage lost |
| (3) Fold `ios-springboard` into `ios-simulator` per export, and start the simulator boot at the top of the job with a documented `background: true` step | to do, one measured run each | ~21 macOS job-minutes and 3 jobs off the 5-slot pool; pre-boot hides 1.3–6.6 min per green leg; §8 |
| (4) Skip XCUITest's 2 × 60 s SpringBoard idle waits | no | only a private XCTest API reaches it; §9 |
| (5) Gradle user-home cache inside the GameCI container | defer | 0 wall-clock once (1) is done; ~0.5–1.5 job-minutes; §8 |
| Save this record | done | this file |

## 3. Method

Four research sweeps (GameCI caching, macOS, Android caching, workflow
structure) over the run-73 logs and the docs, then a verification workflow of
ten agents on 2026-09-15: four re-read the primary sources (GameCI, GitHub,
25 other projects' real workflow files, Unity), five tried to refute each
recommendation against runs 73/75/77/78, one looked for what the research
never considered. Corrections from that pass are folded in below; the
original recommendations over-counted in three places (pre-boot savings,
the fold's wall-clock gain, the Gradle cache) and missed the simulator
architecture entirely.

## 4. Measured baselines **[verified]**

| run | shape | wall | note |
|---|---|---|---|
| 73 (2026-09-11) | PR, 28 jobs, 4 macOS | 39.8 min / 108 job-min | chain links 5.7 + 10.6 + 5.3 + 2.8 + 3.5 + 4.5 + 7.0 = 39.4 = the wall clock; every Library cache a primary hit; image pulls 26.3 job-min |
| 75 (2026-09-14) | scheduled, main, 24 jobs | 47.65 min | chain = wall clock again; simulator boot 0.65 min |
| 77 (2026-09-15) | PR, 30 jobs, 10 macOS | 47.3 min | critical path `ios springboard tap (unity6)` 24.8 min (xcodebuild 6.4, boot + install 8.8, XCUITest 8.4); macOS concurrency peaked at 5; four macOS jobs queued 4.3–8.7 min |
| 78 (2026-09-15) | PR, 34 jobs | 52.65 min | Linux chain to the coex export (28.2) then `ios simulator coex (unity6)` queued 8.1 min behind the cap and 16.3 min long; the four canaries burned 22.2 of 91.5 macOS job-min |

Other measured facts: the GameCI editor image pull is 68–146 s per job
(unity6 android 146 s; the Docker Hub tags in use are 4.3–8.5 GB compressed,
~58 GB across ten tags); GameCI's activation launch in `/BlankProject` is
24–45 s cold per container, ~12 s for a second builder step in the same job;
the simulator's first boot on `macos-26-arm64` image 20260907.0351 took 0.65,
4.3 and 7.9 min in runs 75, 73 and 77 (the `com.apple.locationd` migrator
decides it); XCUITest's own "wait for SpringBoard to idle" costs 2 × 60 s per
springboard leg (120 of a 195 s test; the context menu's blur never lets
SpringBoard go idle).

## 5. What GameCI recommends **[verified]**

- The **only** speed advice GameCI publishes is the Library cache:
  "In order to make builds run faster, you can cache Library files from
  previous builds … This simple addition could speed up your build by more
  than 50%." (builder page; the test-runner page repeats it and adds that it
  "only applies to testing Unity Projects, not Unity Packages" — this repo
  tests through testbed *projects*, so it applies). Nothing about Library
  sub-folders, Bee, image pulls or activation time.
  <https://game.ci/docs/github/builder>, <https://game.ci/docs/github/test-runner>
- Licence concurrency: "Windows and MacOS will each consume an additional
  license seat … This is not an issue for free licenses, but for paid
  licenses, you will need to be mindful of starting too many parallel jobs".
  <https://game.ci/docs/docker/docker-images>
- `skipActivation` "should only be used for Mac self-hosted runners"; the
  test-runner has no such input. Activation stays a per-container cost.
- Own/mirrored images via `containerRegistryRepository` — no speed claim
  anywhere; no slim variant exists (`base` is 0.4–0.47 GB below
  `linux-il2cpp`). <https://game.ci/docs/docker/customize-docker-images>
- Releases: the pinned `d829bfc` is byte-identical to v5.0.0; v5.0.1 adds
  `dockerShmSize` (this repo's own daemon.json shm step could go); v6.0.0 is
  "a thin wrapper around game-ci/cli" with unchanged inputs — nothing changes
  for hosted Docker caching. <https://github.com/game-ci/unity-builder/releases/tag/v6.0.0>
- The only guidance on several builds per job is a disk warning about
  *different* images in one job (gate-off already does that, 12.5 GB compressed).
  <https://game.ci/docs/troubleshooting/common-issues>

## 6. Platform facts the decision rests on

### 6.1 GitHub **[verified]**

- Cache: 10 GB per repository by default (raisable by the owner, USD
  0.07 / GB-month), entries unused for 7 days evicted, LRU when over the cap;
  PR-created caches live on `refs/pull/N/merge`, PRs read the base branch's.
  <https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching>
- Concurrency, Free plan on standard hosted runners: 20 jobs total, **5 macOS**,
  shared with larger runners; larger macOS runners are billed even on public
  repositories (USD 0.102/min for the M2 Pro) and do not enlarge that cap.
  <https://docs.github.com/en/actions/reference/limits>,
  <https://docs.github.com/en/billing/reference/actions-runner-pricing>
- Runners: macOS arm64 standard = 3 (M1) CPU / 7 GB; Ubuntu 4 CPU / 16 GB;
  every job a fresh VM. The `xcode-27` label runs on macOS 27.0 beta since
  2026-09-10. <https://docs.github.com/en/actions/reference/runners/github-hosted-runners>,
  <https://github.blog/changelog/2026-09-10-xcode-27-runner-image-now-runs-on-macos-27/>
- `pull_request` `paths` filters are evaluated on the three-dot diff of the
  whole PR — which is why a docs-only push restarts every Unity job.
- No documented mechanism makes pulling a third-party image faster than
  Docker Hub; Docker's `gha` backend is a buildx layer exporter on the same
  10 GB cache. <https://docs.docker.com/build/cache/backends/gha/>
- Background steps are now first-class: `background: true` with `wait`,
  `wait-all`, `cancel`; "an implicit wait-all runs before any post-job
  cleanup" — a red job would wait for a still-booting simulator.
  <https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idstepsbackground>
- Docker Hub pull limits (100 / 6 h per IPv4 unauthenticated) are **not**
  documented as exempt for hosted runners **[plausible]**; a free-account
  `docker/login-action` is cheap insurance once jobs run in parallel.
  <https://docs.docker.com/docker-hub/usage/pulls/>

### 6.2 How other projects do it **[verified, from their workflow files]**

- Ten GameCI users (game-ci/unity-actions-example, GameCI's own test-runner
  and builder CI, Mirror, Mirage, NuGetForUnity, UIEffect, open-brush,
  VContainer, MessagePack-CSharp): every one pulls `unityci/editor` per job;
  eight cache only `Library`, with keys coarser than this repo's (constant
  platform/version strings — none hashes `Packages/**` + `ProjectSettings/**`);
  nobody caches the editor image. Two ideas this repo does not use: a
  restore/save split that saves only on `main` (open-brush) and a rolling
  per-commit key with restore-keys so Library is re-saved every run
  (UIEffect) — the one precedent that addresses the frozen-blob drift.
- **Nobody chains Unity jobs or caps parallelism for licence reasons**:
  GameCI's test-runner CI runs 93 Unity jobs with no `needs` and no
  `max-parallel`; unity-builder's CI ~36 legs in one matrix on a Personal
  `.ulf`; UIEffect `max-parallel: 8`, VContainer 9, open-brush 9–18, Mirror 6.
  <https://raw.githubusercontent.com/game-ci/unity-test-runner/main/.github/workflows/main.yml>
- iOS: serve-sim starts the boot with `nohup … &` *before* the build and
  waits after it (the pre-boot pattern; no figure reported); simulator-action
  does shutdown → erase → boot → `bootstatus` with a bounded wait and one
  recreate-and-retry; fastlane snapshot waits with `bootstatus -b`;
  flutter/packages and Maestro create a fresh device per run. Stuck first boots
  ("Waiting on System App" past 180 s) are reported on the very image release
  run 73 used. Nobody caches simulator state.
  <https://raw.githubusercontent.com/EvanBacon/serve-sim/main/.github/actions/build-serve-sim-for-ci/action.yml>,
  <https://raw.githubusercontent.com/futureware-tech/simulator-action/main/src/main.ts>,
  <https://github.com/buster14a/buster/issues/636>
- Android: nowinandroid, leakcanary and coil do not cache AVDs at all;
  ReactiveCircus's own CI and okhttp use the README snapshot recipe; nobody
  caches Gradle *inside* a GameCI container — that idea is this repo's own.

### 6.3 Unity-side bounds **[verified]**

- `Library/Bee` holds the incremental build state and is already cached and
  hitting (run 73 unity6 android: "1 items updated, 1080 evaluated"). Unity's
  machine-level `BEE_CACHE_DIRECTORY` reuses only non-embedded packages and
  libIL2CPP artefacts across projects and lands under the container's `/root`
  mount; the Accelerator caches asset imports only. One Library can hold
  several targets' imports side by side, but the kinds differ in Bee state.
  <https://docs.unity3d.com/6000.1/Documentation/Manual/incremental-build-pipeline.html>
- **The simulator export is x86_64-only by omission.** `TestbedBuilder.cs`
  calls `PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 2)` — the
  *device* architecture — and never `PlayerSettings.iOS.simulatorSdkArchitecture`
  (`AppleMobileArchitectureSimulator`: X86_64 = 0 is the serialized default,
  `iOSSimulatorArchitecture: 0` in both ProjectSettings). Every export
  therefore compiles `ARCHS=x86_64`, runs under Rosetta on the arm64 runners,
  and is refused outright by the iOS 27 simulator (the canary finding). ARM64
  simulator export exists from 6000.0.9f1 and was backported in 2022.3.54f1;
  Unity marks X86_64/Universal obsolete in `UnityCsReference` master. Set
  ARM64 (not Universal — `ONLY_ACTIVE_ARCH=NO` would compile both slices).
  <https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AppleMobileArchitectureSimulator.html>,
  <https://unity.com/releases/editor/whats-new/2022.3.54f1>
- The IL2CPP C++ configuration on iOS follows the `xcodebuild -configuration`
  (`Release` → `-O3`, the 4.5–5-min GameAssembly script phase); a Debug or
  `OptimizeSize` simulator build is a stated coverage change, not a cache.
- Licence: "Unity Personal users have an unlimited number of activations"
  (Unity support); the pinned entrypoint randomizes the machine-id for
  Personal serials and `activate.sh` retries five times; run 25 already
  started 11 container jobs within two seconds. The one thing not documented
  away is a compliance sentence on Unity's terms page — "Running Unity on more
  than one machine at the same time is not allowed. A separate license is
  required for build machines." — which applies to Personal and which the
  workflow already meets at two concurrent containers; un-chaining changes
  the count (11 with `max-parallel: 2`), not the kind. **That is the owner's
  call, made knowingly.** A paid seat would bring the two-activations rule and
  the chain back. <https://support.unity.com/hc/en-us/articles/360021433091-Can-I-use-my-licence-with-different-operating-systems>,
  <https://unity.com/pages/license-compliance>
- A Unity licence server is Enterprise-only; self-hosted runners "should
  almost never be used for public repositories" (GitHub). Both rejected.

## 7. What not to do, and why **[verified unless marked]**

| idea | ceiling | verdict |
|---|---|---|
| Re-key / roll the Library cache so it refreshes | ≤ 0.5 min per Unity leg of refresh + asmdef recompile; 5.47 GB per generation against 10 GB | no |
| `base` image for the tests legs | 0.39–0.47 GB ≈ 5–8 s per leg | no |
| Cache the editor image | 4.3–8.5 GB per tag, ~58 GB in use; no evidence a `docker load` beats a 58–73 MB/s Docker Hub pull on a fresh VM; un-chaining takes the pulls off the critical path anyway | no |
| DerivedData / ccache / Xcode 26 compilation cache | reach only the ≤ 0.9 min Xcode-scheduled slice per unity6 job (0.5 on 2022.3); the 4.5-min il2cpp/bee script phase calls clang by absolute path and keeps its state in `Il2CppTempDirArtifacts/`, outside DerivedData; Apple's task signatures include mtimes, which every artifact download resets | no |
| `Il2CppTempDirArtifacts/` cache | the only thing that could touch the 4.5 min; Tundra signatures default to timestamps **[unverified]**; hundreds of MB per leg | at most one measured experiment |
| AVD snapshot cache | boot is 31–46 s with KVM; the cache covers only `~/.android`; 1–3 GB per AVD against the budget; an open parallel-job hang (ReactiveCircus #362); smoke legs are leaves | no |
| Larger macOS runners | billed on public repos, same 5-job cap | no |
| Drop the 2021.3 iOS export | 2.4 min only while `max-parallel < 3`; loses the only real-editor 2021.3 plist evidence | no |
| Gate all macOS jobs to `main` | loses the only tap → `Performed` evidence on PRs | only the canaries |

## 8. The four changes, with corrected numbers

**(0) ARM64 simulator export** — one line in each of Testbed2022's and
Testbed6's `TestbedBuilder.cs` (`PlayerSettings.iOS.simulatorSdkArchitecture
= AppleMobileArchitectureSimulator.ARM64`). Expected: no Rosetta on install /
first launch (the 1.2–2.2-min first launch versus 0.4 min for a second launch
is the likeliest place it shows **[plausible]**), and the two Unity 6 Xcode 27
canaries plausibly install. Zero cache or licence exposure. Goes first because
every macOS timing below includes the Rosetta cost.

**(1) Un-chain** — delete the six `needs: [license, <previous>]` edges so
`tests`, `android-build`, `ios-export`, `ios-export-coex`,
`tests-unity6-latest` and `android-shrink-verify` sit on `needs: license`;
`gate-off` keeps `needs: [android-build, ios-export]` (it downloads their
artifacts); keep the `!cancelled()` gates; keep `UNITY_MAX_PARALLEL=2` for one
cycle (peak 11 activations, the count run 25 survived), then raise it (worth
~2–3 min more). Replay on measured durations: main shape 47.65 → ~22 min;
PR shape with ten macOS jobs 47–53 → ~31–38 min (hard floor 30.6 = ios-export
end 5.8 + springboard unity6 24.75); ±5 min from boot variance. Job-minutes
unchanged and free. Rewrite the workflow header's licence rationale (it cites
the hardcoded machine-id, which the Personal path randomizes) and the
`UNITY_MAX_PARALLEL` text in the READMEs.

**(2) Canaries to schedule / dispatch** — frees ~22 macOS job-minutes and
four jobs per PR run; they are `continue-on-error` and all red until (0)
lands, so PRs lose nothing.

**(3) Fold springboard into `ios-simulator`, pre-boot** — one download, one
xcodebuild, one boot per export; run the SpringBoard tap first (the app must
be cold), then the alive-check as a relaunch (`--terminate-running-process`).
Removes three macOS jobs and ~21 macOS job-minutes; wall clock ~0 on its own
(the merged unity6 job stays the critical path) — its value is pool relief
(run 77 queued four macOS jobs 4.3–8.7 min). Cost: verdict coupling (a
springboard `FAIL` reddens `ios simulator`), check names change, alive-check
semantics change. Pre-boot: choose the UDID first (a background step's
outputs are only visible after `wait`), `xcrun simctl boot` in a
`background: true` step before `download-artifact`, keep `bootstatus -b`
before `install` with a **bounded** wait and one delete-and-recreate retry
(the stuck-first-boot reports); hideable part is min(boot, build + download):
2.7–6.6 min on macos-26 legs, 1.3–1.5 on macos-15, ≈ 19 macOS job-minutes
ceiling across the six green legs, ≤ ~5 wall-min on run 77's shape until (1)
lands; expect CPU contention on 3 cores (bee runs `--threads=3`). Also cheap
to try: `xcrun simctl shutdown` as a last step — the two unity6 macOS legs'
"Complete job" took 1.2–1.9 min in run 78 versus 0.3–0.4 elsewhere
**[unverified]**.

**(5) Gradle in the container** — `unity-builder` mounts
`$RUNNER_TEMP/_github_home` at `/root` and Unity's Gradle uses the default
user home, so `${{ runner.temp }}/_github_home/.gradle/caches` is cacheable,
keyed by editor image tag, seeded only by a push to `main` or the cron (PR
caches are merge-ref scoped). Never cache the whole `_github_home` — it holds
the activated licence. Worth ~0.5–1.5 job-minutes and 0 wall-clock once (1)
is done. Last, if ever.

## 9. Gaps the research closed on the way

- The XCUITest idle waits (2 × 60 s per springboard leg) are real and
  reachable only through a private XCTest API (WebDriverAgent swizzles
  `XCUIApplicationProcess`'s quiescence wait; Appium exposes it as
  `waitForIdleTimeout` and marks it "not recommended"); public XCTest has no
  knob and `UIView.setAnimationsEnabled` reaches the app, not SpringBoard.
  Not worth the fragility. **[verified]**
- Path-filtered "lite" PR runs, a `workflow_dispatch` leg selector, reusable
  workflows and a merge queue were already costed in the structure sweep: a
  job-level `if` keeps required checks green but a mis-scoped filter is a
  silent green; merge queues need an organisation-owned repo. Later, opt-in.
- The per-macOS-leg "Complete job" time and running the unity6 legs on
  `macos-15` (iOS 26.2 instead of 26.5 — a coverage change) are the two
  **[unverified]** experiments left.

## 10. Caveats

- All run numbers are from single runs; simulator first-boot variance alone
  is ±5 min on the wall clock.
- The GitHub cache API (`/actions/cache/usage`, `/actions/caches`) was
  refused through this session's proxy, so 5.47 GB is the sum of the sizes
  restored in run 73, not the repository's live total.
- A cold Library miss was never observed (every run since run 10 restored
  the same blobs); the cost of a full import plus a cold IL2CPP compile on
  Android is inferred, not measured.
- The pre-boot and fold numbers are replays of measured step durations, not
  runs; both need one measured run before the docs claim anything.
- Unity's terms sentence in §6.3 is a compliance question for the owner, not
  an activation limit; this record does not decide it.
