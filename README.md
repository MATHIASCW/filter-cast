# Filter Cast

A small command-line tool to extract or preserve audio and video streams from media with FFmpeg.

## .NET Web Interface

A local ASP.NET Core interface is available in `web/`. It lets you choose a file or URL, output mode, audio track, and Demucs voice separation.

Run the interface from the project root:

```powershell
dotnet run --project web/web.csproj --no-launch-profile
```

Then open [http://127.0.0.1:5080](http://127.0.0.1:5080). The address `127.0.0.1` limits access to the local machine. Uploaded files are stored in `input/` and results in `output/`, both ignored by Git.

The interface calls the Python script without a system shell, using separate arguments. It limits uploads to 500 MB, generates a random filename, and only accepts HTTP/HTTPS URLs. This is not an interface meant to be exposed directly on the internet. For public deployment, you would need to add authentication, HTTPS, quotas, file cleanup, and CSRF protection.

The code is separated by responsibility:

- `filter_media.py`: CLI interface and orchestration
- `media_commands.py`: calls to `yt-dlp`, `ffprobe`, and `ffmpeg`
- `media_service.py`: track discovery and selection
- `media_config.py`: supported extensions and modes
- `test_filter_media.py`: unit tests

## Requirements

- Python 3.10 or later
- FFmpeg installed and available in `PATH` (`ffmpeg` and `ffprobe`)
- `yt-dlp` installed and available in `PATH` for downloads

On Windows, using `winget`:

```powershell
winget install Gyan.FFmpeg.Shared
python -m pip install -U yt-dlp
```

## Usage

Place media files in the `input` folder, then run:

```powershell
python filter_media.py
```

The result is created in `output`. By default, the tool preserves the first video track and the first audio track.

Extract audio only or video only:

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

`--audio-index` is zero-indexed and matches the index shown by `--list-audio`. `--language` searches for an exact language code (`fr`, `en`, etc.); `--title` searches for a word in the track title or handler name. These filters can be combined.

Use different folders:

```powershell
python filter_media.py --input my-videos --output exports --mode audio
```

Processing uses `-c copy`: selected streams are not re-encoded, which is fast and preserves quality. Files with common audio/video extensions are processed automatically.

## Separate Voice with Demucs

Demucs is optional and runs locally. There is no API cost, but the first run downloads the model and computation uses CPU or GPU.

Installation:

```powershell
python -m pip install -U demucs
```

Separate vocals from accompaniment:

```powershell
python filter_media.py --input input --output output --separate vocals --device cuda
```

The same computation can be used to extract accompaniment (`no_vocals`) or all stems (`all`):

```powershell
python filter_media.py --separate no_vocals --device cuda
python filter_media.py --separate all --device cuda
```

Results are in `output/separated/htdemucs/<filename>/`. With `--device cpu`, Demucs runs without a GPU but more slowly. The model primarily separates vocals and accompaniment; it does not guarantee perfect separation between speech, background noise, and music.

## Download from a URL

Download and then process a media file into `input/`:

```powershell
python filter_media.py --download "https://example.com/video" --download-mode both
```

Download audio only or video only:

```powershell
python filter_media.py --download "https://example.com/video" --download-mode audio
python filter_media.py --download "https://example.com/video" --download-mode video
```

If PowerShell does not yet recognize FFmpeg after installation, specify its folder directly:

```powershell
python filter_media.py --ffmpeg-location "C:\path\to\ffmpeg\bin" --download "URL" --download-mode audio
```

After download, the file is processed according to `--mode` and the result is placed in `output/`. For example, to download and extract a French audio track:

```powershell
python filter_media.py --download "https://example.com/video" --download-mode both --mode audio --language fr
```

Only download and process content you have the right to use.

## Tests

Run unit tests:

```powershell
python -m unittest -v
```