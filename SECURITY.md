# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| latest release (see [CHANGELOG](./CHANGELOG.md)) | ✅ |
| older releases | ❌ — upgrade to the latest |

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Instead:

1. Use GitHub's **private vulnerability reporting** on this repository
   (*Security ▸ Report a vulnerability*), or
2. contact the maintainer directly via the profile at
   <https://github.com/emindeniz99>.

You can expect an acknowledgement within a few days. Confirmed issues are fixed
in the next patch release and credited in the CHANGELOG (unless you prefer
otherwise).

## Scope notes (what this package can and can't do)

- The shipped package has **zero third-party dependencies** — no DLLs, no
  vendored libraries; only Unity APIs, hand-written C#/Objective-C++/Java, and
  OS shortcut APIs. Supply-chain surface is the repo itself.
- The known, documented trust boundary: **a shortcut tap is not an
  authenticated action** (the Android trampoline is an exported activity; any
  app on the device can synthesize a tap id). See README ▸ "Security". Don't
  wire shortcut ids to destructive or privileged operations.
- Dev-tooling dependencies never ship to consumers. The NuGet test packages and
  GitHub Actions are updated via Dependabot (see `.github/dependabot.yml`) with
  a release-age cooldown as malicious-release mitigation; Pillow (marketing
  images only) is provisioned by the devcontainer and updated manually.
