# Installing Unity on Linux (reproducible steps)

The web-session container is ephemeral, so Unity does not persist between
sessions. These are the exact steps used to install a Unity 2022.3 LTS Editor
for manual smoke-testing. **Day-to-day verification does not need Unity** — use
[`verify.sh`](./verify.sh) (stub-based, license-free). Unity is only needed to
open a real project and confirm import/compile in-Editor.

## 1. Unity Hub (optional, for managing installs)

```bash
curl -fsSL https://hub.unity3d.com/linux/keys/public | gpg --dearmor \
  | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg >/dev/null
echo 'deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main' \
  | sudo tee /etc/apt/sources.list.d/unityhub.list
sudo apt-get update && sudo apt-get install -y unityhub
```

## 2. Editor (direct download — no Hub/GUI needed)

Resolve a 2022.3 LTS Linux build from Unity's release API, then download and
extract the `.tar.xz` (~3 GB compressed, ~6 GB extracted):

```bash
# Example: 2022.3.9f1 (changeset ea401c316338)
URL="https://download.unity3d.com/download_unity/ea401c316338/LinuxEditorInstaller/Unity-2022.3.9f1.tar.xz"
curl -L -o /tmp/Unity.tar.xz "$URL"
sudo mkdir -p /opt/unity && sudo tar -xf /tmp/Unity.tar.xz -C /opt/unity
/opt/unity/Editor/Unity -version   # binary at /opt/unity/Editor/Unity
```

For another version, query:
`https://services.api.unity.com/unity/editor/release/v1/releases?limit=25&order=RELEASE_DATE_DESC&stream=LTS&platform=LINUX&architecture=X86_64`
(filter for `2022.3`, read `downloads[].url` / `.modules` for the Android build module).

## 3. License (required to build or open a project in batchmode)

Installing the Editor needs no license, but **running** it does:

```
No valid Unity Editor license found. Please activate your license.
```

Activate a free Personal license (needs your Unity account):

```bash
# Interactive/CI activation, then reuse the resulting .ulf:
/opt/unity/Editor/Unity -batchmode -nographics -quit \
  -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -logFile -
```

Or `-createManualActivationFile` → upload at <https://license.unity3d.com/manual>
→ `-manualLicenseFile <file>.ulf`. CI typically uses `UNITY_SERIAL` (Plus/Pro)
or the `.ulf` from a manual activation (Personal).

## 4. Platform build limits

- **Android** builds work on Linux once the *Android Build Support* module
  (SDK/NDK/JDK) is installed and a license is active.
- **iOS** builds **cannot** be produced on Linux — Unity emits an Xcode project
  that must be compiled on macOS with Xcode. The iOS post-processor and `.mm`
  here are exercised only on a macOS Unity. (This is a platform constraint, not
  a package limitation.)

## 5. Licensing lessons (learned the hard way)

- A **manually activated** license (`.ulf` via license.unity3d.com/manual)
  **cannot be returned from the CLI** — `-returnlicense` only works for
  `-username/-password`(+serial) activations. Freeing a manual seat requires a
  **Unity support ticket** (Unity ID ▸ My Account ▸ Support, or
  support.unity.com) quoting the machine identifiers; plan for this *before*
  activating on a throwaway VM/container, because the seat stays bound to that
  machine until support releases it.
- The seat binding follows the machine fingerprint (hostname + machine-id),
  not the disk image — deleting the VM does not free the seat.
- Never commit the `.ulf` (it embeds the serial) and never paste the full
  serial into logs/tickets — mask it and use its SHA1 when support asks for
  proof.
