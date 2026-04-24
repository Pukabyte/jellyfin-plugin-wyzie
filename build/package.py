#!/usr/bin/env python3
"""Package built plugin DLLs into Jellyfin/Emby release zips and update
manifest.json with the new version entry.

Called from GitHub Actions after `dotnet build -c Release`. Safe to run
locally too:

    python3 build/package.py --version 1.0.0.0 --repo owner/repo
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import pathlib
import shutil
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"

JELLYFIN_GUID = "b2c9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b70"
JELLYFIN_NAME = "Wyzie Subtitles"
JELLYFIN_DESC = "On-demand subtitle provider backed by sub.wyzie.io."
JELLYFIN_OVERVIEW = (
    "Streams subtitle content directly from Wyzie when playback starts. "
    "Requires a free API key from https://sub.wyzie.io/redeem."
)
JELLYFIN_CATEGORY = "Subtitles"
JELLYFIN_OWNER = "wyzie"
TARGET_ABI = "10.9.0.0"

JELLYFIN_BIN = ROOT / "src/Jellyfin.Plugin.Wyzie/bin/Release/net8.0"
EMBY_BIN = ROOT / "src/Emby.Plugin.Wyzie/bin/Release/netstandard2.0"
COMMON_BIN = ROOT / "src/Wyzie.Common/bin/Release/netstandard2.0"


def md5(path: pathlib.Path) -> str:
    h = hashlib.md5()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def build_jellyfin_zip(version: str, changelog: str, timestamp: str) -> pathlib.Path:
    meta = {
        "category": JELLYFIN_CATEGORY,
        "guid": JELLYFIN_GUID,
        "name": JELLYFIN_NAME,
        "description": JELLYFIN_DESC,
        "overview": JELLYFIN_OVERVIEW,
        "owner": JELLYFIN_OWNER,
        "targetAbi": TARGET_ABI,
        "version": version,
        "changelog": changelog,
        "timestamp": timestamp,
    }
    zip_path = ARTIFACTS / f"jellyfin-plugin-wyzie_{version}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("meta.json", json.dumps(meta, indent=2))
        z.write(JELLYFIN_BIN / "Jellyfin.Plugin.Wyzie.dll", "Jellyfin.Plugin.Wyzie.dll")
        z.write(COMMON_BIN / "Wyzie.Common.dll", "Wyzie.Common.dll")
    return zip_path


def build_emby_zip(version: str) -> pathlib.Path:
    zip_path = ARTIFACTS / f"emby-plugin-wyzie_{version}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(EMBY_BIN / "Emby.Plugin.Wyzie.dll", "Emby.Plugin.Wyzie.dll")
        z.write(COMMON_BIN / "Wyzie.Common.dll", "Wyzie.Common.dll")
    return zip_path


def update_manifest(version: str, changelog: str, timestamp: str, zip_url: str, checksum: str) -> None:
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

    entry = manifest[0]
    versions = [v for v in entry.get("versions", []) if v.get("version") != version]
    versions.insert(0, {
        "version": version,
        "changelog": changelog,
        "targetAbi": TARGET_ABI,
        "sourceUrl": zip_url,
        "checksum": checksum,
        "timestamp": timestamp,
    })
    entry["versions"] = versions
    manifest[0] = entry

    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True, help="SemVer-like 1.0.0.0")
    p.add_argument("--repo", default="", help="owner/repo for GitHub release URL")
    p.add_argument("--changelog", default="", help="Changelog text for this version")
    args = p.parse_args()

    version = args.version.lstrip("v")
    timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    if ARTIFACTS.exists():
        shutil.rmtree(ARTIFACTS)
    ARTIFACTS.mkdir()

    jf_zip = build_jellyfin_zip(version, args.changelog, timestamp)
    em_zip = build_emby_zip(version)

    jf_checksum = md5(jf_zip)
    print(f"jellyfin zip  : {jf_zip.name}  md5={jf_checksum}")
    print(f"emby zip      : {em_zip.name}  md5={md5(em_zip)}")

    if args.repo:
        zip_url = f"https://github.com/{args.repo}/releases/download/v{version}/{jf_zip.name}"
        update_manifest(version, args.changelog, timestamp, zip_url, jf_checksum)
        print(f"manifest.json : updated → {zip_url}")
    else:
        print("manifest.json : skipped (no --repo)")

    return 0


if __name__ == "__main__":
    sys.exit(main())
