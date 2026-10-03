# Tira N prints da tela principal inteira, em pixels reais.
param([int]$Quantas = 10, [string]$Pasta = "$PSScriptRoot\fotos")
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Name Dpi -Namespace Lumina -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[void][Lumina.Dpi]::SetProcessDPIAware()
New-Item -ItemType Directory -Force $Pasta | Out-Null
$tela = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
for ($i = 1; $i -le $Quantas; $i++) {
    $bmp = New-Object System.Drawing.Bitmap $tela.Width, $tela.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($tela.Location, [System.Drawing.Point]::Empty, $tela.Size)
    $bmp.Save((Join-Path $Pasta ('foto{0:D2}.png' -f $i)))
    $g.Dispose(); $bmp.Dispose()
    Start-Sleep -Milliseconds 700
}
"$Quantas prints em $Pasta"
