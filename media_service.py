from __future__ import annotations

from pathlib import Path
from typing import Any

from media_commands import probe_media
from media_config import SUPPORTED_EXTENSIONS


def media_files(input_dir: Path) -> list[Path]:
    return sorted(
        path for path in input_dir.iterdir()
        if path.is_file() and path.suffix.lower() in SUPPORTED_EXTENSIONS
    )


def stream_label(stream: dict[str, Any], audio_number: int) -> str:
    tags = stream.get("tags", {})
    title = tags.get("title") or tags.get("handler_name") or "untitled"
    language = tags.get("language", "unknown language")
    return f"track {audio_number} | {language} | {title}"


def choose_audio_stream(streams: list[dict[str, Any]], audio_index: int | None, language: str | None, title: str | None) -> tuple[int, int]:
    audio_streams = [stream for stream in streams if stream.get("codec_type") == "audio"]
    if not audio_streams:
        raise ValueError("no audio tracks found")
    if audio_index is not None:
        if audio_index < 0 or audio_index >= len(audio_streams):
            raise ValueError(f"--audio-index must be between 0 and {len(audio_streams) - 1}")
        selected = audio_streams[audio_index]
    else:
        candidates = audio_streams
        if language:
            candidates = [stream for stream in candidates if stream.get("tags", {}).get("language", "").lower() == language.lower()]
        if title:
            candidates = [stream for stream in candidates if title.lower() in " ".join((stream.get("tags", {}).get("title", ""), stream.get("tags", {}).get("handler_name", ""))).lower()]
        if not candidates:
            raise ValueError("no audio tracks match the requested filters")
        selected = candidates[0]
    return int(selected["index"]), audio_streams.index(selected)


def output_path(source: Path, output_dir: Path, mode: str, audio_number: int | None) -> Path:
    suffix = ".m4a" if mode == "audio" else ".mp4"
    track_suffix = f"_track{audio_number}" if audio_number is not None else ""
    return output_dir / f"{source.stem}_{mode}{track_suffix}{suffix}"


def list_audio_tracks(input_dir: Path, ffprobe: str) -> int:
    files = media_files(input_dir)
    if not files:
        print(f"No media files in {input_dir}")
        return 0
    for source in files:
        print(f"\n{source.name}")
        streams = probe_media(ffprobe, source).get("streams", [])
        for number, stream in enumerate(stream for stream in streams if stream.get("codec_type") == "audio"):
            print(f"  {number}: {stream_label(stream, number)}")
    return 0
