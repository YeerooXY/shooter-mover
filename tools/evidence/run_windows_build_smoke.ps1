[CmdletBinding(DefaultParameterSetName = "Run")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Run")]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true, ParameterSetName = "Run")]
    [string]$BuildIdentityPath,

    [Parameter(Mandatory = $true, ParameterSetName = "Run")]
    [string]$ContentVersionPath,

    [Parameter(ParameterSetName = "Run")]
    [string]$ConfigurationPath = "tools/evidence/fixtures/stage1-evidence-config-v1.json",

    [Parameter(ParameterSetName = "Run")]
    [string]$UnityPath,

    [Parameter(ParameterSetName = "Run")]
    [string]$PythonPath = "python",

    [Parameter(ParameterSetName = "Run")]
    [ValidateSet("reject-dirty", "allow-dirty-development")]
    [string]$DirtyStatePolicy = "reject-dirty",

    [Parameter(ParameterSetName = "Run")]
    [string]$TuningProfileId = "movement-tuning.stage1-baseline",

    [Parameter(ParameterSetName = "Run")]
    [string]$SessionId = "session.eh009-windows-build",

    [Parameter(ParameterSetName = "Run")]
    [string]$AttemptId = "attempt.eh009-windows-build-1",

    [Parameter(Mandatory = $true, ParameterSetName = "ContractTest")]
    [switch]$ContractTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function ConvertTo-ProcessArgument {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Argument)

    if ($Argument.Contains('"')) {
        throw "Arguments containing a double quote are not supported: '$Argument'."
    }
    if ($Argument.Length -eq 0 -or $Argument -match '\s') {
        return '"' + $Argument + '"'
    }
    return $Argument
}

function Invoke-PowerShellChild {
    param(
        [Parameter(Mandatory = $true)][string]$PowerShellExecutable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter()][string]$LogPath
    )

    $argumentLine = ($Arguments | ForEach-Object {
        ConvertTo-ProcessArgument -Argument $_
    }) -join " "

    $process = $null
    try {
        if ([string]::IsNullOrWhiteSpace($LogPath)) {
            $process = Start-Process `
                -FilePath $PowerShellExecutable `
                -ArgumentList $argumentLine `
                -PassThru `
                -Wait `
                -NoNewWindow
        }
        else {
            $stdoutPath = $LogPath + ".stdout.tmp"
            $stderrPath = $LogPath + ".stderr.tmp"
            Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
            $process = Start-Process `
                -FilePath $PowerShellExecutable `
                -ArgumentList $argumentLine `
                -PassThru `
                -Wait `
                -NoNewWindow `
                -RedirectStandardOutput $stdoutPath `
                -RedirectStandardError $stderrPath

            $text = ""
            if (Test-Path -LiteralPath $stdoutPath -PathType Leaf) {
                $text += [System.IO.File]::ReadAllText($stdoutPath)
            }
            if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
                if ($text.Length -gt 0) {
                    $text += "`n"
                }
                $text += [System.IO.File]::ReadAllText($stderrPath)
            }
            [System.IO.File]::WriteAllText($LogPath, $text, $script:Utf8NoBom)
            Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
        }
        return $process.ExitCode
    }
    finally {
        if ($null -ne $process) {
            $process.Dispose()
        }
    }
}

function Assert-Uf010BuildContract {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $buildSettingsPath = Join-Path $RepositoryRoot "ProjectSettings\EditorBuildSettings.asset"
    if (-not (Test-Path -LiteralPath $buildSettingsPath -PathType Leaf)) {
        throw "UF-010 EditorBuildSettings.asset is missing."
    }
    $text = [System.IO.File]::ReadAllText($buildSettingsPath)
    $enabledCount = [regex]::Matches(
        $text,
        '(?m)^\s*-?\s*enabled:\s*1\s*$').Count
    if ($enabledCount -ne 1) {
        throw "UF-010 must expose exactly one enabled build scene; found $enabledCount."
    }
    if (-not $text.Contains("path: Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity")) {
        throw "UF-010 enabled build scene is not the accepted Bootstrap shell."
    }
}

function Get-Uf010OutputRoot {
    $localApplicationData = [System.Environment]::GetFolderPath(
        [System.Environment+SpecialFolder]::LocalApplicationData)
    if ([string]::IsNullOrWhiteSpace($localApplicationData)) {
        throw "Windows LocalApplicationData could not be resolved."
    }
    return [System.IO.Path]::GetFullPath(
        (Join-Path $localApplicationData "ShooterMover\Builds\WindowsDevelopment"))
}

