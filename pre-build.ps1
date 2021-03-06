# https://docs.microsoft.com/en-us/azure/devops/pipelines/scripts/powershell?view=azure-devops

function Log([string]$text) {
    Write-Warning $text
}

# If this script is not running on a build server, remind user to
# set environment variables so that this script can be debugged
if (-not ($Env:BUILD_SOURCESDIRECTORY -and $Env:BUILD_BUILDNUMBER)) {
    Write-Warning "Executing locally, setting dummy values"
    $Env:BUILD_SOURCESDIRECTORY = (Get-Location).Path
    $Env:BUILD_BUILDNUMBER = 222
    $Env:BUILD_SOURCEBRANCHNAME = "local"
}

# Make sure path to source code directory is available
if (-not $Env:BUILD_SOURCESDIRECTORY) {
    Write-Error ("BUILD_SOURCESDIRECTORY environment variable is missing.")
    exit 1
}
elseif (-not (Test-Path $Env:BUILD_SOURCESDIRECTORY)) {
    Write-Error "BUILD_SOURCESDIRECTORY does not exist: $Env:BUILD_SOURCESDIRECTORY"
    exit 1
}
Log "BUILD_SOURCESDIRECTORY: $Env:BUILD_SOURCESDIRECTORY"

# Make sure there is a build number
if (-not $Env:BUILD_BUILDNUMBER) {
    Write-Error ("BUILD_BUILDNUMBER environment variable is missing.")
    exit 1
}
Log "BUILD_BUILDNUMBER: $Env:BUILD_BUILDNUMBER"

Log "BUILD_SOURCEBRANCHNAME: $Env:BUILD_SOURCEBRANCHNAME"

$sourcesDirectory = $env:BUILD_SOURCESDIRECTORY

function Set-NodeValue($rootNode, [string]$nodeName, [string]$value) {
    $nodePath = "PropertyGroup/$($nodeName)"

    $node = $rootNode.Node.SelectSingleNode($nodePath)

    if ($null -eq $node) {
        $group = $rootNode.Node.SelectSingleNode("PropertyGroup")
        $node = $group.OwnerDocument.CreateElement($nodeName)
        $group.AppendChild($node) | Out-Null
    }

    $node.InnerText = $value

    Log "Set $($nodeName) to $($value)"
}

# We assume that BUILDNUMBER is '20190723.8'
$buildNumber = $Env:BUILD_BUILDNUMBER
$majorVersion = $buildNumber.Substring(2, 2)
$minorVersion = $buildNumber.Substring(4, 2)
$build = $buildNumber.Substring(6, 2)
$revision = $buildNumber.Substring(9)
$actualVersion = "$($majorVersion).$($minorVersion).$($build).$($revision)"
$informativeVersion = "$($actualVersion)-$($Env:BUILD_SOURCEBRANCHNAME)"

Get-ChildItem -Path $sourcesDirectory -Filter "*.csproj" -Recurse -File |
Where-Object { $_.FullName -like "*OpenKh*" } |
ForEach-Object {
    Log "Patching $($_.FullName)"

    $projectPath = $_.FullName
    $project = Select-Xml $projectPath -XPath "//Project"

    Set-NodeValue $project "AssemblyVersion" $actualVersion
    Set-NodeValue $project "FileVersion" $actualVersion
    Set-NodeValue $project "Version" $informativeVersion
    Set-NodeValue $project "InformationalVersion" $informativeVersion
    Set-NodeValue $project "Authors" "OpenKh contributors"
    Set-NodeValue $project "Company" "OpenKh"
    Set-NodeValue $project "Copyright" "Copyright (C) OpenKh $($date.Year)"
    Set-NodeValue $project "Description" "https://github.com/Xeeynamo/OpenKh"

    $document = $project.Node.OwnerDocument
    $document.PreserveWhitespace = $true

    $document.Save($projectPath)
}