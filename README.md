# Quantum Design DAT Tool (C#)

Rebuilt in .NET 8/WPF from the original Python utility. The app parses Quantum Design DAT files, detects the measurement type, suggests intelligent defaults, filters stable segments, overlays charts, and exports CSV data.

## Runtime Requirements

- Windows 10 or later
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)  
  > The published build is **framework-dependent**. Install the runtime first. If you need an “all-in-one” package, publish with `--self-contained true`.

## Install for Windows Users

1. **Install .NET 8 Desktop Runtime (only if prompted)**  
   - Most users can simply run the installer; if the system is missing .NET 8 Desktop Runtime, the setup will show a message and open the official download page.  
   - On that page, install **.NET 8 Desktop Runtime (x64)**, then re‑run the Quantum Design DAT Tool installer.
2. **Run the installer**  
   - Click the link below to download the setup package directly (served via GitHub’s raw file endpoint, pointing at `releases/latest/QuantumDatToolSetup.exe`):  
     [Download QuantumDatToolSetup.exe](https://raw.githubusercontent.com/yuanxiuliang/Quantum-Design-DAT-Tool/main/releases/latest/QuantumDatToolSetup.exe)  
   - After the download finishes, double‑click `QuantumDatToolSetup.exe` (standard Windows installer) and follow the wizard.  
   - The default install path is `C:\Program Files\Quantum Design DAT Tool`, but you can change it if needed.
3. **Launch the app**  
   - Use the Start menu entry **Quantum Design DAT Tool** or the optional desktop shortcut.  
   - The installer also registers the `.dat` file extension, so double‑clicking any `.dat` file in Explorer will open it in this tool and plot the data.
4. **Uninstall**  
   - Open **Settings → Apps → Installed apps** (or **Control Panel → Programs and Features**), find **Quantum Design DAT Tool**, and uninstall it.  
   - Uninstalling removes the Start menu entry, desktop shortcut, and `.dat` file association.

## Quick Start (for portable/manual runs)

1. Copy the contents of `DatTool.UI/bin/Release/net8.0-windows10.0.19041/win-x64/publish/` to the target folder.
2. Double-click `DatTool.UI.exe` (this “portable” mode is intended for developers or advanced users; normal users should prefer the installer).
3. Click **Open DAT** on the left to pick a file (PPMS/VSM DAT files are supported).
4. The app will:
   - Parse headers, columns, and metadata;
   - Detect the measurement type and auto-select X/Y axes plus filter defaults;
   - Show the file path in the **DAT File Path** panel.
5. Adjust settings if needed:
   - **Coordinate Columns**: choose X and Y columns.
   - **Filter Parameters**: select the column, set Target Mean/Tolerance/Min Continuous Rows. Leave Target Mean empty for auto-detect.
6. Click **Apply Filter**: the list shows mean/start/end/points/std-dev. Use Shift/Ctrl to multi-select segments; the chart overlays them.
7. Extras:
   - **Plot Type** toggles (Line / Scatter / Line + Scatter).
   - Left drag to zoom, right-click to step back out.
   - **Export Selected** to create a CSV snapshot of chosen segments.

## FAQ

### “Missing .NET” at startup
Install .NET 8 Desktop Runtime. Without admin rights, request the self-contained build (~180 MB).

### Build/publish fails because files are locked
Close any running `DatTool.UI.exe` before calling `dotnet build` or `dotnet publish`.

### Packaging into an installer
The repo ships a plain exe. Wrap the publish folder using WiX, MSIX Packaging Tool, or Inno Setup if you need an installer.

## Developer Cheatsheet

```bash
# Run with hot reload/debug
dotnet run --project DatTool.UI

# Release, self-contained, single file
dotnet publish DatTool.UI/DatTool.UI.csproj -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false

# Release, framework-dependent (current shipping option)
dotnet publish DatTool.UI/DatTool.UI.csproj -c Release -r win-x64 --self-contained false
```

## Build a Windows Installer (Inno Setup)

1. Ensure a Release publish exists (framework-dependent or self-contained):  
   `dotnet publish DatTool.UI/DatTool.UI.csproj -c Release -r win-x64 --self-contained false`
2. Install [Inno Setup](https://jrsoftware.org/isinfo.php) locally.
3. Open `installers/QuantumDatToolSetup.iss` in Inno Setup Compiler.  
   - Update `MyAppVersion` and `PublishDir` if paths differ.
4. Compile (`Build > Compile`) to generate `dist/QuantumDatToolSetup.exe`.
5. During installation, the script checks for the **.NET 8 Desktop Runtime** using the Windows registry; if missing, users are prompted to download it before continuing.
6. Test the installer on a clean Windows VM: install, launch the app, uninstall, and verify shortcuts/registry entries are removed (including the `.dat` file association that lets users open data files with a double-click).

### Custom Icon

- The WPF app and installer both use `DatTool.UI/Assets/QuantumDatTool.ico` (derived from files in `logo/`). Replace this file to update branding; rebuild the app and recompile the installer afterward.

## Solution Layout

- `DatTool.Domain`: core entities (columns, rows, segments, metadata)
- `DatTool.Services`: DAT parsing, measurement defaults, filtering
- `DatTool.ViewModels`: MVVM layer (`MainViewModel`, commands, state)
- `DatTool.UI`: WPF/XAML views with OxyPlot-based visualization
- `DatTool.Tests`: xUnit tests covering parser/defaults/filter logic

Need new measurement presets, export formats, or installer scripts? Extend the corresponding project and rebuild. Happy hacking!***

