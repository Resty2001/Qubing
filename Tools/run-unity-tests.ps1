[CmdletBinding()]
param(
    [Parameter()]
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'

$RequiredUnityVersion = '6000.3.3f1'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = [System.IO.Path]::GetFullPath((Join-Path $ScriptRoot '..'))
$ResultsRoot = Join-Path $ProjectRoot 'TestResults'

function Fail-QubingTests {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter()]
        [string]$LogPath
    )

    Write-Host ''
    Write-Host '[Qubing Tests] FAILED' -ForegroundColor Red
    Write-Host $Message -ForegroundColor Red
    if ($LogPath) {
        Write-Host 'See:'
        Write-Host $LogPath
    }

    exit 1
}

function Resolve-UnityExecutable {
    param(
        [Parameter()]
        [string]$ExplicitPath
    )

    $candidate = $null
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidate = $ExplicitPath
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:UNITY_PATH)) {
        $candidate = $env:UNITY_PATH
    }
    else {
        $candidate = Join-Path $env:ProgramFiles `
            'Unity\Hub\Editor\6000.3.3f1\Editor\Unity.exe'
    }

    $candidate = [Environment]::ExpandEnvironmentVariables($candidate.Trim().Trim('"'))
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Unity $RequiredUnityVersion was not found at '$candidate'. " +
            "Pass -UnityPath '<path-to-Unity.exe>' or set UNITY_PATH."
    }

    return (Get-Item -LiteralPath $candidate).FullName
}

function Assert-ProjectLayout {
    $requiredDirectories = @('Assets', 'Packages', 'ProjectSettings')
    foreach ($directory in $requiredDirectories) {
        $path = Join-Path $ProjectRoot $directory
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            throw "Resolved project root '$ProjectRoot' is missing '$directory/'."
        }
    }

    $projectVersionPath = Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt'
    $versionMatch = Select-String -LiteralPath $projectVersionPath `
        -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
    if (-not $versionMatch) {
        throw "Could not read m_EditorVersion from '$projectVersionPath'."
    }

    $projectVersion = $versionMatch.Matches[0].Groups[1].Value.Trim()
    if ($projectVersion -ne $RequiredUnityVersion) {
        throw "Project requires Unity '$projectVersion', but this test script requires " +
            "exactly '$RequiredUnityVersion'."
    }
}

function Assert-UnityVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    $productVersion = (Get-Item -LiteralPath $ExecutablePath).VersionInfo.ProductVersion
    $versionPrefix = $RequiredUnityVersion + '_'
    if ($productVersion -ne $RequiredUnityVersion -and
        -not $productVersion.StartsWith($versionPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Resolved Unity executable reports version '$productVersion'; " +
            "exactly '$RequiredUnityVersion' is required."
    }
}

function Assert-ProjectNotLocked {
    $unityLockPath = Join-Path $ProjectRoot 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $unityLockPath -PathType Leaf) {
        throw "Qubing appears to be open or locked by another Unity process. " +
            "Close the Unity Editor for Qubing and run the script again. " +
            "The script will not stop Unity automatically."
    }
}

function Get-XmlIntegerAttribute {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement]$Element,

        [Parameter(Mandatory = $true)]
        [string[]]$Names,

        [Parameter()]
        [int]$DefaultValue = 0
    )

    foreach ($name in $Names) {
        if ($Element.HasAttribute($name)) {
            $value = 0
            if ([int]::TryParse($Element.GetAttribute($name), [ref]$value)) {
                return $value
            }
        }
    }

    return $DefaultValue
}

function Read-TestResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$XmlPath,

        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    if (-not (Test-Path -LiteralPath $XmlPath -PathType Leaf)) {
        throw "Unity did not create the expected result XML '$XmlPath'. See '$LogPath'."
    }

    try {
        [xml]$document = Get-Content -Raw -LiteralPath $XmlPath
    }
    catch {
        throw "Could not parse result XML '$XmlPath'. See '$LogPath'. $($_.Exception.Message)"
    }

    $root = $document.DocumentElement
    if ($null -eq $root -or $root.Name -ne 'test-run') {
        throw "Result XML '$XmlPath' does not contain an NUnit <test-run> root. See '$LogPath'."
    }

    $total = Get-XmlIntegerAttribute -Element $root -Names @('total', 'testcasecount')
    $passed = Get-XmlIntegerAttribute -Element $root -Names @('passed')
    $failed = Get-XmlIntegerAttribute -Element $root -Names @('failed')
    $skipped = Get-XmlIntegerAttribute -Element $root -Names @('skipped')
    $inconclusive = Get-XmlIntegerAttribute -Element $root -Names @('inconclusive')
    $result = $root.GetAttribute('result')
    $passingResults = @('Passed', 'Pass', 'Success', 'Successful')
    $passedOverall = $failed -eq 0 -and $passingResults -contains $result

    return [pscustomobject]@{
        Total = $total
        Passed = $passed
        Failed = $failed
        Skipped = $skipped
        Inconclusive = $inconclusive
        Result = $result
        PassedOverall = $passedOverall
    }
}

