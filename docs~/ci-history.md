# CI and device-smoke history

*Maintainer document, not shipped with the package. Written 2026-10-02.* The
narration that used to sit in comments in
[`.github/workflows/unity-ci.yml`](../.github/workflows/unity-ci.yml) and
[`tools~/device-smoke/android_device_smoke.sh`](../tools~/device-smoke/android_device_smoke.sh)
lives here, so those files keep a short *why* and a pointer to this one. The
text under each heading is the comment as it was: the comment markers are
removed and the lines re-wrapped, and the two phrases in square brackets are
changed because they pointed at code that is no longer directly below them.
Run numbers, dates and figures are the comments' own; none of them was
re-checked when it moved.

## 1. Why every Unity leg runs on every push

Source: the header of `unity-ci.yml`.

### The cost argument, and the number not to quote

The heavy legs used to be dispatch/cron-only, on a "split by cost" rationale
that measurement disproved: run 25 ran the whole matrix — 17 jobs, macOS
included — in 13.4 minutes of wall clock, and GitHub billed 0 ms for all of it,
because runner minutes on a public repo are free. What that gating actually
bought was a manual step between writing a change and learning whether it works
— which is how a test that passes headlessly and fails in a real Editor reached
main.

That 13.4 minutes was measured BEFORE the licence cap [max-parallel, in
unity-ci.yml]. Chained, run 31 took 29 minutes without the shrink leg, and the
2026-08-31 cron (run 35) took 38 for the whole matrix, shrink leg included. The
argument is unchanged — free minutes, no manual step — but the number is, so do
not quote 13 minutes.

### The cron's first catch

(Its first real catch, run 37 on 2026-09-01: 6000.6.0f1's editor aborts in a
container with Docker's 64 MB /dev/shm — see the shared-memory step in each
Unity job.)

## 2. Why the `needs:` chain was removed

Source: the header of `unity-ci.yml`, which follows its paragraph on
`max-parallel` (the "it" of the first sentence). The recipe at the end is the
way back.

Until 2026-09-17 a SECOND mechanism sat on top of it: the seven Unity jobs were
chained with `needs:` (tests -> android-build -> ios-export -> ios-export-coex
-> tests-unity6-latest -> android-shrink-verify -> gate-off) so that exactly ONE
of them was ever eligible and the whole workflow ran essentially in series. That
chain is gone. Each of those jobs now waits only on the licence gate, except
gate-off, which waits on its REAL producers — it downloads
quickactions-demo-apk-2022.3 from android-build and
ios-simulator-xcodeproj-2022.3 from ios-export, so pointing it at the gate alone
would race them and fail on a missing artifact.

