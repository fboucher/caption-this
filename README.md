# caption-this

Examples in different languages showing how to use the Reka Vision API to caption videos and static images. See the documentation at https://docs.reka.ai/vision for more details. Assets used in the examples are available in the assets folder.

## Folder overview
- `dotnet/`: .NET console app with a menu to list, upload, caption, and delete videos or images against the Reka Vision API. Saves responses under `dotnet/data`. Run with `dotnet run`.
- `http/`: REST Client collection (`caption-this.http`) you can execute in VS Code (with the REST Client extension) to hit the same endpoints. 
- `python/`: Python examples. Install deps with `pip install -r ./requirements.txt` (or activate `python/myenv/`). 
  - `caption_this.py`: Single-shot helper that uploads a video, waits for indexing, requests a caption, and saves JSON to `python/videos/captions/`. Usage: `python caption_this.py path/to/video.mp4`.
  - `vision_app.py`: Interactive menu (list/upload/caption/delete videos and images). Outputs are written to `python/data/`. Run with `python vision_app.py`.
  - `create_video.py`: Google GenAI video generation sample. Set `GOOGLE_API_KEY` and replace the prompt placeholder, then run `python create_video.py` to save a rendered clip under `python/videos/`.

## Environment variables
All examples uses the same `.env` files that should be place at the root. The following environment variables need to be set:
- `API_KEY`: required for Reka Vision API calls (can be placed in `.env`).
- `GOOGLE_API_KEY`: required only for `create_video.py`.
