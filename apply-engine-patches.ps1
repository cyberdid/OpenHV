param(
	[string]$EnginePath = "./engine"
)

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RelativeEnginePath = $EnginePath.Replace("\", "/") -replace "^\./", "" -replace "^/", ""
$ResolvedEnginePath = Join-Path $ProjectDir $RelativeEnginePath
$PatchPath = Join-Path $ProjectDir "engine-patches/openra-headless.patch"
$AiCombatPatchPath = Join-Path $ProjectDir "engine-patches/openra-ai-combat.patch"
$OverlaySource = Join-Path $ProjectDir "engine-patches/OpenRA.Game/Graphics/HeadlessPlatform.cs"
$OverlayTarget = Join-Path $ResolvedEnginePath "OpenRA.Game/Graphics/HeadlessPlatform.cs"
$BotModulesPath = Join-Path $ResolvedEnginePath "OpenRA.Mods.Common/Traits/BotModules"

if (!(Test-Path (Join-Path $ResolvedEnginePath "VERSION")))
{
	throw "Cannot apply engine patches: $ResolvedEnginePath is not an initialized OpenRA SDK."
}

function Invoke-EnginePatch($Patch)
{
	git apply --check "--directory=$RelativeEnginePath" $Patch 2>$null
	if ($LASTEXITCODE -eq 0)
	{
		git apply "--directory=$RelativeEnginePath" $Patch
		if ($LASTEXITCODE -ne 0)
		{
			throw "Failed to apply $Patch."
		}
	}
	else
	{
		git apply --reverse --check "--directory=$RelativeEnginePath" $Patch 2>$null
		if ($LASTEXITCODE -ne 0)
		{
			throw "Engine patch $Patch does not apply cleanly to $ResolvedEnginePath."
		}
	}
}

Push-Location $ProjectDir
try
{
	Invoke-EnginePatch $PatchPath
}
finally
{
	Pop-Location
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OverlayTarget) | Out-Null
Copy-Item -Force $OverlaySource $OverlayTarget

# Not every engine source is valid UTF-8 (AirStates.cs carries ISO-8859 bytes in
# a comment). Decoding those as UTF-8 replaces the offending bytes and rewrites
# the file differently from the Unix path. Latin-1 round-trips all 256 byte
# values, so the rewrite stays byte-exact on both platforms.
$ByteExact = [System.Text.Encoding]::Latin1
Get-ChildItem -Path $BotModulesPath -Filter "*.cs" -Recurse | ForEach-Object {
	$Content = [System.IO.File]::ReadAllText($_.FullName, $ByteExact)
	$UpdatedContent = $Content.Replace(".LocalRandom", ".BotRandom")
	if ($UpdatedContent -ne $Content)
	{
		[System.IO.File]::WriteAllText($_.FullName, $UpdatedContent, $ByteExact)
	}
}

$BotModuleSources = Get-ChildItem -Path $BotModulesPath -Filter "*.cs" -Recurse |
	ForEach-Object { [System.IO.File]::ReadAllText($_.FullName, $ByteExact) }
if (($BotModuleSources -match "\.LocalRandom").Count -gt 0 -or
	($BotModuleSources -match "\.BotRandom").Count -eq 0)
{
	throw "Failed to route OpenRA bot modules through World.BotRandom."
}

# Generated against the BotRandom-routed tree, so it applies after the rewrite.
Push-Location $ProjectDir
try
{
	Invoke-EnginePatch $AiCombatPatchPath
}
finally
{
	Pop-Location
}
