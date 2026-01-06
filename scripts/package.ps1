param(
    [string]$Configuration = "Release"
)

$project = (Resolve-Path (Join-Path $PSScriptRoot "../SpellCards/SpellCards.csproj")).Path
$artifactsDir = Join-Path $PSScriptRoot "artifacts"
$publishRoot = Join-Path $artifactsDir "publish"

$runtimes = @("win-x64", "linux-x64", "osx-arm64")
$selfContainedOptions = @($false, $true)

if (Test-Path $artifactsDir) {
    Remove-Item $artifactsDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot | Out-Null

foreach ($rid in $runtimes) {
    foreach ($selfContained in $selfContainedOptions) {
        if ($selfContained) {
            $suffix = "selfcontained"
        } else {
            $suffix = "framework"
        }
        $publishDir = Join-Path $publishRoot "$rid-$suffix"
        New-Item -ItemType Directory -Path $publishDir | Out-Null

        if ($selfContained) {
            $scSwitch = "true"
            $trimSwitch = "false"
        } else {
            $scSwitch = "false"
            $trimSwitch = "true"
        }

        & dotnet publish $project `
            -c $Configuration `
            -r $rid `
            --self-contained $scSwitch `
            -p:PublishTrimmed=$trimSwitch `
            -o $publishDir

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $rid ($suffix)."
        }

        $zipName = "spellcards-$rid-$suffix.zip"
        $zipPath = Join-Path $artifactsDir $zipName
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath
        Write-Host "Created $zipName"
    }
}

Write-Host "All packages created in $artifactsDir"