function Write-SanitizedLog {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$SensitiveRoots
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Player did not write its requested log at '$Path'."
    }
    $text = [System.IO.File]::ReadAllText($Path)
    for ($index = 0; $index -lt $SensitiveRoots.Length; $index++) {
        $root = $SensitiveRoots[$index]
        if (-not [string]::IsNullOrWhiteSpace($root)) {
            $text = [regex]::Replace(
                $text,
                [regex]::Escape($root),
                "<local-root-$index>",
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
    }
    [System.IO.File]::WriteAllText($Path, $text, $script:Utf8NoBom)
}

function Invoke-PlayerPass {
    param(
        [Parameter(Mandatory = $true)][string]$PlayerPath,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][int]$StartupTimeoutSeconds,
        [Parameter(Mandatory = $true)][int]$ShutdownTimeoutSeconds,
        [Parameter(Mandatory = $true)][string[]]$SensitiveRoots,
        [Parameter(Mandatory = $true)][int]$PassNumber
    )

    Remove-Item -LiteralPath $LogPath -Force -ErrorAction SilentlyContinue
    $arguments = @("-logFile", $LogPath)
    $argumentLine = ($arguments | ForEach-Object {
        ConvertTo-ProcessArgument -Argument $_
    }) -join " "

    $process = $null
    try {
        $process = Start-Process -FilePath $PlayerPath -ArgumentList $argumentLine -PassThru
        $startupStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $closeRequested = $false
        try {
            while ($startupStopwatch.Elapsed.TotalSeconds -lt $StartupTimeoutSeconds) {
                Start-Sleep -Milliseconds 100
                $process.Refresh()
                if ($process.HasExited) {
                    $earlyExit = $process.ExitCode
                    if ($earlyExit -ne 0) {
                        throw "[EH009-CHILD:$earlyExit] Windows player pass $PassNumber exited during startup."
                    }
                    throw "Windows player pass $PassNumber exited before startup verification."
                }

                if ($process.MainWindowHandle -ne [System.IntPtr]::Zero -and $process.CloseMainWindow()) {
                    $closeRequested = $true
                    break
                }
            }
        }
        finally {
            $startupStopwatch.Stop()
        }
        if (-not $closeRequested) {
            throw "Windows player pass $PassNumber did not expose a closeable main window within $StartupTimeoutSeconds seconds."
        }
        if (-not $process.WaitForExit($ShutdownTimeoutSeconds * 1000)) {
            try {
                $process.Kill()
            }
            catch {
                # Best effort only; the technical failure below remains authoritative.
            }
            throw "Windows player pass $PassNumber did not exit cleanly within $ShutdownTimeoutSeconds seconds."
        }
        if ($process.ExitCode -ne 0) {
            throw "[EH009-CHILD:$($process.ExitCode)] Windows player pass $PassNumber returned a nonzero exit code."
        }
    }
    finally {
        if ($null -ne $process) {
            try {
                $process.Refresh()
                if (-not $process.HasExited) {
                    if ($process.MainWindowHandle -ne [System.IntPtr]::Zero) {
                        [void]$process.CloseMainWindow()
                    }
                    if (-not $process.WaitForExit(1000)) {
                        $process.Kill()
                        [void]$process.WaitForExit(5000)
                    }
                }
            }
            catch {
                # Cleanup is best effort; preserve the original smoke failure.
            }
            finally {
                $process.Dispose()
            }
        }
    }

    Write-SanitizedLog -Path $LogPath -SensitiveRoots $SensitiveRoots
    $logText = [System.IO.File]::ReadAllText($LogPath)
    foreach ($failureSignature in @(
        "Crash!!!",
        "Unhandled Exception",
        "NullReferenceException",
        "Assertion failed")) {
        if ($logText.IndexOf($failureSignature, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Windows player pass $PassNumber log contains failure signature '$failureSignature'."
        }
    }
}

function Write-JsonNoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $json = ($Value | ConvertTo-Json -Depth 8) + "`n"
    [System.IO.File]::WriteAllText($Path, $json, $script:Utf8NoBom)
}

