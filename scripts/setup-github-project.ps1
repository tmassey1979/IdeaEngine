param(
    [string]$Owner = "tmassey1979",
    [string]$Repo = "tmassey1979/IdeaEngine",
    [string]$ProjectTitle = "Dragon Idea Engine",
    [string]$MilestoneTitle = "Pi-MVP",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Invoke-Gh {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output | Out-String).Trim()
    }

    return $output
}

function Invoke-GhJson {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    (Invoke-Gh -Arguments $Arguments) | ConvertFrom-Json
}

function Ensure-ProjectScope {
    try {
        Invoke-Gh -Arguments @("project", "list", "--owner", $Owner, "--limit", "1") | Out-Null
        return
    }
    catch {
        throw "GitHub project access is not available with the current token. Run: gh auth refresh --hostname github.com -s read:project,project"
    }
}

function Ensure-Project {
    $projects = Invoke-GhJson -Arguments @("project", "list", "--owner", $Owner, "--limit", "100", "--format", "json")
    $existing = @($projects.projects) | Where-Object { $_.title -eq $ProjectTitle } | Select-Object -First 1
    if ($existing) {
        return $existing
    }

    if ($DryRun) {
        return [pscustomobject]@{ id = "DRYRUN"; number = 0; title = $ProjectTitle }
    }

    return Invoke-GhJson -Arguments @("project", "create", "--owner", $Owner, "--title", $ProjectTitle, "--format", "json")
}

function Ensure-Field {
    param(
        [Parameter(Mandatory = $true)]
        $Project,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$DataType,
        [string[]]$Options = @()
    )

    $fields = Invoke-GhJson -Arguments @("project", "field-list", $Project.number.ToString(), "--owner", $Owner, "--limit", "100", "--format", "json")
    $existing = @($fields.fields) | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if ($existing) {
        return $existing
    }

    if ($DryRun) {
        return [pscustomobject]@{ id = "DRYRUN-$Name"; name = $Name; options = @() }
    }

    $args = @("project", "field-create", $Project.number.ToString(), "--owner", $Owner, "--name", $Name, "--data-type", $DataType, "--format", "json")
    if ($Options.Count -gt 0) {
        $args += @("--single-select-options", ($Options -join ","))
    }

    return Invoke-GhJson -Arguments $args
}

function Get-LabelNames {
    param($Issue)
    if (-not $Issue.labels) { return @() }
    return @($Issue.labels | ForEach-Object { $_.name })
}

function Resolve-StatusOption {
    param($Issue)

    $labels = Get-LabelNames $Issue
    if ($Issue.state -eq "CLOSED") { return "Done" }
    if ($labels -contains "quarantined" -or $labels -contains "stalled") { return "Blocked" }
    if ($labels -contains "waiting-follow-up") { return "Review" }
    if ($labels -contains "in-progress") { return "In Progress" }
    return "Backlog"
}

function Resolve-StreamOption {
    param($Issue)

    $labels = Get-LabelNames $Issue
    if ($labels -contains "pi-mvp-core") { return "Pi MVP Core" }
    if ($labels -contains "pi-mvp-autonomy") { return "Pi MVP Autonomy" }
    if ($labels -contains "pi-mvp-ops") { return "Pi MVP Ops" }
    if ($labels -contains "pi-future-cluster") { return "Future Cluster" }
    return "Other"
}

function Resolve-PriorityOption {
    param($Issue)

    $labels = Get-LabelNames $Issue
    if ($labels -contains "pi-mvp-core") { return "P0" }
    if ($labels -contains "pi-mvp-autonomy" -or $labels -contains "pi-mvp-ops") { return "P1" }
    if ($labels -contains "pi-future-cluster") { return "P3" }
    return "P2"
}

function Get-OptionId {
    param(
        [Parameter(Mandatory = $true)]$Field,
        [Parameter(Mandatory = $true)][string]$Name
    )

    return (@($Field.options) | Where-Object { $_.name -eq $Name } | Select-Object -First 1).id
}

Ensure-ProjectScope
$project = Ensure-Project

$statusField = Ensure-Field -Project $project -Name "Status" -DataType "SINGLE_SELECT" -Options @("Backlog", "Ready", "In Progress", "Blocked", "Review", "Done")
$streamField = Ensure-Field -Project $project -Name "Stream" -DataType "SINGLE_SELECT" -Options @("Pi MVP Core", "Pi MVP Autonomy", "Pi MVP Ops", "Future Cluster", "Other")
$priorityField = Ensure-Field -Project $project -Name "Priority" -DataType "SINGLE_SELECT" -Options @("P0", "P1", "P2", "P3")
$etaNotesField = Ensure-Field -Project $project -Name "ETA Notes" -DataType "TEXT"

$issues = Invoke-GhJson -Arguments @(
    "issue", "list",
    "--repo", $Repo,
    "--state", "all",
    "--limit", "500",
    "--json", "number,title,state,labels,milestone"
)

$selected = @($issues | Where-Object { $_.milestone -and $_.milestone.title -eq $MilestoneTitle })

foreach ($issue in $selected) {
    $issueUrl = "https://github.com/$Repo/issues/$($issue.number)"
    if ($DryRun) {
        Write-Output "Would add $issueUrl to project '$ProjectTitle'."
        continue
    }

    $addResult = Invoke-GhJson -Arguments @(
        "project", "item-add", $project.number.ToString(),
        "--owner", $Owner,
        "--url", $issueUrl,
        "--format", "json"
    )

    $itemId = $addResult.id
    if (-not $itemId) {
        continue
    }

    $statusOptionId = Get-OptionId -Field $statusField -Name (Resolve-StatusOption $issue)
    $streamOptionId = Get-OptionId -Field $streamField -Name (Resolve-StreamOption $issue)
    $priorityOptionId = Get-OptionId -Field $priorityField -Name (Resolve-PriorityOption $issue)

    if ($statusOptionId) {
        Invoke-Gh -Arguments @("project", "item-edit", "--id", $itemId, "--project-id", $project.id, "--field-id", $statusField.id, "--single-select-option-id", $statusOptionId) | Out-Null
    }
    if ($streamOptionId) {
        Invoke-Gh -Arguments @("project", "item-edit", "--id", $itemId, "--project-id", $project.id, "--field-id", $streamField.id, "--single-select-option-id", $streamOptionId) | Out-Null
    }
    if ($priorityOptionId) {
        Invoke-Gh -Arguments @("project", "item-edit", "--id", $itemId, "--project-id", $project.id, "--field-id", $priorityField.id, "--single-select-option-id", $priorityOptionId) | Out-Null
    }

    $etaNote = switch (Resolve-StreamOption $issue) {
        "Pi MVP Core" { "Foundational runtime and deployment work." }
        "Pi MVP Autonomy" { "Autonomous execution and self-build throughput work." }
        "Pi MVP Ops" { "Monitoring, safety, and operator visibility work." }
        "Future Cluster" { "Not required for single-Pi MVP." }
        default { "Needs triage." }
    }
    Invoke-Gh -Arguments @("project", "item-edit", "--id", $itemId, "--project-id", $project.id, "--field-id", $etaNotesField.id, "--text", $etaNote) | Out-Null
}

Write-Output "Project ready: $ProjectTitle"
Write-Output "Owner: $Owner"
Write-Output "Milestone synced: $MilestoneTitle"
Write-Output "Fields: Status, Stream, Priority, ETA Notes"