Why it went, measured rather than guessed: the chain cost 29 minutes on run 31
(before the shrink leg joined it) and 38 on run 35 for the whole matrix, against
13.4 unchained — and runner minutes are free on a public repo, so it was buying
nothing but wall clock. What it was insuring against has never happened: across
30 runs no game-ci step has conclusion "failure", every red is downstream of a
successful activation, and run 25 activated all eleven legs simultaneously with
every one logging "Successfully returned ULF license". GameCI hardcodes one
machine-id into its Linux images, so parallel containers present to Unity as the
same machine, and it states the split in one sentence: "This is not an issue for
free licenses, but for paid licenses, you will need to be mindful of starting
too many parallel jobs as activation will fail"
(https://game.ci/docs/docker/docker-images/#concurrent-builds-on-windows-and-macos
— NOT the FAQ, which an earlier version of this comment cited; the FAQ does not
contain the word "seat"). This repo uses a Personal .ulf with no UNITY_SERIAL,
so the hard "two activations per key" limit that GameCI's own CI hit
(game-ci/unity-builder#516) does not bind these credentials. The chain was a
deliberate margin, not a fix for an observed failure, and the margin cost more
than the risk it covered.

max-parallel stays at 2 for now, deliberately: removing the chain is the one
variable this change moves, so an activation problem — if one ever appears — can
be attributed to it rather than to two changes at once. The ceiling is now
roughly eleven concurrent activations, which run 25 already survived.

What it actually bought, measured on run 103 (the first unchained run): 36m01s
wall clock — INSIDE the chained 29–38 range, not the ~13 the old comment's
unchained figure predicted. That figure was measured before the macOS matrix
grew to ten jobs, and it no longer describes this workflow. The run splits like
this:

  * the Linux half finishes at 16 min, and ten jobs were running at once
    three minutes in — that part is what the un-chain fixed, and it is why
    the Linux side will not become the bottleneck as the macOS side shrinks;
  * the macOS half runs to 35 min and owns the critical path. Its jobs enter
    in waves (t=3, 6, 9, 11, 12, 18, 23) behind GitHub's concurrent-macOS
    cap, and the two longest are the springboard taps: unity6-xcode27 at
    12→35 and unity6 at 12→34.

The single biggest remaining cost was therefore a CANARY that gates nothing: ios
springboard tap (unity6-xcode27), 23 minutes, holding a macOS slot on every PR.
Four xcode-27 canary legs did that between them. Moving them to the weekly cron
was the next change, and this measurement is why; it is done, see
`ios-simulator`'s matrix comment. Every Unity job on run 103 still logged
"Successfully returned ULF license" under full parallelism, which is the one
thing the un-chain could have broken.

To restore the old behaviour: put android-build, ios-export, ios-export-coex,
tests-unity6-latest, android-shrink-verify and gate-off back on
`needs: [license, <the previous job>]` in the order listed above.

## 3. `android-shrink-verify`: how the job got its shape

Source: the comment above the job in `unity-ci.yml`. The method and the "what a
green run does NOT prove" paragraph stay there; this is the part that narrates
the choice of one Unity line, the Gradle task and the first run.

One line only (2022.3 — the line whose Android bake was proven on a real APK):
it answers a question about AGP, not about a Unity version, so running it per
line would buy nothing. It builds the resource shrinker's own Gradle task rather
than a whole APK — assembleRelease drags in the release IL2CPP native build,
which overran a two-hour ceiling twice (runs 30 and 31) and produces nothing
this experiment reads. Every step fails LOUDLY and prints what it could not
parse instead of skipping. The job's first run (2026-08-21) validated its own
design the hard way, exactly where the header had warned: export, chown,
probe-planting and the minify flip all worked, and the build then died because
the export ships NO gradlew and the runner's system Gradle (9.x) cannot load the
AGP 7.1.2 the export pins (org.gradle.util.GUtil is gone in Gradle 9). The job
now supplies JDK 17 and NDK r23b, downloads the Gradle the export's own wrapper
properties name (checksum-verified against Gradle's published one) and re-points
the docker-image paths the export hardcodes, instead of trusting the runner
image's drift — the "JDK 11, Gradle 7.2" an earlier version of this comment
promised was a guess about Unity 2022.3's bundle that run 25 disproved (see the
JDK step [in the android-shrink-verify job]).

## 4. Android long-press capture: where the drawer swipe starts

Source: step 2 of the capture in `tools~/device-smoke/android_device_smoke.sh`.
The pixel ranges are the comment's own, from the hierarchy dumps of the API 35
and API 30 images it names; nothing here says they hold for another launcher or
device.

Start on the WORKSPACE first, not the bottom edge. The first four attempts began
at 90% of the height, and the dumps of both images put that point on a search
box: the Pixel launcher's hotseat search bar on API 35 (y 535–598 of 640) and
the collapsed all-apps search box on API 30 (574–630). A drag that begins on a
search widget is the widget's to keep, and API 30 opened its drawer on one run
in five while API 35 never moved. 65% is above the hotseat and the page
indicator on both dumps (API 35: 441–465 / 465+; API 30: 475–499 / 499+) and
below the smartspace card at the top. The 65% start opened the API 35 drawer on
its first run (PR #19 run 64) and the API 30 one not at all, while the 90% start
had opened API 30's once — the drag that starts on the collapsed all-apps box is
the one that image answers. So both, in that order: the workspace first, the
bottom edge second, each followed by a fresh search.
