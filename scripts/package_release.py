"""Create the Dalamud install ZIP and repository list from the same build."""
import argparse
import json
import re
import struct
import time
import zipfile
from pathlib import Path


def package(build, output, repository, tag, ref=None):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("repository must be owner/name")
    manifest = json.loads((build / "EquinoxCompanion.json").read_text(encoding="utf-8-sig"))
    version = manifest["AssemblyVersion"]
    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", version) or tag != "v" + version:
        raise ValueError("Release tag must equal v plus the compiled AssemblyVersion")
    if manifest["InternalName"] != "EquinoxCompanion" or manifest["DalamudApiLevel"] != 15:
        raise ValueError("Unexpected plugin identity/API; review compatibility before publishing")
    icon = (build / "icon.png").read_bytes()
    if icon[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("Plugin icon must be PNG")
    width, height = struct.unpack(">II", icon[16:24])
    if width != height or not 64 <= width <= 512:
        raise ValueError("Dalamud icon must be square and between 64 and 512 pixels")
    if not manifest.get("IconUrl"):
        raise ValueError("Compiled manifest must include the plugin icon URL")
    catalogue=json.loads((build / "collection-ids.json").read_text())
    categories={x["category"] for x in catalogue}
    if not {"mount","minion","orchestrion","emote","barding","card","ornament","framerkit","hairstyle","quest"}.issubset(categories):
        raise ValueError("Incomplete collection catalogue, including hairstyles")
    if len({(x["category"],x["id"]) for x in catalogue}) != len(catalogue):
        raise ValueError("Duplicate collection identity")
    dll = build / "EquinoxCompanion.dll"
    if not dll.is_file() or dll.read_bytes()[:2] != b"MZ":
        raise ValueError("Compiled plugin DLL is missing or invalid")
    output.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output / "EquinoxCompanion.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for name in ("EquinoxCompanion.dll", "EquinoxCompanion.json", "EquinoxCompanion.deps.json", "icon.png", "collection-ids.json"):
            archive.write(build / name, name)
        for asset in (build / "garden-art").rglob("*"):
            if asset.is_file(): archive.write(asset, asset.relative_to(build))
    link = f"https://github.com/{repository}/releases/download/{tag}/EquinoxCompanion.zip"
    if ref is not None:
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_./-]*", ref) or ".." in ref:
            raise ValueError("Invalid repository ref")
        link = f"https://raw.githubusercontent.com/{repository}/{ref}/dist/{tag}/EquinoxCompanion.zip"
    (output / "icon.png").write_bytes(icon)
    entry = dict(manifest)
    entry.update(IconUrl=manifest["IconUrl"], RepoUrl=f"https://github.com/{repository}", IsHide=False,
                 IsTestingExclusive=False, DownloadLinkInstall=link,
                 DownloadLinkUpdate=link, DownloadLinkTesting=link,
                 LastUpdate=int(time.time()))
    (output / "repo.json").write_text(json.dumps([entry], indent=2) + "\n", encoding="utf-8")
    print(f"Packaged {version}; install index after publication:")
    print(f"https://raw.githubusercontent.com/{repository}/{ref}/repo.json" if ref else f"https://github.com/{repository}/releases/latest/download/repo.json")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--build", type=Path, default=Path("src/bin/Release"))
    parser.add_argument("--output", type=Path, default=Path("release"))
    parser.add_argument("--repository", required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--ref", help="Host ZIP from dist/<tag> on this repository ref instead of Releases")
    args = parser.parse_args()
    package(args.build, args.output, args.repository, args.tag, args.ref)
