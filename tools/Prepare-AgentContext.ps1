<#
.SYNOPSIS
Generates a copy/paste-ready Shooter Mover task brief from repository state.

.DESCRIPTION
Reads the committed task backlog and context files from a local checkout. It
combines accepted committed collaboration records, merged GitHub pull-request
titles when available, and task-tagged commit subjects reachable from HEAD. It
never changes product files, branches, or task state.

The generated Markdown is intended for a human to review and paste into a web
agent. Project-specific facts come from the checkout and, when selected, merged
pull-request metadata. Any explicit completion override is labeled as such.

.PARAMETER TaskId
The canonical task ID for a detailed brief, for example MT-004.

.PARAMETER ReadyTasks
Lists every task whose dependencies are currently satisfied instead of
preparing one detailed task brief.

.PARAMETER RepositoryPath
The repository root. Defaults to the parent of this script's tools directory.

.PARAMETER CompletionSource
Auto prefers merged GitHub PR titles when gh is authenticated and otherwise
uses local Git history. GitHub requires gh; Git uses only commits reachable
from HEAD.

.PARAMETER CompletedTaskId
Optional explicit completion overrides. These do not write state and are
clearly marked in the output.

.PARAMETER WebAgent
Adds the operating instructions for the browser-based agents used on this
project.

.PARAMETER OutputPath
Optional Markdown output path. Without it, the brief is written to stdout.

.EXAMPLE
.\tools\Prepare-AgentContext.ps1 -TaskId MT-004 -CompletionSource GitHub -WebAgent

.EXAMPLE
.\tools\Prepare-AgentContext.ps1 -ReadyTasks -CompletionSource GitHub

.EXAMPLE
.\tools\Prepare-AgentContext.ps1 -TaskId MT-004 -OutputPath "$env:TEMP\MT-004-context.md"
#>
[CmdletBinding(DefaultParameterSetName = 'Task')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Task')]
    [ValidatePattern('^[A-Za-z]+-[0-9]+$')]
    [string]$TaskId,

    [Parameter(Mandatory = $true, ParameterSetName = 'Ready')]
    [switch]$ReadyTasks,

    [string]$RepositoryPath = (Split-Path -Parent $PSScriptRoot),

    [ValidateSet('Auto', 'GitHub', 'Git')]
    [string]$CompletionSource = 'Auto',

    [string[]]$CompletedTaskId = @(),

    [switch]$WebAgent,

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-GitCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $output = & git -C $Repository @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).TrimEnd()
}

function Invoke-GhCommand {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return ($output -join [Environment]::NewLine).TrimEnd()
}

function Get-TaskIdsFromText {
    param(
        [AllowEmptyString()][string]$Text,
        [Parameter(Mandatory = $true)][hashtable]$TasksById
    )

    $matches = [regex]::Matches($Text, '(?<![A-Za-z0-9])([A-Z]{2,}-[0-9]+)(?![A-Za-z0-9])')
    foreach ($match in $matches) {
        $candidate = $match.Groups[1].Value.ToUpperInvariant()
        if ($TasksById.ContainsKey($candidate)) {
            $candidate
        }
    }
}

function Get-StringValues {
    param($Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value | ForEach-Object { [string]$_ })
}

function Add-MarkdownList {
    param(
        [Parameter(Mandatory = $true)][System.Text.StringBuilder]$Builder,
        [string[]]$Values,
        [string]$EmptyText = '_None._'
    )

    if ($Values.Count -eq 0) {
        [void]$Builder.AppendLine($EmptyText)
        return
    }

    foreach ($value in $Values) {
        [void]$Builder.AppendLine("- $value")
    }
}

function Get-GitHubRepositoryName {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$RemoteUrl
    )

    $match = [regex]::Match($RemoteUrl, 'github\.com[:/]([^/]+)/([^/]+?)(?:\.git)?$')
    if (-not $match.Success) {
        throw "The origin remote is not a GitHub repository: $RemoteUrl"
    }

    return "$($match.Groups[1].Value)/$($match.Groups[2].Value)"
}

function Get-CompletedTasksFromGitHub {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryName,
        [Parameter(Mandatory = $true)][hashtable]$TasksById
    )

    $json = Invoke-GhCommand -Arguments @(
        'pr', 'list', '--repo', $RepositoryName, '--state', 'merged', '--limit', '200',
        '--json', 'number,title,mergedAt'
    )

    $pullRequests = $json | ConvertFrom-Json
    $result = @{}
    foreach ($pullRequest in $pullRequests) {
        foreach ($id in @(Get-TaskIdsFromText -Text $pullRequest.title -TasksById $TasksById | Sort-Object -Unique)) {
            $result[$id] = "merged PR #$($pullRequest.number) ($($pullRequest.title))"
        }
    }

    return $result
}

