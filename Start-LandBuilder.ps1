param(
    [switch]$SkipTests,
    [switch]$Editor
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Label" -ForegroundColor Cyan
    & $Action
}

function Resolve-GodotExecutable {
    if ($env:GODOT_BIN) {
        if (Test-Path $env:GODOT_BIN) {
            return (Resolve-Path $env:GODOT_BIN).Path
        }

        throw "GODOT_BIN is set but the file does not exist: $env:GODOT_BIN"
    }

    $candidates = @(
        "godot4-mono",
        "godot4",
        "godot",
        "Godot_v4.6.1-stable_mono_win64_console",
        "Godot_v4.6.1-stable_mono_win64"
    )

    foreach ($candidate in $candidates) {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    # Fall back to the usual places a downloaded Godot .NET build ends up.
    $searchRoots = @(
        (Join-Path $env:USERPROFILE "Downloads"),
        (Join-Path $env:USERPROFILE "Desktop"),
        $env:ProgramFiles
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $searchRoots) {
        $found = Get-ChildItem -Path $root -Filter "Godot_v4.6*mono*win64*.exe" -Recurse -Depth 3 -ErrorAction SilentlyContinue |
            Sort-Object { $_.Name -like "*console*" }, FullName |
            Select-Object -First 1
        if ($found) {
            return $found.FullName
        }
    }

    return $null
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

Invoke-Step "Building presentation project" {
    dotnet build LandBuilder.Presentation.csproj
}

if (-not $SkipTests) {
    Invoke-Step "Running test suite" {
        dotnet test LandBuilder.sln
    }
}

$godotExe = Resolve-GodotExecutable
if (-not $godotExe) {
    Write-Host ""
    Write-Warning "Build and tests succeeded, but no Godot executable was found."
    Write-Host "Set GODOT_BIN to your Godot 4 .NET executable, or add Godot to PATH." -ForegroundColor Yellow
    exit 0
}

$launchArgs = @("--path", $repoRoot)
if ($Editor) {
    $launchArgs += "--editor"
}

Invoke-Step "Launching Godot project" {
    Write-Host "Using: $godotExe" -ForegroundColor DarkGray
    Start-Process -FilePath $godotExe -ArgumentList $launchArgs -WorkingDirectory $repoRoot | Out-Null
}

Write-Host ""
Write-Host "Land-Builder launch requested." -ForegroundColor Green
