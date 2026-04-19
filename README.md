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
   Start both `AiMusicWorkstation.Api` and `AiMusicWorkstation.Desktop` from Visual Studio (multi-startup) or run them separately with `dotnet run`.

## Python Configuration
The desktop app targets the local API (`AiMusicWorkstation.Api`), which proxies requests to the Python engine (`PythonEngine/main.py`, default port `8000`). Configuration can be overridden via `appsettings.json` or user secrets.

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

If not specified, the API defaults to `PythonEngine/venv/Scripts/python.exe` and `PythonEngine/main.py` relative to the solution root.

### Desktop appsettings
```json
{
  "Python": {
    "BaseUrl": "http://localhost:5082/",
    "TimeoutMinutes": 10
  }
}
```

### API appsettings
```json
{
  "PythonEngine": {
    "BaseUrl": "http://127.0.0.1:8000/",
    "TimeoutMinutes": 10
  }
}
```

## API Project Status
`AiMusicWorkstation.Api` now acts as a local proxy to the Python engine during development. Start both the API and Desktop projects to run end-to-end.

## Repository Layout
- `AiMusicWorkstation.Desktop`: WPF UI
- `AiMusicWorkstation.Application`: Application layer
- `AiMusicWorkstation.Infrastructure`: Persistence and adapters
- `PythonEngine`: Local Python analysis service

## Notes
- The app auto-starts the Python server when it initializes the `PythonBridge` and will retry a health check before analysis requests.
- `PythonEngine/requirements.txt` reflects the current Python environment.
