# Filter Cast

Petit outil en ligne de commande pour extraire ou conserver les flux audio et vidéo d'une vidéo avec FFmpeg.

Le code est séparé par responsabilité :

- `filter_media.py` : interface CLI et orchestration
- `media_commands.py` : appels à `yt-dlp`, `ffprobe` et `ffmpeg`
- `media_service.py` : recherche et sélection des pistes
- `media_config.py` : extensions et modes acceptés
- `test_filter_media.py` : tests unitaires

## Pré-requis

- Python 3.10 ou plus récent
- FFmpeg installé et disponible dans le `PATH` (`ffmpeg` et `ffprobe`)
- `yt-dlp` installé et disponible dans le `PATH` pour le téléchargement

Sous Windows, avec `winget` :

```powershell
winget install Gyan.FFmpeg.Shared
python -m pip install -U yt-dlp
```

## Utilisation

Placez les vidéos dans le dossier `input`, puis lancez :

```powershell
python filter_media.py
```

Le résultat est créé dans `output`. Par défaut, l'outil conserve la première piste vidéo et la première piste audio.

Choisir uniquement l'audio ou uniquement la vidéo :

```powershell
python filter_media.py --mode audio
python filter_media.py --mode video
```

Afficher les pistes audio disponibles et leurs métadonnées :

```powershell
python filter_media.py --list-audio
```

Sélectionner une piste audio particulière :

```powershell
python filter_media.py --mode audio --audio-index 2
python filter_media.py --mode both --language fr
python filter_media.py --mode audio --title musique
```

`--audio-index` est numéroté à partir de `0` et correspond à l'index affiché par `--list-audio`. `--language` recherche une langue exacte (`fr`, `en`, etc.) ; `--title` recherche un mot dans le titre ou le nom de la piste. Ces filtres peuvent être combinés.

Utiliser d'autres dossiers :

```powershell
python filter_media.py --input mes-videos --output exports --mode audio
```

Le traitement utilise `-c copy` : les flux sélectionnés ne sont pas réencodés, ce qui est rapide et évite une perte de qualité. Les fichiers portant les extensions vidéo/audio courantes sont traités automatiquement.

## Séparer la voix avec Demucs

Demucs est optionnel et fonctionne localement. Il n'y a pas de coût d'API, mais le premier lancement télécharge le modèle et le calcul consomme du CPU ou du GPU.

Installation :

```powershell
python -m pip install -U demucs
```

Séparer la voix de l'accompagnement :

```powershell
python filter_media.py --input input --output output --separate vocals --device cuda
```

Le même calcul peut être demandé pour récupérer l'accompagnement (`no_vocals`) ou les quatre stems (`all`) :

```powershell
python filter_media.py --separate no_vocals --device cuda
python filter_media.py --separate all --device cuda
```

Les résultats sont dans `output/separated/htdemucs/<nom-du-fichier>/`. Avec `--device cpu`, Demucs fonctionne sans carte graphique, mais plus lentement. Le modèle sépare principalement la voix et l'accompagnement ; il ne garantit pas une séparation parfaite entre voix parlée, bruit de fond et musique.

## Télécharger depuis une URL

Télécharger puis traiter une vidéo dans `input/` :

```powershell
python filter_media.py --download "https://exemple.com/video" --download-mode both
```

Télécharger uniquement l'audio ou uniquement la vidéo :

```powershell
python filter_media.py --download "https://exemple.com/video" --download-mode audio
python filter_media.py --download "https://exemple.com/video" --download-mode video
```

Si PowerShell ne reconnaît pas encore FFmpeg après son installation, indiquez directement son dossier :

```powershell
python filter_media.py --ffmpeg-location "C:\chemin\vers\ffmpeg\bin" --download "URL" --download-mode audio
```

Après le téléchargement, le fichier est traité selon `--mode` et le résultat est placé dans `output/`. Par exemple, pour télécharger puis extraire une piste audio française :

```powershell
python filter_media.py --download "https://exemple.com/video" --download-mode both --mode audio --language fr
```

Utilisez uniquement des contenus que vous avez le droit de télécharger et de traiter.

## Tests

Lancer les tests unitaires :

```powershell
python -m unittest -v
```