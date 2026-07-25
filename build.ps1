param(
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src\RightClickGuardian'
$artifactRoot = Join-Path $projectRoot 'artifacts'
$referenceRoot = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found.'
}
if (-not (Test-Path -LiteralPath $referenceRoot)) {
    throw 'Install the .NET Framework 4.8 Developer Pack.'
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

$references = @(
    'mscorlib.dll',
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'WindowsBase.dll',
    'System.Xaml.dll',
    'System.Core.dll',
    'System.dll',
    'System.Xml.dll',
    'System.Xml.Linq.dll',
    'System.Runtime.Serialization.dll',
    'System.Management.dll',
    'System.Windows.Forms.dll',
    'System.Drawing.dll',
    'Microsoft.CSharp.dll'
) | ForEach-Object { '/reference:' + (Join-Path $referenceRoot $_) }

$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' |
    ForEach-Object { $_.FullName }
$application = Join-Path $artifactRoot 'RightClickGuardian.exe'
$arguments = @(
    '/nologo',
    '/noconfig',
    '/nostdlib+',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/langversion:5',
    '/codepage:65001',
    ('/out:' + $application),
    ('/win32manifest:' + (Join-Path $sourceRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $sourceRoot 'RightClickGuardian.ico'))
) + $references + $sources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw 'Application compilation failed.'
}

Write-Host ('Built: ' + $application)

if (-not $RunTests) {
    return
}

function Build-TestHarness {
    param(
        [string]$Name,
        [string[]]$AdditionalReferences
    )

    $testSource = Join-Path $projectRoot ('tests\' + $Name + '.cs')
    $testOutput = Join-Path $artifactRoot ($Name + '.exe')
    $testReferences = @(
        ('/reference:' + (Join-Path $referenceRoot 'mscorlib.dll')),
        ('/reference:' + (Join-Path $referenceRoot 'System.Core.dll')),
        ('/reference:' + (Join-Path $referenceRoot 'System.dll')),
        ('/reference:' + (Join-Path $referenceRoot 'Microsoft.CSharp.dll')),
        ('/reference:' + $application)
    )
    foreach ($reference in $AdditionalReferences) {
        $testReferences += '/reference:' + (Join-Path $referenceRoot $reference)
    }

    $testArguments = @(
        '/nologo',
        '/noconfig',
        '/nostdlib+',
        '/target:exe',
        '/platform:anycpu',
        '/optimize+',
        '/langversion:5',
        '/codepage:65001',
        ('/out:' + $testOutput)
    ) + $testReferences + @($testSource)
    & $compiler @testArguments
    if ($LASTEXITCODE -ne 0) {
        throw ($Name + ' compilation failed.')
    }

    & $testOutput
    if ($LASTEXITCODE -ne 0) {
        throw ($Name + ' tests failed.')
    }
}

Build-TestHarness 'IntegrationHarness' @()
Build-TestHarness 'ScanHarness' @()
Build-TestHarness 'SoftwareCatalogHarness' @()
Build-TestHarness 'NavigationHarness' @(
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'WindowsBase.dll',
    'System.Xaml.dll'
)
Build-TestHarness 'TrayVisualHarness' @(
    'System.Drawing.dll',
    'System.Windows.Forms.dll'
)
Build-TestHarness 'UiPerformanceHarness' @(
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'WindowsBase.dll',
    'System.Xaml.dll'
)

Write-Host 'All tests passed.'
