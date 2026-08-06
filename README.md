# AbcVersion

Automatic semantic versioning for .NET projects based on Git history. AbcVersion calculates version numbers from your repository's commit history and branch configuration — no manual version bumps needed.

Available as both a **CLI tool** and a **NuGet library** targeting .NET 8.0.

## Installation

### CLI Tool (Global)

```bash
dotnet tool install --global Deneblab.AbcVersionCmd
```

Requires the .NET SDK/runtime.

### Native Binary (no .NET required)

Every push to `production` publishes self-contained Native AOT binaries as GitHub Release assets
— no .NET SDK or runtime needed on the target machine.

```bash
# linux-x64
curl -fsSL -o abcversion \
  https://github.com/DenebLab/AbcVersion/releases/latest/download/abcversion-linux-x64
chmod +x abcversion
./abcversion -p semversion
```

```powershell
# win-x64
Invoke-WebRequest -Uri https://github.com/DenebLab/AbcVersion/releases/latest/download/abcversion-win-x64.exe -OutFile abcversion.exe
.\abcversion.exe -p semversion
```

### Docker Build

```dockerfile
FROM alpine:3.20 AS abcversion
RUN apk add --no-cache curl && \
    curl -fsSL -o /usr/local/bin/abcversion \
      https://github.com/DenebLab/AbcVersion/releases/latest/download/abcversion-linux-x64 && \
    chmod +x /usr/local/bin/abcversion

FROM your-base-image AS build
COPY --from=abcversion /usr/local/bin/abcversion /usr/local/bin/abcversion
RUN abcversion -p semversion
```

### GitHub Actions

```yaml
- name: Install abcversion (native, no .NET SDK needed)
  run: |
    curl -fsSL -o /usr/local/bin/abcversion \
      https://github.com/DenebLab/AbcVersion/releases/latest/download/abcversion-linux-x64
    chmod +x /usr/local/bin/abcversion
- name: Get version
  id: get_version
  run: echo "version=$(abcversion -p semversion)" >> $GITHUB_OUTPUT
```

### NuGet Library

```bash
dotnet add package Deneblab.AbcVersion
```

## Quick Start

### 1. Initialize your repository

```bash
cd your-repo
abcversion init
```

This creates a `.abcversion.json` configuration file at the repository root:

```json
{
  "BaseVersion": "0.3.0",
  "Projects": {
    "server": {
      "Path": "src/StashLock.Server",
      "BaseVersion": "2.0.0",
      "Branches": {
        "production": {
          "StartPoint": {
            "Version": "1.5.0",
            "ParentSha": "9a25480..."
          }
        }
      }
    }
  },
  "Snapshots": {
    "9a25480...": "1.2.3"
  }
}
```

