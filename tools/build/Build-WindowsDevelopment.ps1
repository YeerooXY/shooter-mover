[CmdletBinding()]
param(
    [Parameter()]
    [string]$UnityPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

function ConvertTo-ProcessArgument {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Argument)

    if ($Argument.Contains('"')) {
        throw "Process arguments containing a double quote are not supported: '$Argument'."
    }

    if ($Argument.Length -eq 0 -or $Argument -match '\s') {
        return '"' + $Argument + '"'
    }

    return $Argument
}

function Get-GitContentSha256 {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$RepositoryPath
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "git"
    $startInfo.Arguments = "show --no-textconv `"HEAD:$RepositoryPath`""
    $startInfo.WorkingDirectory = $ProjectRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $content = New-Object System.IO.MemoryStream

    try {
        if (-not $process.Start()) {
            throw "Git did not start."
        }

        $process.StandardOutput.BaseStream.CopyTo($content)
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        if ($process.ExitCode -ne 0) {
            throw "git show failed with exit code $($process.ExitCode): $errorText"
        }

        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $sha256.ComputeHash($content.ToArray())
        }
        finally {
            $sha256.Dispose()
        }

        return [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    }
    catch {
        throw "Could not fingerprint canonical repository content for '$RepositoryPath'. Run this script from a Git checkout with git available on PATH. Details: $($_.Exception.Message)"
    }
    finally {
        $content.Dispose()
        $process.Dispose()
    }
}

function Clear-ReadOnlyAttribute {
    param([Parameter(Mandatory = $true)][System.IO.FileSystemInfo]$Item)

    if (($Item.Attributes -band [System.IO.FileAttributes]::ReadOnly) -ne 0) {
        $Item.Attributes = $Item.Attributes -bxor [System.IO.FileAttributes]::ReadOnly
    }
}

function Reset-OwnedOutputDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$OutputRoot,
        [Parameter(Mandatory = $true)][string]$ExpectedOutputRoot
    )

    if (-not [string]::Equals($OutputRoot, $ExpectedOutputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected output path '$OutputRoot'. The only owned output is '$ExpectedOutputRoot'."
    }

    if (-not (Test-Path -LiteralPath $OutputRoot)) {
        New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
        return
    }

    try {
        $rootItem = Get-Item -LiteralPath $OutputRoot -Force
        if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "The owned output path is a reparse point. Remove it manually before building."
        }

        $existingItems = @(Get-ChildItem -LiteralPath $OutputRoot -Force -Recurse)
        $reparsePoint = $existingItems |
            Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } |
            Select-Object -First 1

        if ($null -ne $reparsePoint) {
            throw "The owned output contains a reparse point at '$($reparsePoint.FullName)'. Remove it manually before building."
        }

        foreach ($item in $existingItems) {
            Clear-ReadOnlyAttribute -Item $item
        }
        Clear-ReadOnlyAttribute -Item $rootItem

        Remove-Item -LiteralPath $OutputRoot -Recurse -Force
        New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
    }
    catch {
        throw "Could not clear stale build output at '$OutputRoot'. Close any running ShooterMover player or Explorer window, ensure the files are writable, and retry. No path outside the owned output was touched. Details: $($_.Exception.Message)"
    }
}

function Get-ProjectEditorVersion {
    param([Parameter(Mandatory = $true)][string]$ProjectVersionPath)

    $editorVersion = $null
    $editorRevision = $null

    foreach ($line in Get-Content -LiteralPath $ProjectVersionPath) {
        if ($line -match '^m_EditorVersion:\s*(.+)$') {
            $editorVersion = $Matches[1].Trim()
        }
        elseif ($line -match '^m_EditorVersionWithRevision:\s*(.+)$') {
            $editorRevision = $Matches[1].Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($editorVersion) -or [string]::IsNullOrWhiteSpace($editorRevision)) {
        throw "ProjectSettings/ProjectVersion.txt does not contain both pinned editor fields."
    }

    return [ordered]@{
        Version = $editorVersion
        Revision = $editorRevision
    }
}

function Resolve-UnityExecutable {
    param(
        [Parameter()][string]$RequestedPath,
        [Parameter(Mandatory = $true)][string]$PinnedVersion
    )

    $resolvedPath = $RequestedPath

    if ([string]::IsNullOrWhiteSpace($resolvedPath)) {
        if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR_PATH)) {
            $resolvedPath = $env:UNITY_EDITOR_PATH
        }
        else {
            $resolvedPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$PinnedVersion\Editor\Unity.exe"
        }
    }

    $resolvedPath = Get-FullPath -Path $resolvedPath
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "Unity Editor was not found at '$resolvedPath'. Pass -UnityPath or set UNITY_EDITOR_PATH to the pinned editor executable."
    }

    return $resolvedPath
}

function Show-LogTail {
    param([Parameter(Mandatory = $true)][string]$LogPath)

    if (Test-Path -LiteralPath $LogPath -PathType Leaf) {
        Write-Host "---- Unity log tail ----"
        Get-Content -LiteralPath $LogPath -Tail 80 | ForEach-Object { Write-Host $_ }
        Write-Host "---- end Unity log tail ----"
    }
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Get-FullPath -Path (Join-Path $scriptDirectory "..\..")
$tempRoot = Get-FullPath -Path (Join-Path $projectRoot "Temp")
$ownedOutputRoot = Get-FullPath -Path (Join-Path $tempRoot "ShooterMoverBuild")
$expectedOutputRoot = Get-FullPath -Path (Join-Path $projectRoot "Temp\ShooterMoverBuild")
$pathTrimCharacters = [char[]]@(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar
)

$tempPrefix = $tempRoot.TrimEnd($pathTrimCharacters) + [System.IO.Path]::DirectorySeparatorChar
if (-not $ownedOutputRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Owned output must remain under the project Temp directory."
}

$projectVersionPath = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
$packageLockPath = Join-Path $projectRoot "Packages\packages-lock.json"
$buildSettingsPath = Join-Path $projectRoot "ProjectSettings\EditorBuildSettings.asset"

foreach ($requiredInput in @($projectVersionPath, $packageLockPath, $buildSettingsPath)) {
    if (-not (Test-Path -LiteralPath $requiredInput -PathType Leaf)) {
        throw "Required build input is missing: '$requiredInput'."
    }
}

$editor = Get-ProjectEditorVersion -ProjectVersionPath $projectVersionPath
$unityExecutable = Resolve-UnityExecutable -RequestedPath $UnityPath -PinnedVersion $editor.Version

Reset-OwnedOutputDirectory -OutputRoot $ownedOutputRoot -ExpectedOutputRoot $expectedOutputRoot

$playerPath = Join-Path $ownedOutputRoot "ShooterMover.exe"
$logPath = Join-Path $ownedOutputRoot "unity-build.log"
$fingerprintPath = Join-Path $ownedOutputRoot "build-fingerprints.json"
$artifactListPath = Join-Path $ownedOutputRoot "build-artifacts.txt"

$fingerprints = [ordered]@{
    schema_version = 1
    target = "StandaloneWindows64"
    configuration = "Development"
    output = "Temp/ShooterMoverBuild/ShooterMover.exe"
    editor = [ordered]@{
        project_version = $editor.Version
        project_version_with_revision = $editor.Revision
        project_version_worktree_sha256 = (Get-FileHash -LiteralPath $projectVersionPath -Algorithm SHA256).Hash.ToLowerInvariant()
        unity_executable_sha256 = (Get-FileHash -LiteralPath $unityExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    packages = [ordered]@{
        package_lock_repository_sha256 = Get-GitContentSha256 -ProjectRoot $projectRoot -RepositoryPath "Packages/packages-lock.json"
        package_lock_worktree_sha256 = (Get-FileHash -LiteralPath $packageLockPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$fingerprints | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $fingerprintPath -Encoding UTF8

$unityArguments = @(
    "-batchmode",
    "-quit",
    "-projectPath", $projectRoot,
    "-buildTarget", "win64",
    "-standaloneBuildSubtarget", "Player",
    "-development",
    "-buildWindows64Player", $playerPath,
    "-logFile", $logPath
)
$unityArgumentLine = ($unityArguments | ForEach-Object {
    ConvertTo-ProcessArgument -Argument $_
}) -join " "

Write-Host "Building ShooterMover Windows x86-64 Development player"
Write-Host "Unity:  $unityExecutable"
Write-Host "Project: $projectRoot"
Write-Host "Output:  $ownedOutputRoot"

$unityProcess = $null
try {
    $unityProcess = Start-Process -FilePath $unityExecutable -ArgumentList $unityArgumentLine -PassThru
    Wait-Process -Id $unityProcess.Id
    $unityProcess.Refresh()
    $unityExitCode = $unityProcess.ExitCode
}
finally {
    if ($null -ne $unityProcess) {
        $unityProcess.Dispose()
    }
}

if ($unityExitCode -ne 0) {
    Show-LogTail -LogPath $logPath
    [Console]::Error.WriteLine("Unity compile/build failed with exit code $unityExitCode. Full log: '$logPath'.")
    exit $unityExitCode
}

$requiredArtifacts = @(
    @{ Path = $playerPath; Type = "Leaf" },
    @{ Path = (Join-Path $ownedOutputRoot "ShooterMover_Data"); Type = "Container" },
    @{ Path = (Join-Path $ownedOutputRoot "ShooterMover_Data\boot.config"); Type = "Leaf" },
    @{ Path = (Join-Path $ownedOutputRoot "ShooterMover_Data\globalgamemanagers"); Type = "Leaf" },
    @{ Path = (Join-Path $ownedOutputRoot "UnityPlayer.dll"); Type = "Leaf" },
    @{ Path = $logPath; Type = "Leaf" },
    @{ Path = $fingerprintPath; Type = "Leaf" }
)

$missingArtifacts = @()
foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath $artifact.Path -PathType $artifact.Type)) {
        $missingArtifacts += $artifact.Path
    }
}

if ($missingArtifacts.Count -gt 0) {
    Show-LogTail -LogPath $logPath
    throw "Unity returned success but expected artifacts are missing: $($missingArtifacts -join ', ')."
}

New-Item -ItemType File -Path $artifactListPath -Force | Out-Null
$outputPrefix = $ownedOutputRoot.TrimEnd($pathTrimCharacters) + [System.IO.Path]::DirectorySeparatorChar
$artifactLines = Get-ChildItem -LiteralPath $ownedOutputRoot -File -Recurse |
    ForEach-Object {
        $_.FullName.Substring($outputPrefix.Length) -replace '\\', '/'
    } |
    Sort-Object

$artifactLines | Set-Content -LiteralPath $artifactListPath -Encoding UTF8

if (-not (Test-Path -LiteralPath $artifactListPath -PathType Leaf)) {
    throw "Artifact inventory was not written to '$artifactListPath'."
}

Write-Host "Build succeeded."
Write-Host "Player:       $playerPath"
Write-Host "Fingerprints: $fingerprintPath"
Write-Host "Artifact list: $artifactListPath"
Write-Host "Unity log:    $logPath"