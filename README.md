RNNoise Denoiser (Windows, .NET 8)

Local speech denoising for video/audio via FFmpeg's arnndn filter (RNNoise). WinForms UI, batch-friendly, no internet required.


✨ Features

Batch processing of video/audio files

Keep video un-reencoded (optional) — clean audio only

Adjustable chain: highpass / lowpass → aresample=48000 → arnndn (RNNoise) → speechnorm

Preset-ready (e.g., Soft/Standard/Aggressive)

Drag & drop, paths and settings persist between runs


🔧 Requirements

Windows 10/11

.NET 8 Desktop Runtime

FFmpeg with the arnndn filter

RNNoise model file (.rnnn)


🔽 Where to get FFmpeg & RNNoise models

FFmpeg (Windows builds):

Official download page → https://ffmpeg.org/download.html

Recommended Windows builds that include arnndn:

Gyan.dev (Full build): https://www.gyan.dev/ffmpeg/builds/

BtbN FFmpeg-Builds (GitHub): https://github.com/BtbN/FFmpeg-Builds

RNNoise (library & models):

RNNoise project: https://github.com/xiph/rnnoise

Pretrained models for FFmpeg arnndn:

arnndn-models: https://github.com/richardpl/arnndn-models (start with std.rnnn)

Community models: https://github.com/GregorR/rnnoise-models

Version guidance: Use FFmpeg 6.x or 7.x (latest stable). If mix option is missing in your build, update FFmpeg or remove :mix= from the filter.


🚀 Installation

Install .NET 8 Desktop Runtime (if you don't have it).

Download and unzip FFmpeg. Note the path to its bin folder (e.g., C:\Tools\ffmpeg\bin).

Download an RNNoise model (e.g., std.rnnn) and place it somewhere stable (e.g., C:\Tools\rnnoise\models\std.rnnn).

Launch the app and set these paths in the top panel.


🖱️ Usage

Set paths to ffmpeg/bin and your .rnnn model in the UI.

Add files (video/audio) to the queue (buttons or drag & drop).

Choose an output folder, tweak mix / filters → Start.

The app by default copies the video stream (no re-encode) and cleans only the audio.


🔬 Minimal console sanity check (optional)

:: Check that arnndn exists
ffmpeg -hide_banner -filters | findstr arnndn
ffmpeg -hide_banner -h filter=arnndn

:: Try the model (adjust your paths; prefer forward slashes in Windows paths)
ffmpeg -i "input.wav" -af aresample=48000,arnndn=m=C:/Tools/rnnoise/models/std.rnnn -ar 48000 "out.wav"


⚙️ Suggested presets

Soft: mix=0.90

Standard: mix=0.85 (default)

Aggressive: mix=0.70 + highpass=80 + lowpass=12000

Tip: If audio sounds “underwater”, raise mix or add a gentle lowpass (11–13 kHz). Keep aresample=48000 before arnndn.


🧩 How it works (short)

arnndn loads a small RNN model (.rnnn) trained to suppress non-speech noise.

We resample to 48 kHz, apply RNNoise, then optionally normalize speech (speechnorm).

For videos, the video stream can be copied as-is; only audio is re-encoded.


🧰 Troubleshooting

FFmpeg code -22 (Invalid argument)

Usually a bad model path inside arnndn.

On Windows, do not use single quotes around the path; prefer forward slashes: m=C:/path/to/model.rnnn.

If the entire -af chain is wrapped in quotes, remove them if unnecessary.

Option 'mix' not found'

Your FFmpeg build is too old or lacks this option. Update FFmpeg or remove :mix=... from arnndn.

No such filter: 'arnndn'

Your FFmpeg build doesn’t include the filter. Install a build that has it (Gyan Full / BtbN).

Error opening model / No such file or directory

Check the exact model path. Try using forward slashes and ensure the file is readable.

Artifacts / “underwater” sound

Increase mix (0.85 → 0.9). Add lowpass=11000–13000. Try a different .rnnn model.

Lip sync issues

The app uses -shortest and copies video; if you still see desync, try re-muxing or re-encoding audio with a constant bitrate.


📦 Build from source (optional)

Visual Studio 2022 or dotnet build -c Release

Publishing: dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false


🔐 Privacy

Everything runs locally. No audio is uploaded anywhere.\


☕ Support the Project
If this tool saves you time, consider supporting development:

- Ko-fi: https://ko-fi.com/timasich
- Donation Alerts: https://www.donationalerts.com/r/timasich

Thank you! ❤️


📖 Terms of Use (Summary)
- The app runs **locally** and processes your files on your machine.
- It uses **FFmpeg** and external **RNNoise models**. You are responsible for complying with their licenses.
- Provided **“as is”**, without warranties or liability. Use at your own risk.
- The project **does not distribute** FFmpeg or RNNoise models; you set paths yourself.


📜 License

MIT. The project does not distribute FFmpeg or RNNoise models; you set paths yourself.