For a repository without multiple projects, you can trim this down to just `BaseVersion` — see
[Configuration](#configuration) below for what each section does.

### 2. Get version info

```bash
# Full version details as JSON
abcversion

# Get a specific property
abcversion -p semversion
# Output: 0.1.42

abcversion -p gitbranch
# Output: main
```

## CLI Usage

```
abcversion [options]
```

| Option | Description |
|--------|-------------|
| `--path <path>` | Path to git repository (defaults to current directory) |
| `--project <name>` | Project name from config (defaults to main project) |
| `-p, --property <name>` | Return a single property instead of full JSON |
| `--version` | Display tool version |

### Subcommands

```bash
abcversion init             # Create .abcversion.json in current repo
abcversion init --force     # Overwrite existing config

abcversion info             # Show diagnostic info about version resolution
abcversion info --path ./my-repo

abcversion projects         # List configured projects from .abcversion.json
```

### Available Properties

When using `-p`, you can request any of these properties:

| Property | Example | Description |
|----------|---------|-------------|
| `SemVersion` | `1.1.5` | Semantic version (Major.Minor.Patch) |
| `VersionString` | `1.1.5` | Full version string |
| `Major` | `1` | Major version |
| `Minor` | `1` | Minor version |
| `Patch` | `5` | Patch version (derived from commit count) |
| `PreRelease` | `alpha` | Pre-release label |
| `Meta` | `build.456` | Build metadata |
| `AssemblyVersion` | `1.0.0.0` | .NET assembly version |
| `FileVersion` | `1.1.5.0` | File version |
| `InformationalVersion` | `1.1.5+Branch.main...` | Full informational version |
| `ShortBuildMetaData` | `Branch.main.DateTime...` | Build metadata string (without the leading version) |
| `GitSha` | `a1b2c3d4...` | Current commit SHA |
| `GitBranch` | `main` | Current branch name |
| `DateTime` | `2026-03-12T10:30:45Z` | Build timestamp |
| `Machine` | `WORKSTATION` | Machine name |

## Library Usage

```csharp
using Deneblab.AbcVersion;

// Quick usage
var version = AbcVersionFactory.CreateAbcVersion();
Console.WriteLine(version.SemVersion); // "1.1.5"

// Builder pattern with options
var version = AbcVersionFactory
    .CreateBuilder()
    .SetRepositoryRoot(@"C:\my-repo")
    .SetDateTime(DateTime.UtcNow)
    .UseLoggerFactory(loggerFactory)
    .Build();

Console.WriteLine(version.SemVersion);           // "1.1.5"
Console.WriteLine(version.AssemblyVersion);       // "1.0.0.0"
Console.WriteLine(version.InformationalVersion);  // "1.1.5+Branch.main.DateTime..."
Console.WriteLine(version.GitSha);               // "a1b2c3d4..."
```

### Multi-Project Support

For monorepos with multiple independently versioned projects:

```csharp
var version = AbcVersionFactory
    .CreateBuilder()
    .SetRepositoryRoot(@"C:\my-repo")
    .Build("my-project");
```

## Configuration

The `.abcversion.json` file at your repository root controls version calculation.

### Basic

```json
{
  "BaseVersion": "1.0.0"
}
```

The patch number is derived from the commit count in the first-parent history.

### Branch-Specific Versions

```json
{
  "BaseVersion": "1.0.0",
  "Branches": {
    "release/2.0": {
      "Version": "2.0.0",
      "ParentSha": "abc123..."
    }
  }
}
```

When on the `release/2.0` branch, versioning starts from `2.0.0` and counts commits since the
specified parent SHA. Both `Version` and `ParentSha` are required for any branch listed here —
omitting or nulling either one raises a clear config error rather than being treated as "no start
point".

### Snapshots

Pin specific commits to exact versions:

```json
{
  "BaseVersion": "1.0.0",
  "Snapshots": {
    "abc123def456...": "1.2.3"
  }
}
```

### Multi-Project

```json
{
  "BaseVersion": "1.0.0",
  "Projects": {
    "api": {
      "Name": "api",
      "Path": "src/Api",
      "BaseVersion": "2.0.0"
    },
    "client": {
      "Name": "client",
      "Path": "src/Client",
      "BaseVersion": "1.5.0"
    }
  }
}
```

Each project tracks commits only within its configured path.

## CI/CD Integration

### GitHub Actions

```yaml
steps:
  - uses: actions/checkout@v4
    with:
      fetch-depth: 0  # Full history needed for commit counting

  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '8.0.x'

  - run: dotnet tool install --global Deneblab.AbcVersionCmd

  - name: Get version
    run: |
      VERSION=$(abcversion -p semversion)
      echo "VERSION=$VERSION" >> $GITHUB_ENV

  - run: dotnet pack -p:Version=$VERSION
```

## How It Works

1. Reads `BaseVersion` from `.abcversion.json`
2. Checks if the current commit SHA matches a snapshot — if so, uses that exact version
3. Checks if the current branch has specific configuration with a start point (parent SHA)
4. Counts first-parent commits (optionally scoped to a path for multi-project setups)
5. Derives the patch number from the commit count
6. Produces version properties including SemVersion, assembly versions, and build metadata

## License

[MIT](LICENSE) - Copyright (c) 2024 DenebLab
