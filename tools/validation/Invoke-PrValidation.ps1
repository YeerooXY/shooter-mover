<#
.SYNOPSIS
Validates a Shooter Mover pull request in a disposable clone.

.DESCRIPTION
Fetches a GitHub pull-request head, merges it locally onto the current target
branch, then runs the repository's static validators and Unity Test Framework.
The supplied repository checkout is never modified: Unity imports, logs, and
test results are confined to a newly created clone beneath OutputDirectory.

By default the script runs the complete EditMode suite. Use -TaskId to also
compare the PR's changed paths with that task's allowed areas in the committed
backlog. Use -TestPlatform Both when the task owns PlayMode behavior.

The Unity test command intentionally does not pass -quit. Unity's test runner
must finish and write its result XML before it exits on its own.

.PARAMETER PullRequest
GitHub pull-request number to fetch through origin's pull/<number>/head ref.

.PARAMETER RepositoryPath
An existing Shooter Mover checkout with an origin remote. It is used only to
discover the remote URL; the script does not fetch, checkout, or write in it.

.PARAMETER TaskId
Optional Stage 1 task ID such as MT-002. When supplied, changed paths must fit
that task's committed allowed_areas, including inseparable Unity .meta files.

.PARAMETER TestPlatform
EditMode (default), PlayMode, or Both.

.PARAMETER TestFilter
Optional fully qualified NUnit fixture or test filter passed to Unity.

.PARAMETER UnityPath
Optional path to Unity.exe. If omitted, the script checks SHOOTERMOVER_UNITY_PATH,
UNITY_PATH, D:\<pinned-version>\Editor\Unity.exe, then the Unity Hub location.

.PARAMETER OutputDirectory
Directory for logs, XML results, the JSON summary, and the disposable clone.
It must be outside RepositoryPath. The default is a timestamped directory in
the operating system temporary folder.

.PARAMETER SkipUnity
Run only Git, scope, and Python static validation checks.

.PARAMETER KeepClone
Keep the disposable clone after a successful run for debugging. Failed runs
always retain it.

.EXAMPLE
./tools/validation/Invoke-PrValidation.ps1 -PullRequest 36 -TaskId MT-002 -TestFilter ShooterMover.Tests.EditMode.Movement.BaseLocomotionStepperTests

.EXAMPLE
./tools/validation/Invoke-PrValidation.ps1 -PullRequest 39 -TaskId UF-008 -TestPlatform Both -KeepClone
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 999999)]
    [int]$PullRequest,

    [ValidateNotNullOrEmpty()]
    [string]$RepositoryPath = (Join-Path $PSScriptRoot '..\..'),

    [ValidateNotNullOrEmpty()]
    [string]$BaseRef = 'main',

    [ValidatePattern('^[A-Za-z]+-[0-9]+$')]
    [string]$TaskId,

    [ValidateSet('EditMode', 'PlayMode', 'Both')]
    [string]$TestPlatform = 'EditMode',

    [string]$TestFilter,

    [string]$UnityPath,

    [string]$OutputDirectory,

    [switch]$SkipUnity,

    [switch]$KeepClone
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-ValidationStatus {
    param(
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][string]$Message
    )

    Write-Host ('[{0}] {1}' -f $Kind, $Message)
}

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Description
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Get-NativeOutput {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $output = @(& $Executable @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }

    return $output
}

function ConvertTo-NormalizedFullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return ([System.IO.Path]::GetFullPath($Path)).Replace('\', '/').TrimEnd('/')
}

