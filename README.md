# Quantum Design DAT Tool (C#)

Rebuilt in .NET 8/WPF from the original Python utility. The app parses Quantum Design DAT files, detects the measurement type, suggests intelligent defaults, filters stable segments, overlays charts, and exports CSV data.

## Runtime Requirements

- Windows 10 or later
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)  
  > The published build is **framework-dependent**. Install the runtime first. If you need an “all-in-one” package, publish with `--self-contained true`.

## Install for Windows Users

1. **Install .NET 8 Desktop Runtime (if prompted)**  
   - Most users可以直接运行安装程序；如果系统缺少 .NET 8 Desktop Runtime，安装器会弹出提示并打开官方下载页面。  
   - 在该页面安装“**.NET 8 Desktop Runtime (x64)**”，安装完成后重新运行 Quantum Design DAT Tool 安装程序。
2. **运行安装程序**  
   - 直接点击这里下载最新安装包：  
     `[下载 QuantumDatToolSetup.exe](https://github.com/yuanxiuliang/Quantum-Design-DAT-Tool/releases/latest/download/QuantumDatToolSetup.exe)`  
   - 下载完成后，双击 `QuantumDatToolSetup.exe`（标准 Windows 安装包），按向导一步步完成安装。  
   - 默认安装路径为 `C:\Program Files\Quantum Design DAT Tool`，可根据需要修改。
3. **启动程序**  
   - 通过「开始菜单 → Quantum Design DAT Tool」或桌面快捷方式启动。  
   - 安装器会注册 `.dat` 文件扩展名，之后在资源管理器中双击任意 `.dat` 文件，会自动启动本程序并绘制数据图。
4. **卸载程序**  
   - 打开「设置 → 应用 → 已安装的应用」（或「控制面板 → 程序和功能」），找到 **Quantum Design DAT Tool** 并卸载。  
   - 卸载会一并移除开始菜单/桌面快捷方式以及 `.dat` 关联设置。

## Quick Start (for portable/manual runs)

1. Copy the contents of `DatTool.UI/bin/Release/net8.0-windows10.0.19041/win-x64/publish/` to the target folder.
2. Double-click `DatTool.UI.exe`（这种“便携运行”方式主要面向开发者或高级用户；普通用户推荐使用安装程序）。
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