function Test-LogShowsProjectLock {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        return $false
    }

    return [bool](Select-String -LiteralPath $LogPath -Quiet -Pattern @(
        'another Unity instance is running',
        'Multiple Unity instances cannot open the same project',
        'project is already open',
        'UnityLockfile'
    ))
}

function Invoke-TestSuite {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Platform,

        [Parameter(Mandatory = $true)]
        [string]$XmlPath,

        [Parameter(Mandatory = $true)]
        [string]$LogPath,

        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    Remove-Item -LiteralPath $XmlPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $LogPath -Force -ErrorAction SilentlyContinue

    $arguments = @(
        '-batchmode',
        '-runTests',
        '-projectPath', ('"' + $ProjectRoot + '"'),
        '-testPlatform', $Platform,
        '-testResults', ('"' + $XmlPath + '"'),
        '-logFile', ('"' + $LogPath + '"')
    )

    Write-Host ''
    Write-Host "[$Name]"
    $unityProcess = Start-Process `
        -FilePath $ExecutablePath `
        -ArgumentList $arguments `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    $unityExitCode = $unityProcess.ExitCode

    $summary = $null
    if (Test-Path -LiteralPath $XmlPath -PathType Leaf) {
        $summary = Read-TestResult -XmlPath $XmlPath -LogPath $LogPath
        Write-Host "Total: $($summary.Total)"
        Write-Host "Passed: $($summary.Passed)"
        Write-Host "Failed: $($summary.Failed)"
        Write-Host "Skipped: $($summary.Skipped)"
        Write-Host "Inconclusive: $($summary.Inconclusive)"
    }

    if ($unityExitCode -ne 0) {
        if (Test-LogShowsProjectLock -LogPath $LogPath) {
            throw "Unity could not open Qubing because the project is already open or locked. " +
                "Close the Unity Editor for Qubing and run the script again. " +
                "Unity exit code: $unityExitCode. See '$LogPath'."
        }

        throw "Unity exited abnormally while running $Name tests. " +
            "Exit code: $unityExitCode. See '$LogPath'."
    }

    if ($null -eq $summary) {
        $summary = Read-TestResult -XmlPath $XmlPath -LogPath $LogPath
        Write-Host "Total: $($summary.Total)"
        Write-Host "Passed: $($summary.Passed)"
        Write-Host "Failed: $($summary.Failed)"
        Write-Host "Skipped: $($summary.Skipped)"
        Write-Host "Inconclusive: $($summary.Inconclusive)"
    }

    if (-not $summary.PassedOverall) {
        throw "$Name tests did not pass (NUnit result: '$($summary.Result)'). See '$LogPath'."
    }

    Write-Host 'PASS' -ForegroundColor Green
    return $summary
}

try {
    Assert-ProjectLayout
    $resolvedUnityPath = Resolve-UnityExecutable -ExplicitPath $UnityPath
    Assert-UnityVersion -ExecutablePath $resolvedUnityPath

    New-Item -ItemType Directory -Path $ResultsRoot -Force | Out-Null

    $editModeXml = Join-Path $ResultsRoot 'editmode-results.xml'
    $editModeLog = Join-Path $ResultsRoot 'editmode.log'
    $playModeXml = Join-Path $ResultsRoot 'playmode-results.xml'
    $playModeLog = Join-Path $ResultsRoot 'playmode.log'

    @($editModeXml, $editModeLog, $playModeXml, $playModeLog) |
        ForEach-Object {
            Remove-Item -LiteralPath $_ -Force -ErrorAction SilentlyContinue
        }

    Assert-ProjectNotLocked

    Write-Host "[Qubing Tests] Unity: $RequiredUnityVersion"
    Write-Host "Executable: $resolvedUnityPath"
    Write-Host "Project: $ProjectRoot"

    Invoke-TestSuite `
        -Name 'EditMode' `
        -Platform 'EditMode' `
        -XmlPath $editModeXml `
        -LogPath $editModeLog `
        -ExecutablePath $resolvedUnityPath | Out-Null

    Invoke-TestSuite `
        -Name 'PlayMode' `
        -Platform 'PlayMode' `
        -XmlPath $playModeXml `
        -LogPath $playModeLog `
        -ExecutablePath $resolvedUnityPath | Out-Null

    Write-Host ''
    Write-Host '[Qubing Tests] ALL TESTS PASSED' -ForegroundColor Green
    exit 0
}
catch {
    $message = $_.Exception.Message
    $logPath = $null
    if ($message -match "See '([^']+)'" ) {
        $logPath = $Matches[1]
    }

    Fail-QubingTests -Message $message -LogPath $logPath
}
