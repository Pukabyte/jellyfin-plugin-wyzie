#!/usr/bin/env python3
"""Build and package Wyzie Subtitles releases for Jellyfin and Emby.

For each Jellyfin target (10.9 / 10.10 / 10.11) we produce a zip whose
manifest version is suffixed with a numeric ABI marker (109 / 1010 / 1011)
so all three can coexist in manifest.json. Jellyfin filters each entry by
its targetAbi field and picks the one compatible with the running server.

Usage:

    python3 build/package.py --version 1.0.0 --repo owner/repo

Run after `dotnet restore`; the script calls `dotnet publish` itself.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"

JELLYFIN_CSPROJ = ROOT / "src/Jellyfin.Plugin.Wyzie/Jellyfin.Plugin.Wyzie.csproj"
EMBY_CSPROJ = ROOT / "src/Emby.Plugin.Wyzie/Emby.Plugin.Wyzie.csproj"

JELLYFIN_GUID = "b2c9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b70"
JELLYFIN_NAME = "Wyzie Subtitles"
JELLYFIN_DESC = "On-demand subtitle provider backed by sub.wyzie.io."
JELLYFIN_OVERVIEW = (
    "Streams subtitle content directly from Wyzie when playback starts. "
    "Requires a free API key from https://sub.wyzie.io/redeem."
)
JELLYFIN_CATEGORY = "Subtitles"
JELLYFIN_OWNER = "wyzie"

# Jellyfin target → (targetAbi, numeric version suffix, SDK hint).
JELLYFIN_TARGETS = {
    "10.9":  ("10.9.0.0",  "109",  "net8.0"),
    "10.10": ("10.10.0.0", "1010", "net8.0"),
    "10.11": ("10.11.0.0", "1011", "net9.0"),
}


def md5(path: pathlib.Path) -> str:
    h = hashlib.md5()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def run(cmd: list[str]) -> None:
    print(f"$ {' '.join(cmd)}", flush=True)
    subprocess.run(cmd, check=True)


def publish_jellyfin(jf_version: str, out_dir: pathlib.Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    run([
        "dotnet", "publish", str(JELLYFIN_CSPROJ),
        "-c", "Release",
        f"-p:JellyfinVersion={jf_version}",
        "-o", str(out_dir),
        "--nologo",
    ])


def publish_emby(out_dir: pathlib.Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    run([
        "dotnet", "publish", str(EMBY_CSPROJ),
        "-c", "Release",
        "-o", str(out_dir),
        "--nologo",
    ])


def build_jellyfin_zip(
    jf_version: str,
    manifest_version: str,
    changelog: str,
    timestamp: str,
    target_abi: str,
    publish_dir: pathlib.Path,
) -> pathlib.Path:
    meta = {
        "category": JELLYFIN_CATEGORY,
        "guid": JELLYFIN_GUID,
        "name": JELLYFIN_NAME,
        "description": JELLYFIN_DESC,
        "overview": JELLYFIN_OVERVIEW,
        "owner": JELLYFIN_OWNER,
        "targetAbi": target_abi,
        "version": manifest_version,
        "changelog": changelog,
        "timestamp": timestamp,
    }
    zip_path = ARTIFACTS / f"jellyfin-plugin-wyzie_jf{jf_version}_{manifest_version}.zip"
    dll = publish_dir / "Jellyfin.Plugin.Wyzie.dll"
    common = publish_dir / "Wyzie.Common.dll"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("meta.json", json.dumps(meta, indent=2))
        z.write(dll, dll.name)
        z.write(common, common.name)
    return zip_path


def build_emby_zip(plugin_version: str, publish_dir: pathlib.Path) -> pathlib.Path:
    zip_path = ARTIFACTS / f"emby-plugin-wyzie_{plugin_version}.zip"
    dll = publish_dir / "Emby.Plugin.Wyzie.dll"
    common = publish_dir / "Wyzie.Common.dll"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(dll, dll.name)
        z.write(common, common.name)
    return zip_path


def update_manifest(entries: list[dict]) -> None:
    manifest_path = ROOT / "manifest.json"
    if manifest_path.exists():
        try:
            manifest = json.loads(manifest_path.read_text())
        except json.JSONDecodeError:
            manifest = []
    else:
        manifest = []

    if not manifest:
        manifest = [{
            "category": JELLYFIN_CATEGORY,
            "guid": JELLYFIN_GUID,
            "name": JELLYFIN_NAME,
            "description": JELLYFIN_DESC,
            "overview": JELLYFIN_OVERVIEW,
            "owner": JELLYFIN_OWNER,
            "imageUrl": "",
            "versions": [],
        }]

    plugin = manifest[0]
    existing = {v.get("version"): v for v in plugin.get("versions", [])}
    for entry in entries:
        existing[entry["version"]] = entry
    plugin["versions"] = sorted(existing.values(), key=lambda v: v.get("version", ""), reverse=True)
    manifest[0] = plugin

    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True, help="Base plugin version (e.g. 1.0.0)")
    p.add_argument("--repo", default="", help="owner/repo for GitHub release URL")
    p.add_argument("--changelog", default="", help="Release notes blurb")
    p.add_argument(
        "--jellyfin",
        default="10.9,10.10,10.11",
        help="Comma-separated Jellyfin targets to build (default: all three).",
    )
    p.add_argument("--skip-emby", action="store_true")
    args = p.parse_args()

    base_version = args.version.lstrip("v")
    timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    if ARTIFACTS.exists():
        shutil.rmtree(ARTIFACTS)
    ARTIFACTS.mkdir()

    jf_requested = [v.strip() for v in args.jellyfin.split(",") if v.strip()]
    manifest_entries: list[dict] = []

    for jf_version in jf_requested:
        if jf_version not in JELLYFIN_TARGETS:
            print(f"!! unknown Jellyfin target: {jf_version} — skipping", file=sys.stderr)
            continue
        target_abi, suffix, _tfm = JELLYFIN_TARGETS[jf_version]
        manifest_version = f"{base_version}.{suffix}"

        publish_dir = ARTIFACTS / f"publish-jf-{jf_version}"
        publish_jellyfin(jf_version, publish_dir)

        zip_path = build_jellyfin_zip(
            jf_version, manifest_version, args.changelog, timestamp, target_abi, publish_dir,
        )
        checksum = md5(zip_path)
        print(f"  → {zip_path.name}  md5={checksum}  abi={target_abi}")

        if args.repo:
            manifest_entries.append({
                "version": manifest_version,
                "changelog": args.changelog,
                "targetAbi": target_abi,
                "sourceUrl": f"https://github.com/{args.repo}/releases/download/v{base_version}/{zip_path.name}",
                "checksum": checksum,
                "timestamp": timestamp,
            })

    if not args.skip_emby:
        emby_dir = ARTIFACTS / "publish-emby"
        publish_emby(emby_dir)
        emby_zip = build_emby_zip(base_version, emby_dir)
        print(f"  → {emby_zip.name}  md5={md5(emby_zip)}")

    if manifest_entries:
        update_manifest(manifest_entries)
        print(f"manifest.json: {len(manifest_entries)} entry(ies) added")
    else:
        print("manifest.json: skipped (no --repo or no Jellyfin targets)")

    return 0


if __name__ == "__main__":
    sys.exit(main())
