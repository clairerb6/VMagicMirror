[CmdletBinding(SupportsShouldProcess = $true)]
# Example:
#   .\Batches\update_release_version.cmd 4.5.0 2026/03/31
#   powershell -ExecutionPolicy Bypass -File .\Batches\update_release_version.ps1 -Version 4.5.0 -ReleaseDate 2026/03/31
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseDate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Normalize-Version {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputVersion
    )

    $trimmed = $InputVersion.Trim()
    if ($trimmed -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version must be in the form '4.5.0'."
    }

    return "v$trimmed"
}

function Parse-ReleaseDate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputDate
    )

    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    $style = [System.Globalization.DateTimeStyles]::None

    $parsed = [DateTime]::MinValue
    if ([DateTime]::TryParseExact($InputDate, 'yyyy/MM/dd', $culture, $style, [ref]$parsed)) {
        return $parsed
    }

    throw "ReleaseDate must be in the form '2026/01/30'."
}

function Update-FileText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Updater
    )

    $fullPath = Join-Path $repoRoot $Path
    $before = [System.IO.File]::ReadAllText($fullPath)
    $after = & $Updater $before

    if ($before -eq $after) {
        Write-Host "No changes: $Path"
        return
    }

    if ($PSCmdlet.ShouldProcess($Path, 'Update release metadata')) {
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($fullPath, $after, $utf8NoBom)
        Write-Host "Updated: $Path"
    } else {
        Write-Host "Would update: $Path"
    }
}

function Replace-SingleMatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$Replacement
    )

    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Content,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )

    if ($matches.Count -ne 1) {
        throw "Expected exactly one match for pattern: $Pattern"
    }

    return [System.Text.RegularExpressions.Regex]::Replace(
        $Content,
        $Pattern,
        $Replacement,
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$normalizedVersion = Normalize-Version $Version
$versionNumbers = $normalizedVersion.TrimStart('v').Split('.')
$parsedReleaseDate = Parse-ReleaseDate $ReleaseDate

$jpDate = $parsedReleaseDate.ToString('yyyy/MM/dd', [System.Globalization.CultureInfo]::InvariantCulture)
$enDate = $parsedReleaseDate.ToString('yyyy/MMM/dd', [System.Globalization.CultureInfo]::InvariantCulture)
$appVersionExpr = "new($($versionNumbers[0]), $($versionNumbers[1]), $($versionNumbers[2]))"

Update-FileText -Path 'Batches/version.txt' -Updater {
    param($content)
    Replace-SingleMatch $content 'v\d+\.\d+\.\d+' $normalizedVersion
}

Update-FileText -Path 'WPF/VMagicMirrorConfig/Common/AppConsts.cs' -Updater {
    param($content)
    Replace-SingleMatch $content 'AppVersion => new\(\d+, \d+, \d+\);' "AppVersion => $appVersionExpr;"
}

Update-FileText -Path 'README.md' -Updater {
    param($content)
    $updated = Replace-SingleMatch $content '(?s)(Logo: .*?\r?\n\r?\n)v\d+\.\d+\.\d+' "`$1$normalizedVersion"
    Replace-SingleMatch $updated '(?s)(\* 作成: .*?\r?\n)\* \d{4}/\d{2}/\d{2}' "`$1* $jpDate"
}

Update-FileText -Path 'README_en.md' -Updater {
    param($content)
    $updated = Replace-SingleMatch $content '(?s)(Logo: .*?\r?\n\r?\n)v\d+\.\d+\.\d+' "`$1$normalizedVersion"
    Replace-SingleMatch $updated '(?s)(\* Author: .*?\r?\n)\* \d{4}/[A-Z][a-z]{2}/\d{2}' "`$1* $enDate"
}

Update-FileText -Path 'docs/_config.yml' -Updater {
    param($content)
    $updated = Replace-SingleMatch $content 'latest_version: v\d+\.\d+\.\d+' "latest_version: $normalizedVersion"
    Replace-SingleMatch $updated 'latest_update: \d{4}/\d{2}/\d{2}' "latest_update: $jpDate"
}

Update-FileText -Path 'docs/_local_config.yml' -Updater {
    param($content)
    $updated = Replace-SingleMatch $content 'latest_version: v\d+\.\d+\.\d+' "latest_version: $normalizedVersion"
    Replace-SingleMatch $updated 'latest_update: \d{4}/\d{2}/\d{2}' "latest_update: $jpDate"
}

Write-Host ''
Write-Host "Version : $normalizedVersion"
Write-Host "Date    : $jpDate"
Write-Host 'Completed.'
