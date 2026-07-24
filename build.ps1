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
    throw '找不到 .NET Framework C# 编译器。'
}
if (-not (Test-Path -LiteralPath $referenceRoot)) {
    throw '请安装 .NET Framework 4.8 Developer Pack。'
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
    ('/out:' + $application),
    ('/win32manifest:' + (Join-Path $sourceRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $sourceRoot 'RightClickGuardian.ico'))
) + $references + $sources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw '应用程序编译失败。'
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
        ('/out:' + $testOutput)
    ) + $testReferences + @($testSource)
    & $compiler @testArguments
    if ($LASTEXITCODE -ne 0) {
        throw ($Name + ' 编译失败。')
    }

    & $testOutput
    if ($LASTEXITCODE -ne 0) {
        throw ($Name + ' 测试失败。')
    }
}

Build-TestHarness 'IntegrationHarness' @()
Build-TestHarness 'ScanHarness' @()
Build-TestHarness 'UiPerformanceHarness' @(
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'WindowsBase.dll',
    'System.Xaml.dll'
)

Write-Host 'All tests passed.'
