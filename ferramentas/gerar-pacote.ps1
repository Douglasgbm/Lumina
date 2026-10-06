# Gera o pacote para quem só quer usar: um Lumina.exe único, com o .NET embutido (não precisa instalar nada),
# zipado com o LEIA-ME. Uso:  pwsh -File ferramentas\gerar-pacote.ps1 -Versao 1.0.0
param([Parameter(Mandatory)][string]$Versao)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$saida = Join-Path $raiz "pacote\Lumina-$Versao"
Remove-Item (Join-Path $raiz 'pacote') -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish (Join-Path $raiz 'src\Lumina') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:Version=$Versao -o $saida
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou' }

@"
Lumina $Versao — mostra a placa de captura numa janela, para jogar o console pelo PC.

COMO USAR
1. Ligue a placa de captura no USB e abra o Lumina.exe (não precisa instalar nada).
2. Clique com o botão direito na janela: Placa, modo (720p60, 1080p30...), modo suave × menor atraso,
   entrada e saída de som, volume, mudo, tela cheia.
3. F11 ou clique duplo: tela cheia. Esc sai. M: mudo.

SE APARECER "Acesso à câmera negado"
Alguns antivírus (ex.: Norton) isolam um programa novo na primeira vez que ele abre. Feche e abra de novo.
Se continuar: Configurações do Windows > Privacidade > Câmera > permitir apps da área de trabalho.

A placa aceita um programa por vez: com o OBS ou outro usando a placa, o Lumina espera e volta sozinho.
Configuração e log ficam em %APPDATA%\Lumina.

Código e ajuda: https://github.com/Douglasgbm/Lumina
"@ | Set-Content -Encoding utf8 (Join-Path $saida 'LEIA-ME.txt')

$zip = Join-Path $raiz "pacote\Lumina-$Versao-windows-x64.zip"
Compress-Archive -Path (Join-Path $saida '*') -DestinationPath $zip
Get-Item $zip | Select-Object Name, Length