function Get-CompletedTasksFromGit {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][hashtable]$TasksById
    )

    $subjects = Invoke-GitCommand -Repository $Repository -Arguments @('log', '--format=%s', 'HEAD')
    $result = @{}
    foreach ($subject in @($subjects -split "`r?`n")) {
        foreach ($id in @(Get-TaskIdsFromText -Text $subject -TasksById $TasksById | Sort-Object -Unique)) {
            $result[$id] = "local Git subject ($subject)"
        }
    }

    return $result
}

function Get-CompletedTasksFromCollaboration {
    param(
        [Parameter(Mandatory = $true)]$CollaborationState,
        [Parameter(Mandatory = $true)][hashtable]$TasksById
    )

    $result = @{}
    foreach ($assignment in @($CollaborationState.task_assignments)) {
        $id = ([string]$assignment.task_id).ToUpperInvariant()
        if ($TasksById.ContainsKey($id) -and ([string]$assignment.status).ToLowerInvariant() -in @('accepted', 'done')) {
            $result[$id] = "committed collaboration assignment marked $($assignment.status)"
        }
    }

    foreach ($submission in @($CollaborationState.artifact_submissions)) {
        $id = ([string]$submission.task_id).ToUpperInvariant()
        if ($TasksById.ContainsKey($id) -and ([string]$submission.status).ToLowerInvariant() -eq 'accepted') {
            $result[$id] = "committed accepted submission $($submission.submission_id)"
        }
    }

    return $result
}

function Add-CompletionEvidence {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Destination,
        [Parameter(Mandatory = $true)][hashtable]$Source
    )

    foreach ($id in $Source.Keys) {
        if ($Destination.ContainsKey($id)) {
            if ($Destination[$id] -notlike "*$($Source[$id])*") {
                $Destination[$id] = "$($Destination[$id]); $($Source[$id])"
            }
        }
        else {
            $Destination[$id] = $Source[$id]
        }
    }
}

function Write-OutputDocument {
    param(
        [Parameter(Mandatory = $true)][string]$Document,
        [string]$Destination
    )

    if ([string]::IsNullOrWhiteSpace($Destination)) {
        Write-Output $Document.TrimEnd()
        return
    }

    $parent = Split-Path -Parent $Destination
    if ([string]::IsNullOrWhiteSpace($parent) -or -not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw "OutputPath parent directory does not exist: $parent"
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Destination, $Document, $encoding)
    Write-Host "Wrote context to $Destination"
}

$RepositoryPath = (Resolve-Path -LiteralPath $RepositoryPath).Path
[void](Invoke-GitCommand -Repository $RepositoryPath -Arguments @('rev-parse', '--show-toplevel'))

$requiredFiles = @(
    'AGENTS.md',
    'project_workspace.json',
    'assembly/context/CURRENT_HANDOFF.json',
    'assembly/context/NEW_CHAT_RESUME.md',
    'assembly/generated/collaboration_state.json',
    'assembly/generated/task_backlog.json'
)
foreach ($relativePath in $requiredFiles) {
    $absolutePath = Join-Path $RepositoryPath $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        throw "Required context file is missing: $relativePath"
    }
}

$workspace = Get-Content -LiteralPath (Join-Path $RepositoryPath 'project_workspace.json') -Raw | ConvertFrom-Json
$handoff = Get-Content -LiteralPath (Join-Path $RepositoryPath 'assembly/context/CURRENT_HANDOFF.json') -Raw | ConvertFrom-Json
$resume = Get-Content -LiteralPath (Join-Path $RepositoryPath 'assembly/context/NEW_CHAT_RESUME.md') -Raw
$collaboration = Get-Content -LiteralPath (Join-Path $RepositoryPath 'assembly/generated/collaboration_state.json') -Raw | ConvertFrom-Json
$taskObjects = Get-Content -LiteralPath (Join-Path $RepositoryPath 'assembly/generated/task_backlog.json') -Raw | ConvertFrom-Json
$tasksById = @{}
foreach ($task in $taskObjects) {
    $tasksById[$task.id.ToUpperInvariant()] = $task
}

