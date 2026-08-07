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

- **Check for Updates** — compares the installed `package.json` version against the newest tag.
- **Update to vX.Y.Z** — installs that tag.
- **Update All** — installs every pending update in sequence.
- **Reinstall** — re-resolves a package that is already current, for when a tag was moved or the cache went stale.

Unauthenticated GitHub API calls are limited to 60 per hour, which is far above normal use here.

## Installation (Unity Package Manager)

`Window > Package Manager > + > Add package from git URL`:

```
https://github.com/Paulo-Aragao/unity-tools.git#v1.1.0
```

Requires Unity 2021.3+.
