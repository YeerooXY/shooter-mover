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
    [string]$WindowsBuildRoot,

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
    [string]$SessionId = "session.eh009-playmode",

    [Parameter(ParameterSetName = "Run")]
    [string]$AttemptId = "attempt.eh009-playmode-1",

    [Parameter(Mandatory = $true, ParameterSetName = "ContractTest")]
    [switch]$ContractTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

function Invoke-EditorEngine {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $hostExecutable = (Get-Process -Id $PID).Path
    if ([string]::IsNullOrWhiteSpace($hostExecutable) -or
        -not (Test-Path -LiteralPath $hostExecutable -PathType Leaf)) {
        throw "Could not resolve the current PowerShell executable."
    }

    & $hostExecutable @Arguments
    return $LASTEXITCODE
}

try {
    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $enginePath = Join-Path $scriptDirectory "run_editmode_smoke.ps1"
    if (-not (Test-Path -LiteralPath $enginePath -PathType Leaf)) {
        [Console]::Error.WriteLine("Shared EH-009 editor smoke engine was not found at '$enginePath'.")
        exit 2
    }

    if ($ContractTest) {
        $contractExit = Invoke-EditorEngine -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $enginePath,
            "-ContractTest")
        if ($contractExit -ne 0) {
            exit $contractExit
        }

        $source = [System.IO.File]::ReadAllText($PSCommandPath)
        foreach ($requiredToken in @(
            "-InternalTestPlatform", "PlayMode",
            "-InternalEntrypointName", "playmode",
            "EvidenceEntrypointSmokeTests.EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap")) {
            if (-not $source.Contains($requiredToken)) {
                [Console]::Error.WriteLine("PlayMode wrapper contract is missing '$requiredToken'.")
                exit 7
            }
        }

        Write-Host "EH-009 PlayMode wrapper contract tests passed."
        exit 0
    }

    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $enginePath,
        "-OutputDirectory", $OutputDirectory,
        "-BuildIdentityPath", $BuildIdentityPath,
        "-ContentVersionPath", $ContentVersionPath,
        "-ConfigurationPath", $ConfigurationPath,
        "-PythonPath", $PythonPath,
        "-DirtyStatePolicy", $DirtyStatePolicy,
        "-TuningProfileId", $TuningProfileId,
        "-SessionId", $SessionId,
        "-AttemptId", $AttemptId,
        "-InternalTestPlatform", "PlayMode",
        "-InternalEntrypointName", "playmode",
        "-InternalTestFilter",
        "ShooterMover.Tests.PlayMode.EvidenceHarness.EvidenceEntrypointSmokeTests.EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap")

    if (-not [string]::IsNullOrWhiteSpace($WindowsBuildRoot)) {
        $arguments += @("-WindowsBuildRoot", $WindowsBuildRoot)
    }
    if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
        $arguments += @("-UnityPath", $UnityPath)
    }

    $exitCode = Invoke-EditorEngine -Arguments $arguments
    exit $exitCode
}
catch {
    [Console]::Error.WriteLine("EH-009 PlayMode smoke failed before the shared engine completed: $($_.Exception.Message)")
    exit 1
}
