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
    [string]$SessionId = "session.eh009-editmode",

    [Parameter(ParameterSetName = "Run")]
    [string]$AttemptId = "attempt.eh009-editmode-1",

    [Parameter(ParameterSetName = "Run")]
    [ValidateSet("EditMode", "PlayMode")]
    [string]$InternalTestPlatform = "EditMode",

    [Parameter(ParameterSetName = "Run")]
    [string]$InternalEntrypointName = "editmode",

    [Parameter(ParameterSetName = "Run")]
    [string]$InternalTestFilter = "ShooterMover.Tests.EditMode",

    [Parameter(ParameterSetName = "Run")]
    [string]$AdditionalEvidenceDirectory,

    [Parameter(Mandatory = $true, ParameterSetName = "ContractTest")]
    [switch]$ContractTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Throw-Eh009 {
    param(
        [Parameter(Mandatory = $true)][int]$Code,
        [Parameter(Mandatory = $true)][string]$Message
    )

    throw "[EH009:$Code] $Message"
}

function Throw-ChildExit {
    param(
        [Parameter(Mandatory = $true)][int]$Code,
        [Parameter(Mandatory = $true)][string]$Message
    )

    throw "[EH009-CHILD:$Code] $Message"
}

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

function Test-PathInsideRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $separator = [System.IO.Path]::DirectorySeparatorChar
    $rootWithSeparator = $Root.TrimEnd($separator, [System.IO.Path]::AltDirectorySeparatorChar) + $separator
    return [string]::Equals($Candidate, $Root, [System.StringComparison]::OrdinalIgnoreCase) -or
        $Candidate.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-RepositoryFile {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        Throw-Eh009 -Code 2 -Message "$Label path is required."
    }

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        Get-FullPath -Path $Path
    }
    else {
        Get-FullPath -Path (Join-Path $RepositoryRoot $Path)
    }

    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        Throw-Eh009 -Code 2 -Message "$Label was not found at '$candidate'. Relative paths are resolved from the repository root."
    }

    return $candidate
}

function Assert-NoReparsePoints {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $rootItem = Get-Item -LiteralPath $Path -Force
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        Throw-Eh009 -Code 3 -Message "$Label root is a reparse point: '$Path'."
    }

    $reparsePoint = Get-ChildItem -LiteralPath $Path -Force -Recurse |
        Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } |
        Select-Object -First 1
    if ($null -ne $reparsePoint) {
        Throw-Eh009 -Code 3 -Message "$Label contains a reparse point at '$($reparsePoint.FullName)'."
    }
}

function Assert-FreshOutputDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    if (Test-PathInsideRoot -Candidate $Path -Root $RepositoryRoot) {
        Throw-Eh009 -Code 3 -Message "Output must be outside the repository: '$Path'."
    }

    if (Test-Path -LiteralPath $Path) {
        Assert-NoReparsePoints -Path $Path -Label "Output"
        $existing = Get-ChildItem -LiteralPath $Path -Force | Select-Object -First 1
        if ($null -ne $existing) {
            Throw-Eh009 -Code 3 -Message "Stale output rejected at '$Path'. Select a missing or empty directory."
        }
    }
    else {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function ConvertTo-ProcessArgument {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Argument)

    if ($Argument.Contains('"')) {
        Throw-Eh009 -Code 2 -Message "Process arguments containing a double quote are not supported: '$Argument'."
    }

    if ($Argument.Length -eq 0 -or $Argument -match '\s') {
        return '"' + $Argument + '"'
    }

    return $Argument
}

