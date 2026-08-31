from __future__ import annotations

import argparse
import sys
from pathlib import Path

from media_commands import command_path, download_media, process_media, probe_media, separate_audio
from media_config import MODES
from media_service import choose_audio_stream, list_audio_tracks, media_files, output_path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Filtre les flux audio et vidéo des fichiers placés dans input/.")
    parser.add_argument("--input", type=Path, default=Path("input"))
    parser.add_argument("--output", type=Path, default=Path("output"))
    parser.add_argument("--mode", choices=MODES)
    parser.add_argument("--audio-index", type=int)
    parser.add_argument("--language")
    parser.add_argument("--title")
    parser.add_argument("--list-audio", action="store_true")
    parser.add_argument("--download", metavar="URL")
    parser.add_argument("--download-mode", choices=MODES, default="both")
    parser.add_argument(
        "--separate",
        choices=("vocals", "no_vocals", "all"),
        help="sépare la voix et l'accompagnement avec Demucs",
    )
    parser.add_argument("--demucs-model", default="htdemucs", help="modèle Demucs")
    parser.add_argument("--device", choices=("auto", "cpu", "cuda"), default="auto")
    parser.add_argument(
        "--ffmpeg-location",
        metavar="DOSSIER",
        help="dossier contenant ffmpeg.exe et ffprobe.exe",
    )
    return parser.parse_args()


def process_files(args: argparse.Namespace, ffmpeg: str, ffprobe: str, mode: str) -> int:
    files = media_files(args.input)
    if not files:
        print(f"Aucun fichier média dans {args.input}")
        return 0
    args.output.mkdir(parents=True, exist_ok=True)
    for source in files:
        streams = probe_media(ffprobe, source).get("streams", [])
        selected_index = selected_number = None
        if mode in {"audio", "both"} and (args.audio_index is not None or args.language or args.title):
            selected_index, selected_number = choose_audio_stream(streams, args.audio_index, args.language, args.title)
        process_media(source, output_path(source, args.output, mode, selected_number), mode, selected_index, ffmpeg)
    return 0


def main() -> int:
    args = parse_args()
    if not args.input.is_dir():
        if args.download:
            args.input.mkdir(parents=True, exist_ok=True)
        else:
            print(f"Erreur : le dossier d'entrée n'existe pas : {args.input}", file=sys.stderr)
            return 2
    try:
        if args.download:
            download_media(
                args.download,
                args.input,
                args.download_mode,
                command_path("yt-dlp"),
                args.ffmpeg_location,
            )
        if args.separate:
            args.output.mkdir(parents=True, exist_ok=True)
            ffmpeg_path = command_path("ffmpeg", args.ffmpeg_location)
            ffmpeg_location = str(Path(ffmpeg_path).parent)
            for source in media_files(args.input):
                separate_audio(
                    source,
                    args.output,
                    args.separate,
                    sys.executable,
                    args.demucs_model,
                    args.device,
                    ffmpeg_location,
                )
            return 0
        ffprobe = command_path("ffprobe", args.ffmpeg_location)
        if args.list_audio:
            return list_audio_tracks(args.input, ffprobe)
        mode = args.mode or (args.download_mode if args.download else "both")
        return process_files(args, command_path("ffmpeg", args.ffmpeg_location), ffprobe, mode)
    except (RuntimeError, ValueError) as error:
        print(f"Erreur : {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
