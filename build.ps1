# Сборка CPULogicPet.exe встроенным в Windows компилятором C# (.NET Framework 4).
# Запуск: правый клик → «Выполнить с помощью PowerShell», или в консоли: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path "$fw\csc.exe")) { $fw = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319' }
$speech = Get-ChildItem 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Speech' -Recurse -Filter System.Speech.dll |
    Select-Object -First 1 -ExpandProperty FullName

# картинки и тексты, которые зашиваются внутрь exe
$resources = 'tama-open.png', 'tama-closed.png', 'cut-open.png', 'cut-closed.png',
             'eva-open.png', 'eva-closed.png', 'eva-cut-open.png', 'eva-cut-closed.png',
             'tray.png', 'phrases-loji.txt', 'phrases-eva.txt', 'dialogs.txt'

$cscArgs = @('/nologo', '/target:winexe', '/optimize+', "/out:$proj\CPULogicPet.exe", "/win32icon:$proj\pet.ico",
    "/lib:$fw\WPF", '/r:PresentationFramework.dll', '/r:PresentationCore.dll', '/r:WindowsBase.dll', '/r:System.Xaml.dll',
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', "/r:$speech")
$cscArgs += $resources | ForEach-Object { "/resource:$proj\$_,$_" }
$cscArgs += "$proj\Program.cs"

& "$fw\csc.exe" $cscArgs
if ($LASTEXITCODE -eq 0) { Write-Host "Готово: $proj\CPULogicPet.exe" -ForegroundColor Green }
else { Write-Host "Сборка не удалась (код $LASTEXITCODE)" -ForegroundColor Red }