try {
    if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
        [Console]::Error.WriteLine("EH-009 Windows build smoke requires Windows.")
        exit 4
    }

    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory "..\.."))
    $enginePath = Join-Path $scriptDirectory "run_editmode_smoke.ps1"
    $buildScript = Join-Path $repositoryRoot "tools\build\Build-WindowsDevelopment.ps1"
    $powerShellExecutable = (Get-Process -Id $PID).Path

    foreach ($requiredScript in @($enginePath, $buildScript)) {
        if (-not (Test-Path -LiteralPath $requiredScript -PathType Leaf)) {
            [Console]::Error.WriteLine("Required script is missing: '$requiredScript'.")
            exit 2
        }
    }
    Assert-Uf010BuildContract -RepositoryRoot $repositoryRoot

    if ($ContractTest) {
        $contractExit = Invoke-PowerShellChild `
            -PowerShellExecutable $powerShellExecutable `
            -Arguments @(
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", $enginePath,
                "-ContractTest")
        if ($contractExit -ne 0) {
            exit $contractExit
        }

        $source = [System.IO.File]::ReadAllText($PSCommandPath)
        foreach ($token in @(
            "Build-WindowsDevelopment.ps1",
            "Assert-Uf010BuildContract",
            "Invoke-PlayerPass",
            "CloseMainWindow",
            "MainWindowHandle",
            "Stopwatch",
            "run_editmode_smoke.ps1")) {
            if (-not $source.Contains($token)) {
                [Console]::Error.WriteLine("Windows wrapper contract is missing '$token'.")
                exit 7
            }
        }
        Write-Host "EH-009 Windows wrapper contract tests passed."
        exit 0
    }

    $buildCommandLog = Join-Path ([System.IO.Path]::GetTempPath()) (
        "eh009-uf010-" + [guid]::NewGuid().ToString("N") + ".log")
    try {
        $buildArguments = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $buildScript)
        if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
            $buildArguments += @("-UnityPath", $UnityPath)
        }
        $buildExit = Invoke-PowerShellChild `
            -PowerShellExecutable $powerShellExecutable `
            -Arguments $buildArguments `
            -LogPath $buildCommandLog
        if ($buildExit -ne 0) {
            [Console]::Error.WriteLine("Accepted UF-010 build failed. See '$buildCommandLog'.")
            exit $buildExit
        }
    }
    finally {
        Remove-Item -LiteralPath $buildCommandLog -Force -ErrorAction SilentlyContinue
    }

    $buildRoot = Get-Uf010OutputRoot
    $playerPath = Join-Path $buildRoot "ShooterMover.exe"
    if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) {
        [Console]::Error.WriteLine("UF-010 returned success but '$playerPath' is missing.")
        exit 6
    }

    $configurationCandidate = if ([System.IO.Path]::IsPathRooted($ConfigurationPath)) {
        [System.IO.Path]::GetFullPath($ConfigurationPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ConfigurationPath))
    }
    if (-not (Test-Path -LiteralPath $configurationCandidate -PathType Leaf)) {
        [Console]::Error.WriteLine("EH-002 configuration was not found at '$configurationCandidate'.")
        exit 2
    }
    try {
        $configuration = [System.IO.File]::ReadAllText($configurationCandidate) | ConvertFrom-Json
        $shutdownTimeoutSeconds = [int]$configuration.timeouts.shutdownSeconds
        if ($shutdownTimeoutSeconds -lt 1) {
            throw "shutdownSeconds must be positive."
        }
    }
    catch {
        [Console]::Error.WriteLine("Could not read EH-002 shutdown timeout: $($_.Exception.Message)")
        exit 4
    }

    $proofRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
        "eh009-windows-proof-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $proofRoot -Force | Out-Null
    try {
        $sensitiveRoots = @(
            $repositoryRoot,
            $buildRoot,
            [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile),
            [System.IO.Path]::GetTempPath().TrimEnd('\'))
        $firstLog = Join-Path $proofRoot "player-pass-1.log"
        $secondLog = Join-Path $proofRoot "player-pass-2.log"
        Invoke-PlayerPass `
            -PlayerPath $playerPath `
            -LogPath $firstLog `
            -StartupTimeoutSeconds $shutdownTimeoutSeconds `
            -ShutdownTimeoutSeconds $shutdownTimeoutSeconds `
            -SensitiveRoots $sensitiveRoots `
            -PassNumber 1
        Invoke-PlayerPass `
            -PlayerPath $playerPath `
            -LogPath $secondLog `
            -StartupTimeoutSeconds $shutdownTimeoutSeconds `
            -ShutdownTimeoutSeconds $shutdownTimeoutSeconds `
            -SensitiveRoots $sensitiveRoots `
            -PassNumber 2

        Write-JsonNoBom -Path (Join-Path $proofRoot "windows-smoke.json") -Value ([ordered]@{
            schema = "shooter-mover.eh009-windows-smoke"
            version = 1
            buildContract = "UF-010"
            enabledBuildScene = "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity"
            startupPasses = 2
            harnessShellLoadVerifiedBy = "EvidenceEntrypointSmokeTests.EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap"
            restartVerified = $true
            gracefulCloseRequests = 2
            cleanExitCodes = @(0, 0)
            manifestRequired = $true
        })

        $engineArguments = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $enginePath,
            "-OutputDirectory", $OutputDirectory,
            "-BuildIdentityPath", $BuildIdentityPath,
            "-ContentVersionPath", $ContentVersionPath,
            "-ConfigurationPath", $ConfigurationPath,
            "-WindowsBuildRoot", $buildRoot,
            "-PythonPath", $PythonPath,
            "-DirtyStatePolicy", $DirtyStatePolicy,
            "-TuningProfileId", $TuningProfileId,
            "-SessionId", $SessionId,
            "-AttemptId", $AttemptId,
            "-InternalTestPlatform", "PlayMode",
            "-InternalEntrypointName", "windows-build",
            "-InternalTestFilter",
            "ShooterMover.Tests.PlayMode.EvidenceHarness.EvidenceEntrypointSmokeTests.EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap",
            "-AdditionalEvidenceDirectory", $proofRoot)
        if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
            $engineArguments += @("-UnityPath", $UnityPath)
        }

        $engineExit = Invoke-PowerShellChild `
            -PowerShellExecutable $powerShellExecutable `
            -Arguments $engineArguments
        exit $engineExit
    }
    finally {
        Remove-Item -LiteralPath $proofRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
catch {
    $message = $_.Exception.Message
    if ($message -match '^\[EH009-CHILD:([0-9]+)\]\s*(.*)$') {
        [Console]::Error.WriteLine($Matches[2])
        exit ([int]$Matches[1])
    }
    [Console]::Error.WriteLine("EH-009 Windows build smoke failed: $message")
    exit 1
}
