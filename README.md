# Riptastic

A small macOS app (Avalonia 12 / .NET 10) that rips a DVD `VIDEO_TS` folder to **MKV** and/or **MP4**, wrapping HandBrake and ffmpeg behind a neat and tidy GUI.

<img width="1024" height="796" alt="Main interface - rip completed" src="https://github.com/user-attachments/assets/848c09e2-02a0-494f-bae6-c8c2bbdb7a75" />

## Features

- **Startup dependency check:**  
  Verifies the required tools are installed and, if any are missing, shows the exact `brew install` command to fix it.
- **Drag & drop:**   
  Drop `VIDEO_TS` folder (or browse). Titles are scanned and the lengthiest title is auto-selected; you can override it.
- **Options:**   
  Output folder, file name, quality preset (High / Balanced / Smaller), and MKV / MP4 / both.
- **Live progress:**  
  Activity window to see rip in action - with cancel option.
- **Detects the picture ratio:**  
 Detects ratio and outputs it correctly with square pixels. For 2.35:1 offers option for "fill 16:9" side-crop.

**MKV** keeps AC3 5.1 surround, all subtitle languages, and chapters.
**MP4** carries AAC stereo + AC3 5.1 and chapters for maximum device compatibility (no subtitles — MP4 can't hold DVD bitmap subs). When both are selected the video is encoded once and the MP4 is remuxed from it (no re-encode, no quality loss).

## Requirements

macOS with these tools (install via [Homebrew](https://brew.sh)):

```sh
brew install handbrake ffmpeg libdvdcss
```

Plus the [.NET 10 SDK](https://dotnet.microsoft.com/download) to build.

## Run

```sh
dotnet run
```

## Future Improvements

At the moment this just supplies what I need (e.g. macOS), but if I get any requests for Windows or Linux support I'd be happy to extend to cover those.

## Licence

MIT
