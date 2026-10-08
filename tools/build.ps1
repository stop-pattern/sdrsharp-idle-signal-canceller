param(
    [Parameter(Mandatory = $true)][string]$SdrSharpPath,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = (Resolve-Path -LiteralPath $SdrSharpPath).Path
Set-Location -LiteralPath $repoRoot
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet-home'
$env:DOTNET_NOLOGO = '1'
$usePythonLauncher = [bool](Get-Command py -ErrorAction SilentlyContinue)
$referenceDir = $null

if (Test-Path -LiteralPath $source -PathType Container) {
    if (Test-Path -LiteralPath (Join-Path $source 'SDRSharp.Common.dll')) { $referenceDir = $source }
    else {
        $candidate = Join-Path $source 'SDRSharp.dotnet9.exe'
        if (!(Test-Path -LiteralPath $candidate)) { $candidate = Join-Path $source 'SDRSharp.exe' }
        $source = (Resolve-Path -LiteralPath $candidate).Path
    }
}
if (!$referenceDir) {
    $referenceDir = Join-Path $repoRoot 'vendor/references'
    if ($usePythonLauncher) { py -3 tools/extract_bundle.py $source $referenceDir }
    else { python tools/extract_bundle.py $source $referenceDir }
    if ($LASTEXITCODE -ne 0) { throw 'Reference extraction failed' }
}

dotnet build IdleSignalCanceller.sln -c $Configuration "-p:SdrSharpDir=$referenceDir" --disable-build-servers -m:1 --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet "tests/IdleSignal.Tests/bin/$Configuration/net9.0/IdleSignal.Tests.dll"
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
dotnet "tests/IdleSignal.IntegrationTests/bin/$Configuration/net9.0-windows/IdleSignal.IntegrationTests.dll"
if ($LASTEXITCODE -ne 0) { throw 'Host integration tests failed' }
# unittest normally reports success on stderr; Windows PowerShell 5 treats that
# as NativeCommandError when the build output is redirected. Report on stdout.
$pythonTests = "import sys,unittest; result=unittest.TextTestRunner(stream=sys.stdout,verbosity=2).run(unittest.defaultTestLoader.discover('tests',pattern='test_*.py'));sys.exit(not result.wasSuccessful())"
if ($usePythonLauncher) { py -3 -c $pythonTests }
else { python -c $pythonTests }
if ($LASTEXITCODE -ne 0) { throw 'Bundle tests failed' }

# New staging directory prevents stale or third-party files entering the archive.
$staging = Join-Path $repoRoot ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
$plugins = Join-Path $staging 'Plugins'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
$pluginOutput = "src/SDRSharp.IdleSignalCanceller/bin/$Configuration/net9.0-windows"
foreach ($name in @('SDRSharp.IdleSignalCanceller.dll', 'SDRSharp.IdleSignalCanceller.Core.dll')) {
    Copy-Item -LiteralPath (Join-Path $pluginOutput $name) -Destination (Join-Path $plugins $name)
}
Copy-Item -LiteralPath README_JA.md -Destination (Join-Path $staging 'README_JA.md')
$archive = Join-Path $repoRoot 'artifacts/SDRSharp.IdleSignalCanceller-1.0.0.zip'
$entries = Get-ChildItem -LiteralPath $staging | ForEach-Object { $_.FullName }
Compress-Archive -LiteralPath $entries -DestinationPath $archive -Force
Write-Output "Package: $archive"
