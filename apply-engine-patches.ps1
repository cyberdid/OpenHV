param(
	[string]$EnginePath = "./engine"
)

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RelativeEnginePath = $EnginePath.Replace("\", "/") -replace "^\./", "" -replace "^/", ""
$ResolvedEnginePath = Join-Path $ProjectDir $RelativeEnginePath
$PatchPath = Join-Path $ProjectDir "engine-patches/openra-headless.patch"
$OverlaySource = Join-Path $ProjectDir "engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs"
$OverlayTarget = Join-Path $ResolvedEnginePath "OpenRA.Game/Graphics/HeadlessPlatform.cs"
$BotModulesPath = Join-Path $ResolvedEnginePath "OpenRA.Mods.Common/Traits/BotModules"

if (!(Test-Path (Join-Path $ResolvedEnginePath "VERSION")))
{
	throw "Cannot apply engine patches: $ResolvedEnginePath is not an initialized OpenRA SDK."
}

Push-Location $ProjectDir
try
{
	git apply --check "--directory=$RelativeEnginePath" $PatchPath 2>$null
	if ($LASTEXITCODE -eq 0)
	{
		git apply "--directory=$RelativeEnginePath" $PatchPath
		if ($LASTEXITCODE -ne 0)
		{
			throw "Failed to apply $PatchPath."
		}
	}
	else
	{
		git apply --reverse --check "--directory=$RelativeEnginePath" $PatchPath 2>$null
		if ($LASTEXITCODE -ne 0)
		{
			throw "Engine patch does not apply cleanly to $ResolvedEnginePath."
		}
	}
}
finally
{
	Pop-Location
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OverlayTarget) | Out-Null
Copy-Item -Force $OverlaySource $OverlayTarget

$Utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
Get-ChildItem -Path $BotModulesPath -Filter "*.cs" -Recurse | ForEach-Object {
	$Content = [System.IO.File]::ReadAllText($_.FullName)
	$UpdatedContent = $Content.Replace(".LocalRandom", ".BotRandom")
	if ($UpdatedContent -ne $Content)
	{
		[System.IO.File]::WriteAllText($_.FullName, $UpdatedContent, $Utf8WithoutBom)
	}
}

$BotModuleSources = Get-ChildItem -Path $BotModulesPath -Filter "*.cs" -Recurse |
	ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }
if (($BotModuleSources -match "\.LocalRandom").Count -gt 0 -or
	($BotModuleSources -match "\.BotRandom").Count -eq 0)
{
	throw "Failed to route OpenRA bot modules through World.BotRandom."
}
