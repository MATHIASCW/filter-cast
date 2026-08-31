import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from media_commands import command_path, download_options, separate_audio, stream_mapping
from media_service import choose_audio_stream, media_files, output_path


class MediaTests(unittest.TestCase):
    def test_command_path_accepts_a_tool_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            executable = Path(directory) / "ffmpeg.exe"
            executable.touch()
            self.assertEqual(command_path("ffmpeg", directory), str(executable))

    def test_download_options_have_a_format_for_each_mode(self):
        self.assertEqual(download_options("audio")[:2], ["-f", "bestaudio/best"])
        self.assertIn("--merge-output-format", download_options("both"))

    def test_stream_mapping_selects_requested_audio(self):
        self.assertEqual(stream_mapping("both", 3), ["-map", "0:v:0", "-map", "0:3", "-c", "copy"])

    def test_separate_audio_builds_a_demucs_command(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "clip.m4a"
            source.touch()
            output = Path(directory) / "output"
            with patch("media_commands.run_command") as run:
                separate_audio(source, output, "vocals", "python", device="cuda")
            command = run.call_args.args[0]
            self.assertEqual(command[:4], ["python", "-m", "demucs.separate", "-n"])
            self.assertIn("--two-stems=vocals", command)
            self.assertIn("--device", command)

    def test_separate_audio_passes_ffmpeg_path_to_demucs(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "clip.m4a"
            source.touch()
            with patch("media_commands.run_command") as run:
                separate_audio(source, Path(directory), "vocals", "python", ffmpeg_location="C:\\ffmpeg\\bin")
            environment = run.call_args.kwargs["environment"]
            self.assertTrue(environment["PATH"].startswith("C:\\ffmpeg\\bin"))

    def test_choose_audio_stream_by_language_and_title(self):
        streams = [
            {"index": 1, "codec_type": "video"},
            {"index": 2, "codec_type": "audio", "tags": {"language": "fr", "title": "Voix"}},
            {"index": 3, "codec_type": "audio", "tags": {"language": "en", "title": "Music"}},
        ]
        self.assertEqual(choose_audio_stream(streams, None, "fr", None), (2, 0))
        self.assertEqual(choose_audio_stream(streams, None, None, "music"), (3, 1))

    def test_media_files_and_output_path(self):
        with tempfile.TemporaryDirectory() as directory:
            input_dir = Path(directory)
            (input_dir / "clip.mp4").touch()
            (input_dir / "notes.txt").touch()
            self.assertEqual(media_files(input_dir), [input_dir / "clip.mp4"])
            self.assertEqual(output_path(input_dir / "clip.mp4", input_dir, "audio", 1).name, "clip_audio_track1.m4a")


if __name__ == "__main__":
    unittest.main()
