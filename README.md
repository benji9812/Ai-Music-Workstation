# AI Music Workstation

## Overview
AI Music Workstation is a WPF desktop app backed by a local Python analysis service. The app auto-starts the Python service and communicates via HTTP.

## Prerequisites
- **.NET SDK 10.0** (for building the WPF app)
- **Python 3.11+** (for the local analysis service)
- A valid **`GOOGLE_API_KEY`** for Gemini (stored in `.env`)

## Quick Start
1. **Restore .NET dependencies**
   ```powershell
   dotnet restore
   ```

2. **Prepare Python environment**
   - Ensure a `.env` file exists at the repository root containing:
     ```
     GOOGLE_API_KEY=your_key_here
     ```
   - Run the setup script:
     ```powershell
     ./setup.ps1
     ```

3. **Build and run the app**
   ```powershell
   dotnet build
   ```
   Then run `AiMusicWorkstation.Desktop` from Visual Studio or with `dotnet run`.

## Python Configuration
The desktop app uses a local Python server from `PythonEngine/main.py`. Configuration can be overridden via `appsettings.json` or user secrets under the `Python` section:

```json
{
  "Python": {
    "BaseUrl": "http://127.0.0.1:8000/",
    "TimeoutMinutes": 10,
    "ExecutablePath": "C:\\path\\to\\python.exe",
    "ServerScriptPath": "C:\\path\\to\\main.py"
  }
}
```

If not specified, the app defaults to `PythonEngine/venv/Scripts/python.exe` and `PythonEngine/main.py` relative to the solution root.

## API Project Status
`AiMusicWorkstation.Api` is currently a minimal placeholder project with no referenced runtime usage in the desktop app. If it is not intended for future use, consider retiring it from the solution or documenting a concrete purpose.

## Repository Layout
- `AiMusicWorkstation.Desktop`: WPF UI
- `AiMusicWorkstation.Application`: Application layer
- `AiMusicWorkstation.Infrastructure`: Persistence and adapters
- `PythonEngine`: Local Python analysis service

## Notes
- The app auto-starts the Python server when it initializes the `PythonBridge` and will retry a health check before analysis requests.
- `PythonEngine/requirements.txt` reflects the current Python environment.
