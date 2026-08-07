# unity-tools

Collection of Unity editor development tools by Paulo Aragao.

## Tools

- **Scriptable Object Browser** — editor window for browsing and searching ScriptableObjects by folder, with search, type tags and quick ping/select (`Tools > Scriptable Object Browser`, `Ctrl+Shift+B`).
- **Hierarchy Separator** — colored separator headers for organizing the Hierarchy window (`GameObject > Create Hierarchy Separator`).
- **Transform Reset Button** — inspector buttons to quickly reset position, rotation and scale on Transforms.
- **Package Updater** — lists every package installed from a GitHub URL, checks the repository for newer tags and reinstalls with one click (`Tools > Package Updater`).

## Package Updater

The Unity Package Manager has no Update button for packages installed from a Git URL, and editing
`Packages/manifest.json` by hand often does nothing because `Packages/packages-lock.json` still pins
the previously resolved commit.

This window works around both. It reads the installed git packages, queries the GitHub tags API for
each repository, and installs the newest tag through `Client.Add`, which rewrites the manifest and
the lock file together.

There is no configured package list. Every package in the project whose source is a GitHub URL is
picked up automatically, so packages added later need no change here.

- **Check for Updates** — compares the pinned tag against the newest tag in the repository.
- **Update to vX.Y.Z** — installs that tag.
- **Update All** — installs every pending update in sequence.
- **Reinstall** — re-resolves a package that is already current, for when a tag was moved or the cache went stale.

### Conventions it expects

- Releases are tagged `vX.Y.Z` (a bare `X.Y.Z` also works). Branch pins such as `#main` are listed,
  but the comparison then falls back to the version in `package.json`.
- The tag and the `version` in `package.json` should match. When they drift, the row says so —
  that usually means a tag was pushed before the version bump landed.

Packages from a git host other than GitHub are listed but not checked, since the tag lookup is
GitHub specific. Private repositories return 404 from the unauthenticated API and are reported as
such. Unauthenticated GitHub API calls are limited to 60 per hour, far above normal use here.

## Installation (Unity Package Manager)

`Window > Package Manager > + > Add package from git URL`:

```
https://github.com/Paulo-Aragao/unity-tools.git#v1.1.2
```

Requires Unity 2021.3+.