$head = Invoke-GitCommand -Repository $RepositoryPath -Arguments @('rev-parse', 'HEAD')
$branch = Invoke-GitCommand -Repository $RepositoryPath -Arguments @('branch', '--show-current')
$dirtyStatus = Invoke-GitCommand -Repository $RepositoryPath -Arguments @('status', '--porcelain')
$dirtyPaths = @($dirtyStatus -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$remoteUrl = Invoke-GitCommand -Repository $RepositoryPath -Arguments @('remote', 'get-url', 'origin')

$completed = @{}
$collaborationCompleted = Get-CompletedTasksFromCollaboration -CollaborationState $collaboration -TasksById $tasksById
$localGitCompleted = Get-CompletedTasksFromGit -Repository $RepositoryPath -TasksById $tasksById
Add-CompletionEvidence -Destination $completed -Source $collaborationCompleted
Add-CompletionEvidence -Destination $completed -Source $localGitCompleted
$completionLabel = $null
$completionWarning = $null
$shouldTryGitHub = $CompletionSource -eq 'GitHub' -or ($CompletionSource -eq 'Auto' -and $null -ne (Get-Command gh -ErrorAction SilentlyContinue))
if ($shouldTryGitHub) {
    try {
        $githubRepository = Get-GitHubRepositoryName -Repository $RepositoryPath -RemoteUrl $remoteUrl
        $githubCompleted = Get-CompletedTasksFromGitHub -RepositoryName $githubRepository -TasksById $tasksById
        Add-CompletionEvidence -Destination $completed -Source $githubCompleted
        $completionLabel = "accepted committed collaboration records, task-tagged local Git subjects, and merged GitHub PR titles for $githubRepository"
    }
    catch {
        if ($CompletionSource -eq 'GitHub') {
            throw
        }

        $completionWarning = "GitHub completion lookup was unavailable; using local Git history instead. $($_.Exception.Message)"
    }
}

if ($null -eq $completionLabel) {
    $completionLabel = 'accepted committed collaboration records and task-tagged commit subjects reachable from HEAD (best effort)'
}

foreach ($id in $CompletedTaskId) {
    $normalizedId = $id.ToUpperInvariant()
    if (-not $tasksById.ContainsKey($normalizedId)) {
        throw "CompletedTaskId is not a task in the canonical backlog: $id"
    }

    $completed[$normalizedId] = 'explicit completion override'
}

$ready = @()
foreach ($candidate in $tasksById.Values) {
    $candidateId = $candidate.id.ToUpperInvariant()
    if ($completed.ContainsKey($candidateId)) {
        continue
    }

    $dependencies = @(Get-StringValues $candidate.depends_on)
    $unmet = @($dependencies | Where-Object { -not $completed.ContainsKey($_.ToUpperInvariant()) })
    if ($unmet.Count -eq 0) {
        $ready += $candidate
    }
}

$builder = New-Object System.Text.StringBuilder
[void]$builder.AppendLine('# Shooter Mover Agent Context')
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Snapshot')
[void]$builder.AppendLine()
[void]$builder.AppendLine(('- Repository: `' + $workspace.repository.full_name + '`'))
[void]$builder.AppendLine(('- Checkout branch: `' + $branch + '`'))
[void]$builder.AppendLine(('- Checkout commit: `' + $head + '`'))
[void]$builder.AppendLine("- Completion discovery: $completionLabel")
if ($dirtyPaths.Count -eq 0) {
    [void]$builder.AppendLine('- Checkout is clean.')
}
else {
    [void]$builder.AppendLine("- Warning: checkout has $($dirtyPaths.Count) uncommitted path(s); do not treat it as a clean task base.")
}
if ($handoff.last_verified_commit -and $handoff.last_verified_commit -ne $head) {
    [void]$builder.AppendLine(('- Committed handoff is older than this checkout (`' + $handoff.last_verified_commit + '`); treat it as background, not live status.'))
}
if ($completionWarning) {
    [void]$builder.AppendLine("- Warning: $completionWarning")
}
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Required Repository Reading')
[void]$builder.AppendLine()
Add-MarkdownList -Builder $builder -Values @(
    '`AGENTS.md`',
    '`project_workspace.json`',
    '`assembly/context/CURRENT_HANDOFF.json`',
    '`assembly/context/NEW_CHAT_RESUME.md`',
    '`assembly/generated/task_backlog.json`'
)

if ($ReadyTasks) {
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('## Tasks Ready From This Snapshot')
    [void]$builder.AppendLine()
    if ($ready.Count -eq 0) {
        [void]$builder.AppendLine('_No task is dependency-ready from the discovered completion set._')
    }
    else {
        foreach ($candidate in @($ready | Sort-Object id)) {
            $dependencies = @(Get-StringValues $candidate.depends_on)
            $dependencyText = if ($dependencies.Count -eq 0) { 'none' } else { $dependencies -join ', ' }
            [void]$builder.AppendLine("- **$($candidate.id)** - $($candidate.title) (depends on: $dependencyText)")
        }
    }

    Write-OutputDocument -Document $builder.ToString() -Destination $OutputPath
    exit 0
}

$normalizedTaskId = $TaskId.ToUpperInvariant()
if (-not $tasksById.ContainsKey($normalizedTaskId)) {
    throw "TaskId is not in the canonical backlog: $TaskId"
}

$selectedTask = $tasksById[$normalizedTaskId]
$dependencies = @(Get-StringValues $selectedTask.depends_on)
$completedDependencies = @($dependencies | Where-Object { $completed.ContainsKey($_.ToUpperInvariant()) })
$unmetDependencies = @($dependencies | Where-Object { -not $completed.ContainsKey($_.ToUpperInvariant()) })
$taskIsReady = $unmetDependencies.Count -eq 0

[void]$builder.AppendLine()
[void]$builder.AppendLine("## Task: $($selectedTask.id) - $($selectedTask.title)")
[void]$builder.AppendLine()
[void]$builder.AppendLine($selectedTask.summary)
[void]$builder.AppendLine()
[void]$builder.AppendLine("- Role: $($selectedTask.owner_role)")
[void]$builder.AppendLine("- Lane: $($selectedTask.lane)")
[void]$builder.AppendLine("- Milestone: $($selectedTask.milestone)")
[void]$builder.AppendLine("- Dependency-ready from this snapshot: **$($taskIsReady.ToString().ToUpperInvariant())**")

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Dependency Calculation')
[void]$builder.AppendLine()
if ($dependencies.Count -eq 0) {
    [void]$builder.AppendLine('_This task has no declared dependencies._')
}
else {
    foreach ($dependency in $dependencies) {
        $state = if ($completed.ContainsKey($dependency.ToUpperInvariant())) { "satisfied - $($completed[$dependency.ToUpperInvariant()])" } else { 'not discovered as complete' }
        [void]$builder.AppendLine(('- `' + $dependency + '`' + ': ' + $state))
    }
}

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Objective')
[void]$builder.AppendLine()
[void]$builder.AppendLine($selectedTask.objective)
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Exact Allowed Areas')
[void]$builder.AppendLine()
Add-MarkdownList -Builder $builder -Values (Get-StringValues $selectedTask.allowed_areas | ForEach-Object { '`' + $_ + '`' })
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Required Inputs')
[void]$builder.AppendLine()
Add-MarkdownList -Builder $builder -Values (Get-StringValues $selectedTask.inputs)
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Acceptance Criteria')
[void]$builder.AppendLine()
Add-MarkdownList -Builder $builder -Values (Get-StringValues $selectedTask.acceptance_criteria)
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Required Verification and Proof')
[void]$builder.AppendLine()
Add-MarkdownList -Builder $builder -Values (Get-StringValues $selectedTask.verification)
Add-MarkdownList -Builder $builder -Values (Get-StringValues $selectedTask.proof_required)
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Non-goals and Implementation Notes')
[void]$builder.AppendLine()
$nonGoals = @(Get-StringValues $selectedTask.non_goals)
$implementationNotes = @(Get-StringValues $selectedTask.implementation_notes)
Add-MarkdownList -Builder $builder -Values @($nonGoals + $implementationNotes)

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Committed Resume Note')
[void]$builder.AppendLine()
[void]$builder.AppendLine('Read `assembly/context/NEW_CHAT_RESUME.md` for durable workflow guidance. Its exact-next-action text is deliberately not copied here because the snapshot above is the current dispatch source and may be newer.')

if ($WebAgent) {
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('## Web-agent Operating Instructions')
    [void]$builder.AppendLine()
    Add-MarkdownList -Builder $builder -Values @(
        'Use only the built-in authenticated GitHub connector for repository and PR work.',
        'Do not use `gh`, local Git, cloning, shell authentication checks, or browser login.',
        'Start from the current `main` in a fresh branch, open a draft PR, and do not merge it.',
        'Change only the exact allowed areas and inseparable Unity metadata.',
        'Put dependency proof, changed paths, validation, limitations, and rollback in the draft PR description.'
    )
}

Write-OutputDocument -Document $builder.ToString() -Destination $OutputPath
