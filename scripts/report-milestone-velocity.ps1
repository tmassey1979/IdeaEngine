param(
    [string]$Repo = "tmassey1979/IdeaEngine",
    [string]$MilestoneTitle = "Pi-MVP",
    [int]$RecentDays = 7,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Get-GhJson {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gh command failed: gh $($Arguments -join ' ')"
    }

    return $output | ConvertFrom-Json
}

function Get-LabelNames {
    param($Issue)

    if (-not $Issue.labels) {
        return @()
    }

    return @($Issue.labels | ForEach-Object { $_.name })
}

$issues = Get-GhJson -Arguments @(
    "issue", "list",
    "--repo", $Repo,
    "--state", "all",
    "--limit", "500",
    "--json", "number,title,state,createdAt,closedAt,milestone,labels"
)

$milestones = Get-GhJson -Arguments @(
    "api",
    "repos/$Repo/milestones"
)

$milestone = $milestones | Where-Object { $_.title -eq $MilestoneTitle } | Select-Object -First 1
if (-not $milestone) {
    throw "Milestone '$MilestoneTitle' was not found in $Repo."
}

$selected = @($issues | Where-Object { $_.milestone -and $_.milestone.title -eq $MilestoneTitle })
$now = [DateTimeOffset]::Now
$recentWindowStart = $now.AddDays(-1 * [Math]::Max(1, $RecentDays))

$closed = @($selected | Where-Object { $_.state -eq "CLOSED" })
$open = @($selected | Where-Object { $_.state -eq "OPEN" })

$milestoneCreatedAt = [DateTimeOffset]::Parse($milestone.created_at)
$dueOn = if ($milestone.due_on) { [DateTimeOffset]::Parse($milestone.due_on) } else { $null }
$daysSinceMilestoneCreated = [Math]::Max(1.0, ($now - $milestoneCreatedAt).TotalDays)
$recentClosed = @($closed | Where-Object { $_.closedAt -and [DateTimeOffset]::Parse($_.closedAt) -ge $recentWindowStart })

$velocitySinceCreated = if ($closed.Count -gt 0) {
    [Math]::Round($closed.Count / $daysSinceMilestoneCreated, 2)
} else {
    0.0
}

$velocityRecent = if ($recentClosed.Count -gt 0) {
    [Math]::Round($recentClosed.Count / [Math]::Max(1, $RecentDays), 2)
} else {
    0.0
}

$activeVelocity = if ($velocityRecent -gt 0) { $velocityRecent } else { $velocitySinceCreated }
$etaDays = if ($activeVelocity -gt 0) {
    [Math]::Round($open.Count / $activeVelocity, 1)
} else {
    $null
}

$requiredPerDay = if ($dueOn) {
    $daysRemaining = ($dueOn - $now).TotalDays
    if ($daysRemaining -gt 0) {
        [Math]::Round($open.Count / $daysRemaining, 2)
    } else {
        $null
    }
} else {
    $null
}

$projectedFinishDate = if ($etaDays -ne $null) {
    $now.AddDays($etaDays).ToString("yyyy-MM-dd")
} else {
    $null
}

$piMvpCore = @($selected | Where-Object { (Get-LabelNames $_) -contains "pi-mvp-core" })
$piMvpAutonomy = @($selected | Where-Object { (Get-LabelNames $_) -contains "pi-mvp-autonomy" })
$piMvpOps = @($selected | Where-Object { (Get-LabelNames $_) -contains "pi-mvp-ops" })
$inProgress = @($selected | Where-Object { (Get-LabelNames $_) -contains "in-progress" })
$quarantined = @($selected | Where-Object { (Get-LabelNames $_) -contains "quarantined" })
$waitingFollowUp = @($selected | Where-Object { (Get-LabelNames $_) -contains "waiting-follow-up" })

$report = [pscustomobject]@{
    repo = $Repo
    milestone = $MilestoneTitle
    milestoneNumber = $milestone.number
    milestoneCreatedAt = $milestone.created_at
    milestoneDueOn = $milestone.due_on
    totalIssues = $selected.Count
    openIssues = $open.Count
    closedIssues = $closed.Count
    completionPercent = if ($selected.Count -gt 0) { [Math]::Round(($closed.Count / $selected.Count) * 100, 1) } else { 0.0 }
    inProgressIssues = $inProgress.Count
    quarantinedIssues = $quarantined.Count
    waitingFollowUpIssues = $waitingFollowUp.Count
    piMvpCoreIssues = $piMvpCore.Count
    piMvpAutonomyIssues = $piMvpAutonomy.Count
    piMvpOpsIssues = $piMvpOps.Count
    recentWindowDays = $RecentDays
    recentClosedIssues = $recentClosed.Count
    velocityClosedPerDaySinceMilestoneCreated = $velocitySinceCreated
    velocityClosedPerDayRecent = $velocityRecent
    etaDays = $etaDays
    projectedFinishDate = $projectedFinishDate
    requiredClosedPerDayToHitDueDate = $requiredPerDay
}

if ($Json) {
    $report | ConvertTo-Json -Depth 5
    exit 0
}

Write-Output "Milestone Velocity Report"
Write-Output "repo: $($report.repo)"
Write-Output "milestone: $($report.milestone) (#$($report.milestoneNumber))"
Write-Output "created_at: $($report.milestoneCreatedAt)"
Write-Output "due_on: $($report.milestoneDueOn)"
Write-Output "total_issues: $($report.totalIssues)"
Write-Output "open_issues: $($report.openIssues)"
Write-Output "closed_issues: $($report.closedIssues)"
Write-Output "completion_percent: $($report.completionPercent)"
Write-Output "in_progress_issues: $($report.inProgressIssues)"
Write-Output "quarantined_issues: $($report.quarantinedIssues)"
Write-Output "waiting_follow_up_issues: $($report.waitingFollowUpIssues)"
Write-Output "pi_mvp_core_issues: $($report.piMvpCoreIssues)"
Write-Output "pi_mvp_autonomy_issues: $($report.piMvpAutonomyIssues)"
Write-Output "pi_mvp_ops_issues: $($report.piMvpOpsIssues)"
Write-Output "recent_window_days: $($report.recentWindowDays)"
Write-Output "recent_closed_issues: $($report.recentClosedIssues)"
Write-Output "velocity_closed_per_day_since_milestone_created: $($report.velocityClosedPerDaySinceMilestoneCreated)"
Write-Output "velocity_closed_per_day_recent: $($report.velocityClosedPerDayRecent)"
$etaDaysText = if ($null -ne $report.etaDays) { $report.etaDays } else { "unknown" }
$projectedFinishText = if ($report.projectedFinishDate) { $report.projectedFinishDate } else { "unknown" }
$requiredPerDayText = if ($null -ne $report.requiredClosedPerDayToHitDueDate) { $report.requiredClosedPerDayToHitDueDate } else { "unknown" }
Write-Output "eta_days: $etaDaysText"
Write-Output "projected_finish_date: $projectedFinishText"
Write-Output "required_closed_per_day_to_hit_due_date: $requiredPerDayText"
