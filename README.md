# Filter Cast

A small command-line tool to extract or keep the audio and video streams of a video using FFmpeg.

The code is split by responsibility:

- `filter_media.py`: CLI interface and orchestration
- `media_commands.py`: calls to `yt-dlp`, `ffprobe`, and `ffmpeg`
- `media_service.py`: track lookup and selection
- `media_config.py`: accepted extensions and modes
- `test_filter_media.py`: unit tests

## Requirements

- Python 3.10 or newer
- FFmpeg installed and available on the `PATH` (`ffmpeg` and `ffprobe`)
- `yt-dlp` installed and available on the `PATH` for downloading

On Windows, with `winget`:

```powershell
winget install Gyan.FFmpeg.Shared
python -m pip install -U yt-dlp
```

## Usage

Place your videos in the `input` folder, then run:

```powershell
python filter_media.py
```

The result is created in `output`. By default, the tool keeps the first video track and the first audio track.

Keep only audio or only video:

```powershell
python filter_media.py --mode audio
python filter_media.py --mode video
```

List available audio tracks and their metadata:

```powershell
python filter_media.py --list-audio
```

Select a specific audio track:

```powershell
python filter_media.py --mode audio --audio-index 2
python filter_media.py --mode both --language fr
python filter_media.py --mode audio --title music
```

`--audio-index` is zero-based and matches the index shown by `--list-audio`. `--language` looks for an exact language match (`fr`, `en`, etc.); `--title` searches for a word in the track's title or name. These filters can be combined.

Use different folders:

```powershell
python filter_media.py --input my-videos --output exports --mode audio
```

Processing uses `-c copy`: the selected streams are not re-encoded, which is fast and avoids any quality loss. Files with common video/audio extensions are processed automatically.

## Separating vocals with Demucs

Demucs is optional and runs locally. There's no API cost, but the first run downloads the model and the computation uses CPU or GPU resources.

Installation:

```powershell
python -m pip install -U demucs
```

Separate vocals from the accompaniment:

```powershell
python filter_media.py --input input --output output --separate vocals --device cuda
```

The same process can be used to get the accompaniment (`no_vocals`) or all four stems (`all`):

```powershell
python filter_media.py --separate no_vocals --device cuda
python filter_media.py --separate all --device cuda
```

Results are placed in `output/separated/htdemucs/<file-name>/`. With `--device cpu`, Demucs runs without a graphics card, but more slowly. The model mainly separates vocals from accompaniment; it doesn't guarantee a perfect split between speech, background noise, and music.

## Downloading from a URL

Download and then process a video into `input/`:

```powershell
python filter_media.py --download "https://example.com/video" --download-mode both
```

Download only the audio or only the video:

```powershell
python filter_media.py --download "https://example.com/video" --download-mode audio
python filter_media.py --download "https://example.com/video" --download-mode video
```

If PowerShell doesn't recognize FFmpeg yet after installing it, point directly to its folder:

```powershell
python filter_media.py --ffmpeg-location "C:\path\to\ffmpeg\bin" --download "URL" --download-mode audio
```

After downloading, the file is processed according to `--mode` and the result is placed in `output/`. For example, to download a video and then extract its French audio track:

```powershell
python filter_media.py --download "https://example.com/video" --download-mode both --mode audio --language fr
```

Only use content you have the right to download and process.

## Tests

Run the unit tests:

```powershell
python -m unittest -v
```