function Invoke-Child {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    $logParent = Split-Path -Parent $LogPath
    if (-not [string]::IsNullOrWhiteSpace($logParent)) {
        New-Item -ItemType Directory -Path $logParent -Force | Out-Null
    }

    $stdoutPath = $LogPath + ".stdout.tmp"
    $stderrPath = $LogPath + ".stderr.tmp"
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue

    try {
        & $FilePath @Arguments 1> $stdoutPath 2> $stderrPath
        $childExit = $LASTEXITCODE

        $builder = New-Object System.Text.StringBuilder
        if (Test-Path -LiteralPath $stdoutPath -PathType Leaf) {
            [void]$builder.Append([System.IO.File]::ReadAllText($stdoutPath))
        }
        if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
            if ($builder.Length -gt 0) {
                [void]$builder.Append("`n")
            }
            [void]$builder.Append([System.IO.File]::ReadAllText($stderrPath))
        }
        [System.IO.File]::WriteAllText($LogPath, $builder.ToString(), $script:Utf8NoBom)
        return $childExit
    }
    catch {
        Throw-Eh009 -Code 5 -Message "Could not start child process '$FilePath'. Details: $($_.Exception.Message)"
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-Executable {
    param(
        [Parameter(Mandatory = $true)][string]$Requested,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ([System.IO.Path]::IsPathRooted($Requested)) {
        $full = Get-FullPath -Path $Requested
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            Throw-Eh009 -Code 2 -Message "$Label executable was not found at '$full'."
        }
        return $full
    }

    $command = Get-Command -Name $Requested -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $command) {
        Throw-Eh009 -Code 2 -Message "$Label executable '$Requested' was not found on PATH."
    }
    return $command.Source
}

function Get-PinnedUnityVersion {
    param([Parameter(Mandatory = $true)][string]$ProjectVersionPath)

    $version = $null
    foreach ($line in Get-Content -LiteralPath $ProjectVersionPath) {
        if ($line -match '^m_EditorVersion:\s*(.+)$') {
            $version = $Matches[1].Trim()
        }
    }
    if ([string]::IsNullOrWhiteSpace($version)) {
        Throw-Eh009 -Code 4 -Message "Pinned Unity version is missing from ProjectSettings/ProjectVersion.txt."
    }
    return $version
}

function Resolve-UnityExecutable {
    param(
        [Parameter()][string]$RequestedPath,
        [Parameter(Mandatory = $true)][string]$PinnedVersion
    )

    $candidate = $RequestedPath
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR_PATH)) {
            $candidate = $env:UNITY_EDITOR_PATH
        }
        else {
            $candidate = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$PinnedVersion\Editor\Unity.exe"
        }
    }

    $candidate = Get-FullPath -Path $candidate
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        Throw-Eh009 -Code 4 -Message "Unity Editor was not found at '$candidate'. Pass -UnityPath or set UNITY_EDITOR_PATH."
    }
    return $candidate
}

function Resolve-WindowsBuildRoot {
    param([Parameter()][string]$RequestedPath)

    $candidate = $RequestedPath
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $localApplicationData = [System.Environment]::GetFolderPath(
            [System.Environment+SpecialFolder]::LocalApplicationData)
        if ([string]::IsNullOrWhiteSpace($localApplicationData)) {
            Throw-Eh009 -Code 4 -Message "Windows LocalApplicationData could not be resolved."
        }
        $candidate = Join-Path $localApplicationData "ShooterMover\Builds\WindowsDevelopment"
    }

    $candidate = Get-FullPath -Path $candidate
    if (-not (Test-Path -LiteralPath $candidate -PathType Container)) {
        Throw-Eh009 -Code 4 -Message "Accepted UF-010 Windows build root was not found at '$candidate'."
    }
    Assert-NoReparsePoints -Path $candidate -Label "Windows build"

    foreach ($relative in @(
        "ShooterMover.exe",
        "ShooterMover_Data\boot.config",
        "ShooterMover_Data\globalgamemanagers",
        "UnityPlayer.dll",
        "unity-build.log",
        "build-fingerprints.json",
        "build-artifacts.txt")) {
        $required = Join-Path $candidate $relative
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            Throw-Eh009 -Code 4 -Message "UF-010 build is incomplete; missing '$required'."
        }
    }

    return $candidate
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Text, $script:Utf8NoBom)
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 12
    Write-Utf8NoBom -Path $Path -Text ($json + "`n")
}

