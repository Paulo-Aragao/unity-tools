using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace PauloAragao.Tools
{
    /// <summary>
    /// Lists packages installed from a Git URL, compares the installed version against the
    /// newest tag on GitHub, and reinstalls through Client.Add so the resolution is not served
    /// from the stale entry in packages-lock.json.
    /// </summary>
    public class PackageUpdaterWindow : EditorWindow
    {
        // ── Layout ──────────────────────────────────────────────
        private const float ROW_HEIGHT = 46f;
        private const float BUTTON_WIDTH = 92f;

        // ── State ───────────────────────────────────────────────
        private readonly List<Entry> entries = new List<Entry>();
        private Vector2 scroll;
        private string globalStatus = "";
        private bool busy;

        private ListRequest listRequest;
        private AddRequest addRequest;
        private Entry addTarget;
        private readonly Queue<Entry> updateQueue = new Queue<Entry>();

        private class Entry
        {
            public string Name;
            public string DisplayName;
            public string RepoUrl;        // https://github.com/Owner/Repo.git
            public string Owner;
            public string Repo;
            public string Revision;       // tag or branch requested in the manifest
            public string InstalledVersion;
            public string LatestTag;
            public string Status = "";
            public bool Checking;
            public bool Supported = true; // false for git hosts other than GitHub

            public bool PinnedToTag => LooksLikeTag(Revision);

            public bool UpdateAvailable
            {
                get
                {
                    if (!Supported || string.IsNullOrEmpty(LatestTag)) return false;

                    // When the manifest pins a tag, compare tag against tag. package.json is not
                    // reliable for this: a tag can be published pointing at a commit whose manifest
                    // was never bumped, which would otherwise hide or fake an available update.
                    if (PinnedToTag)
                        return CompareVersions(StripV(LatestTag), StripV(Revision)) > 0;

                    // Tracking a branch, or no revision at all: fall back to the installed version.
                    return CompareVersions(StripV(LatestTag), InstalledVersion) > 0;
                }
            }

            /// <summary>True when the published tag and the package.json inside it disagree.</summary>
            public bool VersionMismatch =>
                PinnedToTag &&
                !string.IsNullOrEmpty(InstalledVersion) &&
                CompareVersions(StripV(Revision), InstalledVersion) != 0;
        }

        // ────────────────────────────────────────────────────────

        [MenuItem("Tools/Package Updater")]
        public static void ShowWindow()
        {
            var win = GetWindow<PackageUpdaterWindow>("Package Updater");
            win.minSize = new Vector2(520, 260);
        }

        private void OnEnable() => RefreshInstalled();

        // ── Installed packages ──────────────────────────────────

        private void RefreshInstalled()
        {
            if (busy) return;

            busy = true;
            globalStatus = "Reading installed packages...";
            entries.Clear();
            listRequest = Client.List(true, false);
            EditorApplication.update += PollList;
        }

        private void PollList()
        {
            if (listRequest == null || !listRequest.IsCompleted) return;

            EditorApplication.update -= PollList;
            busy = false;

            if (listRequest.Status != StatusCode.Success)
            {
                globalStatus = listRequest.Error != null
                    ? $"Failed to list packages: {listRequest.Error.message}"
                    : "Failed to list packages.";
                listRequest = null;
                Repaint();
                return;
            }

            foreach (var pkg in listRequest.Result.Where(p => p.source == PackageSource.Git))
            {
                var url = ExtractUrl(pkg.packageId);
                var supported = TryParseGitHub(url, out var owner, out var repo);

                entries.Add(new Entry
                {
                    Name = pkg.name,
                    DisplayName = string.IsNullOrEmpty(pkg.displayName) ? pkg.name : pkg.displayName,
                    RepoUrl = url,
                    Owner = owner,
                    Repo = repo,
                    Revision = pkg.git != null ? pkg.git.revision : null,
                    InstalledVersion = pkg.version,
                    Supported = supported,
                    Status = supported ? "" : "Not a GitHub URL. Update this one manually."
                });
            }

            globalStatus = entries.Count == 0
                ? "No packages installed from a GitHub URL."
                : $"{entries.Count} git package(s) found. Press Check for Updates.";

            listRequest = null;
            Repaint();
        }

        // ── GitHub tag lookup ───────────────────────────────────

        private void CheckAll()
        {
            foreach (var entry in entries.Where(e => e.Supported))
                CheckLatestTag(entry);
        }

        private void CheckLatestTag(Entry entry)
        {
            entry.Checking = true;
            entry.Status = "Checking...";
            Repaint();

            var url = $"https://api.github.com/repos/{entry.Owner}/{entry.Repo}/tags?per_page=100";
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "UnityPackageUpdater");
            request.SetRequestHeader("Accept", "application/vnd.github+json");

            request.SendWebRequest().completed += _ =>
            {
                entry.Checking = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    entry.Status = request.responseCode == 404
                        ? $"Repository {entry.Owner}/{entry.Repo} not found. If it is private, update this one manually."
                        : $"GitHub request failed: {request.error}";
                    request.Dispose();
                    Repaint();
                    return;
                }

                var tags = ParseTagNames(request.downloadHandler.text);
                request.Dispose();

                if (tags.Count == 0)
                {
                    entry.Status = "Repository has no tags. Publish one to enable updates.";
                    Repaint();
                    return;
                }

                tags.Sort((a, b) => CompareVersions(StripV(b), StripV(a)));
                entry.LatestTag = tags[0];
                entry.Status = entry.UpdateAvailable ? "" : "Up to date.";
                Repaint();
            };
        }

        private static List<string> ParseTagNames(string json)
        {
            // JsonUtility cannot deserialize a top level array, so the response is wrapped first.
            var wrapper = JsonUtility.FromJson<TagList>("{\"items\":" + json + "}");
            if (wrapper == null || wrapper.items == null) return new List<string>();

            return wrapper.items
                .Select(t => t.name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
        }

        [Serializable] private class TagList { public Tag[] items; }
        [Serializable] private class Tag { public string name; }

        // ── Install ─────────────────────────────────────────────

        private void QueueUpdate(Entry entry, string tag)
        {
            entry.Status = "Queued...";
            entry.LatestTag = tag;
            updateQueue.Enqueue(entry);
            DequeueNext();
        }

        private void DequeueNext()
        {
            if (busy || updateQueue.Count == 0) return;

            addTarget = updateQueue.Dequeue();
            busy = true;

            var identifier = $"{addTarget.RepoUrl}#{addTarget.LatestTag}";
            globalStatus = $"Installing {addTarget.Name} {addTarget.LatestTag}...";
            addTarget.Status = "Installing...";

            addRequest = Client.Add(identifier);
            EditorApplication.update += PollAdd;
            Repaint();
        }

        private void PollAdd()
        {
            if (addRequest == null || !addRequest.IsCompleted) return;

            EditorApplication.update -= PollAdd;
            busy = false;

            if (addRequest.Status == StatusCode.Success)
            {
                globalStatus = $"Installed {addRequest.Result.name} {addRequest.Result.version}.";
                if (addTarget != null)
                {
                    addTarget.InstalledVersion = addRequest.Result.version;
                    addTarget.Revision = addTarget.LatestTag;
                    addTarget.Status = "Up to date.";
                }
            }
            else
            {
                var message = addRequest.Error != null ? addRequest.Error.message : "unknown error";
                globalStatus = $"Install failed: {message}";
                if (addTarget != null) addTarget.Status = $"Failed: {message}";
            }

            addRequest = null;
            addTarget = null;
            Repaint();

            DequeueNext();
        }

        // ── GUI ─────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();

            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox(globalStatus, MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var entry in entries)
                DrawEntry(entry);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(globalStatus, EditorStyles.miniLabel);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    RefreshInstalled();

                if (GUILayout.Button("Check for Updates", EditorStyles.toolbarButton, GUILayout.Width(130)))
                    CheckAll();

                var outdated = entries.Where(e => e.UpdateAvailable).ToList();
                using (new EditorGUI.DisabledScope(outdated.Count == 0))
                {
                    var label = outdated.Count > 0 ? $"Update All ({outdated.Count})" : "Update All";
                    if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(110)))
                        foreach (var entry in outdated)
                            QueueUpdate(entry, entry.LatestTag);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntry(Entry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(ROW_HEIGHT));
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(entry.DisplayName, EditorStyles.boldLabel);

            var installed = $"{entry.Name}  |  installed {entry.InstalledVersion}";
            if (!string.IsNullOrEmpty(entry.Revision)) installed += $"  ({entry.Revision})";
            EditorGUILayout.LabelField(installed, EditorStyles.miniLabel);

            if (entry.UpdateAvailable)
                EditorGUILayout.LabelField($"Latest: {entry.LatestTag}", EditorStyles.miniBoldLabel);
            else if (!string.IsNullOrEmpty(entry.Status))
                EditorGUILayout.LabelField(entry.Status, EditorStyles.miniLabel);

            if (entry.VersionMismatch)
            {
                EditorGUILayout.LabelField(
                    $"Tag {entry.Revision} ships package.json {entry.InstalledVersion}. Bump the manifest before tagging.",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            using (new EditorGUI.DisabledScope(busy || entry.Checking || !entry.Supported))
            {
                if (entry.UpdateAvailable)
                {
                    if (GUILayout.Button($"Update to {entry.LatestTag}", GUILayout.Width(140), GUILayout.Height(24)))
                        QueueUpdate(entry, entry.LatestTag);
                }

                if (!string.IsNullOrEmpty(entry.LatestTag) && !entry.UpdateAvailable)
                {
                    if (GUILayout.Button("Reinstall", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(24)))
                        QueueUpdate(entry, entry.LatestTag);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ── Helpers ─────────────────────────────────────────────

        /// <summary>packageId is "name@url"; the name may itself contain no '@', so split on the first one.</summary>
        private static string ExtractUrl(string packageId)
        {
            var at = packageId.IndexOf('@');
            return at < 0 ? packageId : packageId.Substring(at + 1);
        }

        private static bool TryParseGitHub(string url, out string owner, out string repo)
        {
            owner = repo = null;

            var withoutRevision = url.Split('#')[0];
            var match = Regex.Match(withoutRevision, @"github\.com[:/]([^/]+)/([^/]+?)(?:\.git)?/?$");
            if (!match.Success) return false;

            owner = match.Groups[1].Value;
            repo = match.Groups[2].Value;
            return true;
        }

        /// <summary>Distinguishes a version tag ("v1.2.0", "1.2.0") from a branch name ("main").</summary>
        private static bool LooksLikeTag(string revision)
        {
            return !string.IsNullOrEmpty(revision) && Regex.IsMatch(revision, @"^v?\d+\.\d+");
        }

        private static string StripV(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "0";
            return tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag.Substring(1) : tag;
        }

        /// <summary>Numeric semver compare. Non numeric suffixes (pre-release) are ignored.</summary>
        private static int CompareVersions(string a, string b)
        {
            var left = ParseParts(a);
            var right = ParseParts(b);

            for (int i = 0; i < 3; i++)
            {
                if (left[i] != right[i]) return left[i].CompareTo(right[i]);
            }
            return 0;
        }

        private static int[] ParseParts(string version)
        {
            var parts = new int[3];
            if (string.IsNullOrEmpty(version)) return parts;

            var split = version.Split('-')[0].Split('.');
            for (int i = 0; i < 3 && i < split.Length; i++)
                int.TryParse(split[i], out parts[i]);

            return parts;
        }
    }
}