function Test-IsChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$ChildPath,
        [Parameter(Mandatory = $true)][string]$ParentPath
    )

    $child = ConvertTo-NormalizedFullPath $ChildPath
    $parent = ConvertTo-NormalizedFullPath $ParentPath
    return $child.StartsWith($parent + '/', [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-ExpectedUnityVersion {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $versionPath = Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        throw "Unity ProjectVersion file is missing: $versionPath"
    }

    $contents = Get-Content -LiteralPath $versionPath -Raw
    $match = [System.Text.RegularExpressions.Regex]::Match(
        $contents,
        '(?m)^m_EditorVersion:\s*(?<version>[^\r\n]+)\s*$')
    if (-not $match.Success) {
        throw "Unable to read m_EditorVersion from $versionPath"
    }

    return $match.Groups['version'].Value
}

function Resolve-UnityExecutable {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [string]$RequestedPath
    )

    $expectedVersion = Get-ExpectedUnityVersion $ProjectPath
    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $candidates.Add($RequestedPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:SHOOTERMOVER_UNITY_PATH)) {
        $candidates.Add($env:SHOOTERMOVER_UNITY_PATH)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_PATH)) {
        $candidates.Add($env:UNITY_PATH)
    }

    $candidates.Add((Join-Path ('D:\' + $expectedVersion) 'Editor\Unity.exe'))
    $candidates.Add((Join-Path 'C:\Program Files\Unity\Hub\Editor' ($expectedVersion + '\Editor\Unity.exe')))

    foreach ($candidate in $candidates) {
        if ((-not [string]::IsNullOrWhiteSpace($candidate)) -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            $resolved = (Resolve-Path -LiteralPath $candidate).Path
            $productVersion = (Get-Item -LiteralPath $resolved).VersionInfo.ProductVersion
            if ((-not [string]::IsNullOrWhiteSpace($productVersion)) -and (-not $productVersion.StartsWith($expectedVersion, [System.StringComparison]::OrdinalIgnoreCase))) {
                throw "Unity executable '$resolved' is $productVersion, but the project requires $expectedVersion."
            }

            return $resolved
        }
    }

    throw "Unity $expectedVersion was not found. Pass -UnityPath or set SHOOTERMOVER_UNITY_PATH."
}

function Test-AllowedChange {
    param(
        [Parameter(Mandatory = $true)][string]$ChangedPath,
        [Parameter(Mandatory = $true)][string[]]$AllowedAreas
    )

    $normalizedChanged = $ChangedPath.Replace('\', '/').TrimStart('/')
    foreach ($allowedArea in $AllowedAreas) {
        $normalizedArea = $allowedArea.Replace('\', '/').TrimStart('/')
        $isDirectoryRule = $normalizedArea.EndsWith('/')
        $areaWithoutTrailingSlash = $normalizedArea.TrimEnd('/')

        if ($isDirectoryRule) {
            $isChildOfDirectory = $normalizedChanged.StartsWith(
                $areaWithoutTrailingSlash + '/',
                [System.StringComparison]::OrdinalIgnoreCase)
            $isDirectoryMeta = $normalizedChanged.Equals(
                $areaWithoutTrailingSlash + '.meta',
                [System.StringComparison]::OrdinalIgnoreCase)
            if ($isChildOfDirectory -or $isDirectoryMeta) {
                return $true
            }
        }
        else {
            $isExactFile = $normalizedChanged.Equals(
                $normalizedArea,
                [System.StringComparison]::OrdinalIgnoreCase)
            $isFileMeta = $normalizedChanged.Equals(
                $normalizedArea + '.meta',
                [System.StringComparison]::OrdinalIgnoreCase)
            if ($isExactFile -or $isFileMeta) {
                return $true
            }
        }

        # Unity creates folder metadata for ancestors of an owned asset. Accept
        # only those ancestor folder .meta files, never arbitrary siblings.
        if ($normalizedChanged.EndsWith('.meta', [System.StringComparison]::OrdinalIgnoreCase)) {
            $folderPath = $normalizedChanged.Substring(0, $normalizedChanged.Length - 5)
            if ($areaWithoutTrailingSlash.StartsWith(
                    $folderPath + '/',
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }

    return $false
}

function Test-TaskScope {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$RequestedTaskId,
        [Parameter(Mandatory = $true)][string[]]$ChangedPaths
    )

    $backlogPath = Join-Path $ProjectPath 'assembly/generated/task_backlog.json'
    if (-not (Test-Path -LiteralPath $backlogPath -PathType Leaf)) {
        throw "Task backlog is missing: $backlogPath"
    }

    $tasks = Get-Content -LiteralPath $backlogPath -Raw | ConvertFrom-Json
    $matchingTasks = @($tasks | Where-Object { $_.id -eq $RequestedTaskId })
    if ($matchingTasks.Count -ne 1) {
        throw "Task '$RequestedTaskId' was not found exactly once in the committed backlog."
    }

    $allowedAreas = @($matchingTasks[0].allowed_areas | ForEach-Object { [string]$_ })
    $unexpectedPaths = @(
        $ChangedPaths | Where-Object {
            -not (Test-AllowedChange -ChangedPath $_ -AllowedAreas $allowedAreas)
        })

    if ($unexpectedPaths.Count -gt 0) {
        $formattedPaths = $unexpectedPaths -join [Environment]::NewLine
        throw "Task '$RequestedTaskId' changed paths outside its allowed areas:$([Environment]::NewLine)$formattedPaths"
    }

    return [PSCustomObject]@{
        task_id = $RequestedTaskId
        allowed_areas = $allowedAreas
        changed_path_count = $ChangedPaths.Count
    }
}

function Invoke-UnityTests {
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][ValidateSet('EditMode', 'PlayMode')][string]$Platform,
        [string]$Filter,
        [Parameter(Mandatory = $true)][string]$ArtifactPath
    )

    $resultsPath = Join-Path $ArtifactPath ($Platform + '-results.xml')
    $logPath = Join-Path $ArtifactPath ($Platform + '-unity.log')
    Remove-Item -LiteralPath $resultsPath, $logPath -Force -ErrorAction SilentlyContinue

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $ProjectPath,
        '-runTests',
        '-testPlatform', $Platform,
        '-testResults', $resultsPath,
        '-logFile', $logPath
    )
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @('-testFilter', $Filter)
    }

    Write-ValidationStatus 'RUN' ("Unity {0} tests" -f $Platform)
    $process = Start-Process -FilePath $ExecutablePath -ArgumentList $arguments -PassThru
    Wait-Process -Id $process.Id
    $process.Refresh()
    $exitCode = $process.ExitCode

    if (-not (Test-Path -LiteralPath $resultsPath -PathType Leaf)) {
        $tail = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            (Get-Content -LiteralPath $logPath -Tail 40) -join [Environment]::NewLine
        }
        else {
            'Unity did not create a log file.'
        }
        throw "Unity $Platform test results were not written (exit code $exitCode).$([Environment]::NewLine)$tail"
    }

    [xml]$results = Get-Content -LiteralPath $resultsPath -Raw
    $testRun = $results.SelectSingleNode('/test-run')
    if ($null -eq $testRun) {
        throw "Unity $Platform result XML does not contain a test-run root: $resultsPath"
    }

    $summary = [PSCustomObject]@{
        platform = $Platform
        total = [int]$testRun.total
        passed = [int]$testRun.passed
        failed = [int]$testRun.failed
        skipped = [int]$testRun.skipped
        inconclusive = [int]$testRun.inconclusive
        unity_exit_code = $exitCode
        results_path = $resultsPath
        log_path = $logPath
    }

    if ($exitCode -ne 0 -or $summary.failed -ne 0) {
        $failureMessage = "Unity {0} tests failed: total={1}, passed={2}, failed={3}, exit={4}. " -f `
            $Platform, $summary.total, $summary.passed, $summary.failed, $exitCode
        throw $failureMessage
    }

    Write-ValidationStatus 'PASS' (
        "Unity {0}: {1}/{2} tests passed" -f $Platform, $summary.passed, $summary.total)
    return $summary
}

function Remove-CloneAfterSuccess {
    param(
        [Parameter(Mandatory = $true)][string]$ClonePath,
        [Parameter(Mandatory = $true)][string]$ArtifactPath
    )

    if (-not (Test-IsChildPath -ChildPath $ClonePath -ParentPath $ArtifactPath)) {
        throw "Refusing to remove clone outside the validation artifact directory: $ClonePath"
    }

    if (Test-Path -LiteralPath $ClonePath) {
        Remove-Item -LiteralPath $ClonePath -Recurse -Force
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is required but was not found on PATH.'
}

$repositoryPath = (Resolve-Path -LiteralPath $RepositoryPath).Path
$repositoryGitDirectory = Join-Path $repositoryPath '.git'
if (-not (Test-Path -LiteralPath $repositoryGitDirectory)) {
    throw "RepositoryPath is not a Git checkout: $repositoryPath"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) (
        'ShooterMover-PrValidation/pr-{0}-{1}' -f $PullRequest, $timestamp)
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputIsInsideRepository = Test-IsChildPath -ChildPath $outputPath -ParentPath $repositoryPath
$outputEqualsRepository = (ConvertTo-NormalizedFullPath $outputPath).Equals(
    (ConvertTo-NormalizedFullPath $repositoryPath),
    [System.StringComparison]::OrdinalIgnoreCase)
if ($outputIsInsideRepository -or $outputEqualsRepository) {
    throw 'OutputDirectory must be outside RepositoryPath so validation cannot dirty the source checkout.'
}

if (Test-Path -LiteralPath $outputPath) {
    throw "OutputDirectory already exists. Choose a new empty path: $outputPath"
}

New-Item -ItemType Directory -Path $outputPath | Out-Null
$clonePath = Join-Path $outputPath 'repository'
$summaryPath = Join-Path $outputPath 'summary.json'
$validationSucceeded = $false

try {
    $originLines = @(Get-NativeOutput -Executable 'git' -Arguments @(
            '-C', $repositoryPath, 'config', '--get', 'remote.origin.url') -Description 'Reading origin remote')
    $originUrl = $originLines[0].Trim()
    if ([string]::IsNullOrWhiteSpace($originUrl)) {
        throw "The repository has no origin remote: $repositoryPath"
    }

    Write-ValidationStatus 'RUN' ("Cloning PR #{0} from origin into {1}" -f $PullRequest, $clonePath)
    Invoke-Native -Executable 'git' -Arguments @('clone', '--no-checkout', $originUrl, $clonePath) -Description 'Creating disposable validation clone'
    Invoke-Native -Executable 'git' -Arguments @(
        '-C', $clonePath, 'fetch', '--quiet', 'origin', $BaseRef,
        ('pull/{0}/head:refs/remotes/origin/pr/{0}' -f $PullRequest)) -Description 'Fetching target branch and pull request head'

    $remoteBase = 'origin/' + $BaseRef
    $remotePullRequest = 'origin/pr/' + $PullRequest
    $mergeBaseLines = @(Get-NativeOutput -Executable 'git' -Arguments @(
            '-C', $clonePath, 'merge-base', $remoteBase, $remotePullRequest) -Description 'Finding pull request merge base')
    $mergeBase = $mergeBaseLines[0].Trim()
    if ([string]::IsNullOrWhiteSpace($mergeBase)) {
        throw "Unable to find a merge base between $remoteBase and $remotePullRequest."
    }

    $changedPaths = @(Get-NativeOutput -Executable 'git' -Arguments @(
            '-C', $clonePath, 'diff', '--name-only', ($mergeBase + '..' + $remotePullRequest)) -Description 'Listing pull request changes')
    Set-Content -LiteralPath (Join-Path $outputPath 'changed-paths.txt') -Value $changedPaths

    Invoke-Native -Executable 'git' -Arguments @(
        '-C', $clonePath, 'diff', '--check', ($mergeBase + '..' + $remotePullRequest)) -Description 'Checking pull request whitespace'
    Write-ValidationStatus 'PASS' 'Git whitespace check'

    Invoke-Native -Executable 'git' -Arguments @(
        '-C', $clonePath, 'checkout', '--detach', $remotePullRequest) -Description 'Checking out pull request for scope validation'

    $scopeSummary = $null
    if (-not [string]::IsNullOrWhiteSpace($TaskId)) {
        $scopeSummary = Test-TaskScope -ProjectPath $clonePath -RequestedTaskId $TaskId -ChangedPaths $changedPaths
        Write-ValidationStatus 'PASS' ("Task {0} scope: {1} changed path(s)" -f $TaskId, $changedPaths.Count)
    }
    else {
        Write-ValidationStatus 'INFO' ("Scope check skipped; pass -TaskId to validate {0} changed path(s) against the backlog." -f $changedPaths.Count)
    }

    Invoke-Native -Executable 'git' -Arguments @('-C', $clonePath, 'checkout', '--detach', $remoteBase) -Description 'Checking out current target branch'
    Invoke-Native -Executable 'git' -Arguments @(
        '-C', $clonePath,
        '-c', 'user.name=Shooter Mover Validation',
        '-c', 'user.email=validation@local.invalid',
        'merge', '--no-ff', '--no-edit', $remotePullRequest) -Description 'Merging pull request onto current target branch'
    Write-ValidationStatus 'PASS' ("PR #{0} merges cleanly onto {1}" -f $PullRequest, $BaseRef)

    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) {
        throw 'python is required for the committed static validators but was not found on PATH.'
    }

    foreach ($validator in @(
            'tools/validation/validate_repository_layout.py',
            'tools/validation/validate_unity_assembly_graph.py')) {
        $validatorPath = Join-Path $clonePath $validator
        if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
            throw "Committed static validator is missing: $validator"
        }

        Invoke-Native -Executable $python.Source -Arguments @($validatorPath) -Description ("Running {0}" -f $validator)
        Write-ValidationStatus 'PASS' $validator
    }

    $testSummaries = @()
    $resolvedUnityPath = $null
    if ($SkipUnity) {
        Write-ValidationStatus 'INFO' 'Unity test execution skipped by request.'
    }
    else {
        $resolvedUnityPath = Resolve-UnityExecutable -ProjectPath $clonePath -RequestedPath $UnityPath
        $platforms = if ($TestPlatform -eq 'Both') { @('EditMode', 'PlayMode') } else { @($TestPlatform) }
        foreach ($platform in $platforms) {
            $testSummaries += Invoke-UnityTests -ExecutablePath $resolvedUnityPath -ProjectPath $clonePath -Platform $platform -Filter $TestFilter -ArtifactPath $outputPath
        }
    }

    $validationCommitLines = @(Get-NativeOutput -Executable 'git' -Arguments @(
            '-C', $clonePath, 'rev-parse', 'HEAD') -Description 'Reading validation commit')
    $summary = [ordered]@{
        pull_request = $PullRequest
        target_ref = $BaseRef
        merge_base = $mergeBase
        validation_commit = $validationCommitLines[0].Trim()
        changed_paths = $changedPaths
        scope = $scopeSummary
        unity_path = $resolvedUnityPath
        unity_skipped = [bool]$SkipUnity
        tests = $testSummaries
        artifacts = $outputPath
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8

    $validationSucceeded = $true
    Write-ValidationStatus 'PASS' ("PR #{0} validation completed. Artifacts: {1}" -f $PullRequest, $outputPath)
}
finally {
    if ($validationSucceeded -and -not $KeepClone) {
        Remove-CloneAfterSuccess -ClonePath $clonePath -ArtifactPath $outputPath
        Write-ValidationStatus 'INFO' 'Disposable clone removed; logs, XML, changed paths, and summary remain in the artifact directory.'
    }
    elseif (Test-Path -LiteralPath $clonePath) {
        Write-ValidationStatus 'INFO' ("Disposable clone retained for inspection: {0}" -f $clonePath)
    }
}