function Resolve-BuildIdentity {
    param(
        [Parameter(Mandatory = $true)][string]$InputPath,
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $text = [System.IO.File]::ReadAllText($InputPath)
    if ($text.Length -eq 0 -or $text[0] -eq [char]0xfeff -or $text.Contains("`r") -or $text.EndsWith("`n")) {
        Throw-Eh009 -Code 4 -Message "BuildIdentity must be strict UTF-8/LF canonical text with no BOM or trailing newline."
    }

    $lines = $text.Split([char]10)
    $expected = @(
        "identity_kind",
        "source_state",
        "source_commit",
        "unity_version",
        "package_lock_fingerprint",
        "content_fingerprint",
        "save_schema",
        "artifact_checksum")
    if ($lines.Length -ne $expected.Length) {
        Throw-Eh009 -Code 4 -Message "BuildIdentity must contain exactly eight canonical fields."
    }
    for ($index = 0; $index -lt $expected.Length; $index++) {
        if (-not $lines[$index].StartsWith($expected[$index] + "=", [System.StringComparison]::Ordinal)) {
            Throw-Eh009 -Code 4 -Message "BuildIdentity field $($expected[$index]) is missing or reordered."
        }
    }

    $artifactChecksum = "sha256:" + (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $current = $lines[7].Substring("artifact_checksum=".Length)
    if ($current -ne "null" -and $current -ne $artifactChecksum) {
        Throw-Eh009 -Code 4 -Message "BuildIdentity artifact checksum does not match the supplied UF-010 executable."
    }
    $lines[7] = "artifact_checksum=" + $artifactChecksum
    Write-Utf8NoBom -Path $OutputPath -Text ($lines -join "`n")
}

function Capture-EvidenceIdentity {
    param(
        [Parameter(Mandatory = $true)][string]$PythonExecutable,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$BuildIdentity,
        [Parameter(Mandatory = $true)][string]$ContentVersion,
        [Parameter(Mandatory = $true)][string]$Policy,
        [Parameter(Mandatory = $true)][string]$TuningId,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$CommandLog
    )

    $tool = Join-Path $RepositoryRoot "tools\evidence\capture_build_identity.py"
    $arguments = @(
        $tool,
        "--build-identity", $BuildIdentity,
        "--content-version", $ContentVersion,
        "--project-version", (Join-Path $RepositoryRoot "ProjectSettings\ProjectVersion.txt"),
        "--package-lock", (Join-Path $RepositoryRoot "Packages\packages-lock.json"),
        "--dirty-state-policy", $Policy,
        "--build-target", "StandaloneWindows64",
        "--build-configuration", "Development",
        "--tuning-profile-id", $TuningId,
        "--output", $OutputPath)

    $exitCode = Invoke-Child -FilePath $PythonExecutable -Arguments $arguments -LogPath $CommandLog
    if ($exitCode -ne 0) {
        Throw-ChildExit -Code $exitCode -Message "EH-001 identity capture failed. See '$CommandLog'."
    }

    $identityText = [System.IO.File]::ReadAllText($OutputPath)
    $match = [regex]::Match($identityText, '(?m)^record_fingerprint=(sha256:[0-9a-f]{64})$')
    if (-not $match.Success) {
        Throw-Eh009 -Code 4 -Message "Captured EH-001 identity has no canonical record_fingerprint."
    }
    return $match.Groups[1].Value
}

function Write-ResolvedConfiguration {
    param(
        [Parameter(Mandatory = $true)][string]$InputPath,
        [Parameter(Mandatory = $true)][string]$IdentityFingerprint,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $text = [System.IO.File]::ReadAllText($InputPath)
    if ($text.Length -eq 0 -or $text[0] -eq [char]0xfeff -or $text.Contains("`r") -or -not $text.EndsWith("`n")) {
        Throw-Eh009 -Code 4 -Message "EH-002 configuration must be canonical UTF-8/LF text with one trailing newline."
    }

    $pattern = '(?m)^  "identityReference": "sha256:[0-9a-f]{64}",$'
    if ([regex]::Matches($text, $pattern).Count -ne 1) {
        Throw-Eh009 -Code 4 -Message "EH-002 configuration must contain exactly one canonical identityReference line."
    }
    $resolved = [regex]::Replace(
        $text,
        $pattern,
        '  "identityReference": "' + $IdentityFingerprint + '",')
    Write-Utf8NoBom -Path $OutputPath -Text $resolved

    try {
        return $resolved | ConvertFrom-Json
    }
    catch {
        Throw-Eh009 -Code 4 -Message "Resolved EH-002 configuration is not valid JSON. Details: $($_.Exception.Message)"
    }
}

function Sanitize-TextFile {
    param(
        [Parameter(Mandatory = $true)][string]$InputPath,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][hashtable]$Replacements
    )

    $text = [System.IO.File]::ReadAllText($InputPath)
    foreach ($key in $Replacements.Keys) {
        if (-not [string]::IsNullOrWhiteSpace([string]$key)) {
            $text = [regex]::Replace(
                $text,
                [regex]::Escape([string]$key),
                [string]$Replacements[$key],
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
    }
    Write-Utf8NoBom -Path $OutputPath -Text $text
}

function Assert-TestResultsPassed {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Throw-Eh009 -Code 6 -Message "Unity did not write test results to '$Path'."
    }
    try {
        [xml]$document = [System.IO.File]::ReadAllText($Path)
        $testRun = $document.'test-run'
        if ($null -eq $testRun) {
            Throw-Eh009 -Code 6 -Message "Unity test results do not contain a test-run root."
        }
        if ([int]$testRun.failed -ne 0 -or [string]$testRun.result -ne "Passed") {
            Throw-Eh009 -Code 20 -Message "Unity test results are not passing: result=$($testRun.result), failed=$($testRun.failed)."
        }
    }
    catch {
        if ($_.Exception.Message.StartsWith("[EH009:", [System.StringComparison]::Ordinal)) {
            throw
        }
        Throw-Eh009 -Code 6 -Message "Could not parse Unity test results. Details: $($_.Exception.Message)"
    }
}

function Invoke-ContractTests {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $projectVersion = Resolve-RepositoryFile `
        -RepositoryRoot $RepositoryRoot `
        -Path "ProjectSettings/ProjectVersion.txt" `
        -Label "Project version"
    if (-not (Test-PathInsideRoot -Candidate $projectVersion -Root $RepositoryRoot)) {
        throw "Repository-relative path resolution escaped the repository."
    }

    $temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("EH009 contract test " + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    try {
        Assert-FreshOutputDirectory -Path $temporaryRoot -RepositoryRoot $RepositoryRoot
        Write-Utf8NoBom -Path (Join-Path $temporaryRoot "stale.txt") -Text "stale"
        $rejected = $false
        try {
            Assert-FreshOutputDirectory -Path $temporaryRoot -RepositoryRoot $RepositoryRoot
        }
        catch {
            $rejected = $_.Exception.Message.Contains("Stale output rejected")
        }
        if (-not $rejected) {
            throw "Stale output was not rejected."
        }

        $hostExecutable = (Get-Process -Id $PID).Path
        $childLog = Join-Path ([System.IO.Path]::GetTempPath()) ("eh009-child-" + [guid]::NewGuid().ToString("N") + ".log")
        try {
            $childExit = Invoke-Child `
                -FilePath $hostExecutable `
                -Arguments @("-NoProfile", "-Command", "exit 23") `
                -LogPath $childLog
            if ($childExit -ne 23) {
                throw "Child exit propagation returned $childExit instead of 23."
            }
        }
        finally {
            Remove-Item -LiteralPath $childLog -Force -ErrorAction SilentlyContinue
        }

        $manifestTool = Join-Path $RepositoryRoot "tools\evidence\build_evidence_manifest.py"
        if (-not (Test-Path -LiteralPath $manifestTool -PathType Leaf)) {
            throw "EH-008 manifest tool was not found through the repository-relative path."
        }

        $forcedQuitToken = '"' + '-quit' + '"'
        if ([System.IO.File]::ReadAllText($PSCommandPath).Contains($forcedQuitToken)) {
            throw "Unity test invocation must not force -quit before test XML is written."
        }
    }
    finally {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "EH-009 editor entrypoint contract tests passed."
}

function Invoke-EditorSmoke {
    $scriptDirectory = Split-Path -Parent $MyInvocation.ScriptName
    if ([string]::IsNullOrWhiteSpace($scriptDirectory)) {
        $scriptDirectory = Split-Path -Parent $PSCommandPath
    }
    $repositoryRoot = Get-FullPath -Path (Join-Path $scriptDirectory "..\..")

    if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
        Throw-Eh009 -Code 4 -Message "EH-009 smoke entrypoints require Windows because every valid EH-008 manifest binds a UF-010 Windows build."
    }

    $outputRoot = Get-FullPath -Path $OutputDirectory
    Assert-FreshOutputDirectory -Path $outputRoot -RepositoryRoot $repositoryRoot

    $buildIdentityInput = Resolve-RepositoryFile -RepositoryRoot $repositoryRoot -Path $BuildIdentityPath -Label "BuildIdentity"
    $contentVersionInput = Resolve-RepositoryFile -RepositoryRoot $repositoryRoot -Path $ContentVersionPath -Label "ContentVersion"
    $configurationInput = Resolve-RepositoryFile -RepositoryRoot $repositoryRoot -Path $ConfigurationPath -Label "EH-002 configuration"
    $projectVersionPath = Resolve-RepositoryFile -RepositoryRoot $repositoryRoot -Path "ProjectSettings/ProjectVersion.txt" -Label "Project version"
    $pythonExecutable = Resolve-Executable -Requested $PythonPath -Label "Python"
    $pinnedVersion = Get-PinnedUnityVersion -ProjectVersionPath $projectVersionPath
    $unityExecutable = Resolve-UnityExecutable -RequestedPath $UnityPath -PinnedVersion $pinnedVersion
    $sourceBuildRoot = Resolve-WindowsBuildRoot -RequestedPath $WindowsBuildRoot

    $identityDirectory = Join-Path $outputRoot "identity"
    $configurationDirectory = Join-Path $outputRoot "configuration"
    $diagnosticsDirectory = Join-Path $outputRoot "diagnostics"
    $performanceDirectory = Join-Path $outputRoot "performance"
    $packagedBuildRoot = Join-Path $outputRoot "windows-build"
    foreach ($directory in @($identityDirectory, $configurationDirectory, $diagnosticsDirectory, $performanceDirectory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Copy-Item -LiteralPath $sourceBuildRoot -Destination $packagedBuildRoot -Recurse
    Assert-NoReparsePoints -Path $packagedBuildRoot -Label "Packaged Windows build"

    $packagedExecutable = Join-Path $packagedBuildRoot "ShooterMover.exe"
    $resolvedBuildIdentity = Join-Path $identityDirectory "build-identity.txt"
    $packagedContentVersion = Join-Path $identityDirectory "content-version.txt"
    $identityRecord = Join-Path $identityDirectory "evidence-identity.txt"
    Resolve-BuildIdentity `
        -InputPath $buildIdentityInput `
        -ExecutablePath $packagedExecutable `
        -OutputPath $resolvedBuildIdentity
    [System.IO.File]::WriteAllBytes($packagedContentVersion, [System.IO.File]::ReadAllBytes($contentVersionInput))

    $identityCommandLog = Join-Path ([System.IO.Path]::GetTempPath()) ("eh009-identity-" + [guid]::NewGuid().ToString("N") + ".log")
    try {
        $identityFingerprint = Capture-EvidenceIdentity `
            -PythonExecutable $pythonExecutable `
            -RepositoryRoot $repositoryRoot `
            -BuildIdentity $resolvedBuildIdentity `
            -ContentVersion $packagedContentVersion `
            -Policy $DirtyStatePolicy `
            -TuningId $TuningProfileId `
            -OutputPath $identityRecord `
            -CommandLog $identityCommandLog
    }
    finally {
        Remove-Item -LiteralPath $identityCommandLog -Force -ErrorAction SilentlyContinue
    }

    $resolvedConfiguration = Join-Path $configurationDirectory "stage1.json"
    $configuration = Write-ResolvedConfiguration `
        -InputPath $configurationInput `
        -IdentityFingerprint $identityFingerprint `
        -OutputPath $resolvedConfiguration

    $rawUnityLog = Join-Path $diagnosticsDirectory "unity.raw.log"
    $rawResults = Join-Path $diagnosticsDirectory "test-results.raw.xml"
    $commandLog = Join-Path ([System.IO.Path]::GetTempPath()) ("eh009-unity-command-" + [guid]::NewGuid().ToString("N") + ".log")
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Unity's test runner exits after it has written the requested XML. A forced
        # -quit can interrupt that shutdown and leave the evidence incomplete.
        $unityArguments = @(
            "-batchmode",
            "-projectPath", $repositoryRoot,
            "-runTests",
            "-testPlatform", $InternalTestPlatform,
            "-testFilter", $InternalTestFilter,
            "-testResults", $rawResults,
            "-logFile", $rawUnityLog)
        $unityExit = Invoke-Child -FilePath $unityExecutable -Arguments $unityArguments -LogPath $commandLog
    }
    finally {
        $stopwatch.Stop()
        Remove-Item -LiteralPath $commandLog -Force -ErrorAction SilentlyContinue
    }

    if ($unityExit -ne 0) {
        if (Test-Path -LiteralPath $rawUnityLog -PathType Leaf) {
            Write-Host "---- bounded Unity log tail ----"
            Get-Content -LiteralPath $rawUnityLog -Tail 80 | ForEach-Object { Write-Host $_ }
            Write-Host "---- end Unity log tail ----"
        }
        Throw-ChildExit -Code $unityExit -Message "$InternalTestPlatform smoke tests failed."
    }
    Assert-TestResultsPassed -Path $rawResults

    $replacements = @{}
    $replacements[$repositoryRoot] = "<repository>"
    $replacements[$outputRoot] = "<output>"
    $replacements[$unityExecutable] = "<unity-editor>"
    $replacements[[System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile)] = "<user-profile>"
    $replacements[[System.IO.Path]::GetTempPath().TrimEnd('\')] = "<temp>"

    $unityLog = Join-Path $diagnosticsDirectory "unity.log"
    $testResults = Join-Path $diagnosticsDirectory "test-results.xml"
    Sanitize-TextFile -InputPath $rawUnityLog -OutputPath $unityLog -Replacements $replacements
    Sanitize-TextFile -InputPath $rawResults -OutputPath $testResults -Replacements $replacements
    Remove-Item -LiteralPath $rawUnityLog, $rawResults -Force

    if (-not [string]::IsNullOrWhiteSpace($AdditionalEvidenceDirectory)) {
        $additionalRoot = Get-FullPath -Path $AdditionalEvidenceDirectory
        if (-not (Test-Path -LiteralPath $additionalRoot -PathType Container)) {
            Throw-Eh009 -Code 4 -Message "Additional evidence directory was not found at '$additionalRoot'."
        }
        Assert-NoReparsePoints -Path $additionalRoot -Label "Additional evidence"
        Copy-Item -LiteralPath $additionalRoot -Destination (Join-Path $diagnosticsDirectory "windows") -Recurse
    }

    $diagnosticLogs = @(Get-ChildItem -LiteralPath $diagnosticsDirectory -Filter "*.log" -File -Recurse)
    $logBytes = [long](($diagnosticLogs | Measure-Object -Property Length -Sum).Sum)
    if ($null -eq $logBytes) {
        $logBytes = 0L
    }
    $retainedLogCount = $diagnosticLogs.Count
    if ($logBytes -gt [long]$configuration.diagnostics.maxLogBytes) {
        Throw-Eh009 -Code 21 -Message "Diagnostics exceed EH-002 maxLogBytes: $logBytes > $($configuration.diagnostics.maxLogBytes)."
    }
    if ($retainedLogCount -gt [int]$configuration.diagnostics.retainedLogCount) {
        Throw-Eh009 -Code 21 -Message "Diagnostics exceed EH-002 retainedLogCount: $retainedLogCount > $($configuration.diagnostics.retainedLogCount)."
    }

    $eventCount = if ($InternalEntrypointName -eq "windows-build") { 7 } else { 3 }
    $maximumPayloadBytes = 1024
    if ($eventCount -gt [int]$configuration.diagnostics.maxEventCount -or
        $maximumPayloadBytes -gt [int]$configuration.diagnostics.maxEventPayloadBytes) {
        Throw-Eh009 -Code 21 -Message "Entrypoint diagnostics exceed EH-002 event or payload bounds."
    }

    $diagnosticsSummaryPath = Join-Path $diagnosticsDirectory "summary.json"
    $diagnosticsSummary = [ordered]@{
        schema = "shooter-mover.eh009-entrypoint-diagnostics"
        version = 1
        entrypoint = $InternalEntrypointName
        platform = $InternalTestPlatform
        testFilter = $InternalTestFilter
        testResultsPath = "diagnostics/test-results.xml"
        eventCount = $eventCount
        maximumEventPayloadBytes = $maximumPayloadBytes
        logBytes = $logBytes
        retainedLogCount = $retainedLogCount
        truncated = $false
        validity = [ordered]@{
            status = "valid"
            reasonCodes = @()
        }
    }
    Write-JsonFile -Path $diagnosticsSummaryPath -Value $diagnosticsSummary

    $captureSeconds = [Math]::Max(0.001d, $stopwatch.Elapsed.TotalSeconds)
    $performanceSummaryPath = Join-Path $performanceDirectory "summary.json"
    $performanceSummary = [ordered]@{
        schema = "shooter-mover.eh009-entrypoint-observation"
        version = 1
        entrypoint = $InternalEntrypointName
        state = "Completed"
        warmUpSeconds = 0.0
        captureSeconds = $captureSeconds
        frameSampleCount = 1
        qualityProfile = [string]$configuration.qualityProfile
        measurement = "bounded wall-clock entrypoint observation; not a gameplay performance acceptance claim"
    }
    Write-JsonFile -Path $performanceSummaryPath -Value $performanceSummary

    $descriptorPath = Join-Path $outputRoot "evidence-package.json"
    $descriptor = [ordered]@{
        schema = "shooter-mover.evidence-package-descriptor"
        version = 1
        identityRecordPath = "identity/evidence-identity.txt"
        configurationPath = "configuration/stage1.json"
        session = [ordered]@{
            sessionId = $SessionId
            attemptId = $AttemptId
            parentSessionId = $null
            state = "Ended"
            validity = [ordered]@{
                status = "valid"
                reasonCodes = @()
            }
        }
        diagnostics = [ordered]@{
            summaryPath = "diagnostics/summary.json"
            eventCount = $eventCount
            maximumEventPayloadBytes = $maximumPayloadBytes
            logBytes = $logBytes
            retainedLogCount = $retainedLogCount
            truncated = $false
        }
        performance = [ordered]@{
            summaryPath = "performance/summary.json"
            state = "Completed"
            warmUpSeconds = 0.0
            captureSeconds = $captureSeconds
            frameSampleCount = 1
            qualityProfile = [string]$configuration.qualityProfile
        }
        build = [ordered]@{
            rootPath = "windows-build"
            status = "succeeded"
            complete = $true
            exitCode = 0
            fingerprintsPath = "windows-build/build-fingerprints.json"
            artifactInventoryPath = "windows-build/build-artifacts.txt"
            executablePath = "windows-build/ShooterMover.exe"
        }
    }
    Write-JsonFile -Path $descriptorPath -Value $descriptor

    $manifestTool = Join-Path $repositoryRoot "tools\evidence\build_evidence_manifest.py"
    $manifestLog = Join-Path ([System.IO.Path]::GetTempPath()) ("eh009-manifest-" + [guid]::NewGuid().ToString("N") + ".log")
    try {
        $buildExit = Invoke-Child `
            -FilePath $pythonExecutable `
            -Arguments @($manifestTool, "build", "--package-root", $outputRoot, "--descriptor", "evidence-package.json", "--require-valid") `
            -LogPath $manifestLog
        if ($buildExit -ne 0) {
            Throw-ChildExit -Code $buildExit -Message "EH-008 manifest creation failed."
        }
        $verifyExit = Invoke-Child `
            -FilePath $pythonExecutable `
            -Arguments @($manifestTool, "verify", "--package-root", $outputRoot, "--require-valid") `
            -LogPath $manifestLog
        if ($verifyExit -ne 0) {
            Throw-ChildExit -Code $verifyExit -Message "EH-008 manifest verification failed."
        }
    }
    finally {
        Remove-Item -LiteralPath $manifestLog -Force -ErrorAction SilentlyContinue
    }

    foreach ($requiredOutput in @(
        "evidence-manifest-v1.json",
        "evidence-manifest-v1.sha256")) {
        $requiredPath = Join-Path $outputRoot $requiredOutput
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            Throw-Eh009 -Code 6 -Message "Manifest command returned success but '$requiredPath' is missing."
        }
    }

    Write-Host "EH-009 $InternalEntrypointName smoke succeeded."
    Write-Host "Manifested output: $outputRoot"
}

try {
    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $repositoryRoot = Get-FullPath -Path (Join-Path $scriptDirectory "..\..")
    if ($ContractTest) {
        Invoke-ContractTests -RepositoryRoot $repositoryRoot
        exit 0
    }
    Invoke-EditorSmoke
    exit 0
}
catch {
    $message = $_.Exception.Message
    if ($message -match '^\[EH009-CHILD:([0-9]+)\]\s*(.*)$') {
        [Console]::Error.WriteLine($Matches[2])
        exit ([int]$Matches[1])
    }
    if ($message -match '^\[EH009:([0-9]+)\]\s*(.*)$') {
        [Console]::Error.WriteLine($Matches[2])
        exit ([int]$Matches[1])
    }
    [Console]::Error.WriteLine("Unexpected EH-009 editor smoke failure: $message")
    exit 1
}
