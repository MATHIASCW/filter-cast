from __future__ import annotations

import json
import os
import shutil
import subprocess
from pathlib import Path


def installed_ffmpeg_location() -> str | None:
    package_root = Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "WinGet" / "Packages"
    if not package_root.is_dir():
        return None
    for executable in package_root.rglob("ffmpeg.exe"):
        if (executable.parent / "ffprobe.exe").is_file():
            return str(executable.parent)
    return None


def command_path(name: str, location: str | None = None) -> str:
    path = None
    if location:
        location_path = Path(location)
        candidates = [location_path] if location_path.is_file() else [location_path / name, location_path / f"{name}.exe"]
        path = next((str(candidate) for candidate in candidates if candidate.is_file()), None)
    else:
        path = shutil.which(name)
        if path is None:
            installed_location = installed_ffmpeg_location()
            if installed_location:
                path = command_path(name, installed_location)
    if path is None:
        suffix = f" dans {location}" if location else " dans le PATH"
        raise RuntimeError(f"{name} est introuvable{suffix}.")
    return path


def run_command(
    command: list[str],
    error_message: str,
    capture_output: bool = True,
    environment: dict[str, str] | None = None,
) -> str:
    result = subprocess.run(
        command,
        capture_output=capture_output,
        text=True,
        check=False,
        env=environment,
    )
    if result.returncode == 0:
        return result.stdout or ""
    details = (result.stderr or "").strip().splitlines()
    raise RuntimeError(details[-1] if details else error_message)


def download_options(download_mode: str) -> list[str]:
    options = {
        "audio": ["-f", "bestaudio/best", "-x", "--audio-format", "m4a"],
        "video": ["-f", "bestvideo[ext=mp4]/best[ext=mp4]/best", "--merge-output-format", "mp4"],
        "both": ["-f", "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best", "--merge-output-format", "mp4"],
    }
    return options[download_mode]


def download_media(
    url: str,
    input_dir: Path,
    download_mode: str,
    yt_dlp: str,
    ffmpeg_location: str | None = None,
) -> None:
    input_dir.mkdir(parents=True, exist_ok=True)
    command = [yt_dlp, "--no-playlist", "-o", str(input_dir / "%(title)s.%(ext)s")]
    ffmpeg_location = ffmpeg_location or installed_ffmpeg_location()
    if ffmpeg_location:
        command += ["--ffmpeg-location", ffmpeg_location]
    command += download_options(download_mode) + [url]
    run_command(command, "le téléchargement a échoué", capture_output=False)
    print(f"Téléchargement terminé dans {input_dir}")


def probe_media(ffprobe: str, source: Path) -> dict:
    command = [
        ffprobe, "-v", "error",
        "-show_entries", "stream=index,codec_type:stream_tags=language,title,handler_name",
        "-of", "json", str(source),
    ]
    return json.loads(run_command(command, "fichier média illisible"))


def stream_mapping(mode: str, selected_audio_index: int | None) -> list[str]:
    audio_map = f"0:{selected_audio_index}" if selected_audio_index is not None else "0:a:0"
    mappings = {
        "audio": ["-map", audio_map, "-vn", "-c:a", "copy"],
        "video": ["-map", "0:v:0", "-an", "-c:v", "copy"],
        "both": ["-map", "0:v:0", "-map", audio_map, "-c", "copy"],
    }
    return mappings[mode]


def process_media(source: Path, destination: Path, mode: str, selected_audio_index: int | None, ffmpeg: str) -> None:
    command = [ffmpeg, "-y", "-i", str(source)]
    command += stream_mapping(mode, selected_audio_index) + [str(destination)]
    run_command(command, "FFmpeg a échoué")
    print(f"OK  {source.name} -> {destination.name}")


def separate_audio(
    source: Path,
    output_dir: Path,
    stem: str,
    python_executable: str,
    model: str = "htdemucs",
    device: str = "auto",
    ffmpeg_location: str | None = None,
) -> None:
    destination = output_dir / "separated"
    command = [
        python_executable,
        "-m",
        "demucs.separate",
        "-n",
        model,
        "-o",
        str(destination),
    ]
    if stem in {"vocals", "no_vocals"}:
        command += ["--two-stems=vocals"]
    if device != "auto":
        command += ["--device", device]
    command.append(str(source))
    environment = None
    if ffmpeg_location:
        environment = os.environ.copy()
        environment["PATH"] = f"{ffmpeg_location}{os.pathsep}{environment.get('PATH', '')}"
    run_command(command, "Demucs a échoué", capture_output=False, environment=environment)
    print(f"OK  séparation audio -> {destination / model / source.stem}")
