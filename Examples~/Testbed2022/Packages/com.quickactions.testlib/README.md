# com.quickactions.testlib — an `.androidlib` inside a UPM package

A **testbed fixture**, not part of the shipped package and not an example to
copy wholesale. It exists to answer one question on a real APK, sub-item (d) of
the ROADMAP's "Verify the `.androidlib` icon recipe on a real build":

> is an `.androidlib` shipped *inside a UPM package* picked up by the Android
> build the same way one under `Assets/` is?

The mechanism has never been documented on a Unity manual page. It is
established only by precedent: Unity's own `com.unity.mobile.notifications`
ships `Runtime/Android/Plugins/mobilenotifications.androidlib/` with a single
committed `.meta`, and its notification code cannot work without it. This
package copies that layout exactly — the folder depth, the one `PluginImporter`
`.meta` on the `.androidlib` itself and none inside it — and puts one drawable
in it, `ic_quickaction_frompkg`.

CI's `android-build` job requires that name in the 2022.3 APK's resource table
on every push, alongside `ic_quickaction_search` from the `.androidlib` under
`Assets/`. Two locations, one drawable each, same file shape: a leg where one is
present and the other missing says exactly which delivery path broke.

## Why it is an *embedded* package

It sits under the testbed's `Packages/` folder, so Unity discovers it with no
entry in `Packages/manifest.json` and none in `packages-lock.json` — the lock
file records resolved *registry* and *local (`file:`)* dependencies, and an
embedded package is neither. That is also why adding it does not touch the
testbed's `manifest.json` `dependencies` block.

## What it deliberately does not have

No `build.gradle`, no code, no assembly definition. Unity's notifications
package ships a `build.gradle` because its library compiles Java; this one only
carries a resource, which is the case README.md's icon recipe describes.
