Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

foreach ($file in Get-ChildItem -Path "$PSScriptRoot\private\*.ps1") {
    . $file.FullName
}

foreach ($file in Get-ChildItem -Path "$PSScriptRoot\public\*.ps1") {
    . $file.FullName
}
