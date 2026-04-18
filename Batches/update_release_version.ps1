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
    $fileEncoding = Get-FileEncoding $fullPath
    $before = [System.IO.File]::ReadAllText($fullPath, $fileEncoding)
    $after = & $Updater $before

    if ($before -eq $after) {
        Write-Host "No changes: $Path"
        return
    }

    if ($PSCmdlet.ShouldProcess($Path, 'Update release metadata')) {
        [System.IO.File]::WriteAllText($fullPath, $after, $fileEncoding)
        Write-Host "Updated: $Path"
    } else {
        Write-Host "Would update: $Path"
    }
}

function Get-FileEncoding {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)

    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.UTF8Encoding]::new($true)
    }

    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.UnicodeEncoding]::new($false, $true)
    }

    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        return [System.Text.UnicodeEncoding]::new($true, $true)
    }

    if ($bytes.Length -ge 4 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE -and $bytes[2] -eq 0x00 -and $bytes[3] -eq 0x00) {
        return [System.Text.UTF32Encoding]::new($false, $true)
    }

    if ($bytes.Length -ge 4 -and $bytes[0] -eq 0x00 -and $bytes[1] -eq 0x00 -and $bytes[2] -eq 0xFE -and $bytes[3] -eq 0xFF) {
        return [System.Text.UTF32Encoding]::new($true, $true)
    }

    return [System.Text.UTF8Encoding]::new($false)
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
    Replace-SingleMatch $updated '(?s)(v\d+\.\d+\.\d+\r?\n\r?\n\* .*\r?\n)\* \d{4}/\d{2}/\d{2}' "`$1* $jpDate"
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
