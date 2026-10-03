# Conta quadros DIFERENTES que a placa entrega (mpdecimate descarta repetidos).
# A fonte precisa mudar a cada quadro: deixe o cronometro.html aberto na tela capturada.
param([string]$Tamanho = '1280x720', [int]$Fps = 60, [int]$Segundos = 6)
$linha = ffmpeg -hide_banner -f dshow -rtbufsize 64M -vcodec mjpeg -video_size $Tamanho -framerate $Fps `
    -i video="USB Video" -t $Segundos -an -vf 'mpdecimate=hi=64:lo=32:frac=0.1' -f null - 2>&1 |
    Select-String 'frame=' | Select-Object -Last 1
if ($linha -match 'frame=\s*(\d+)') { '{0} pedido a {1}: {2:F1} quadros diferentes por segundo' -f $Tamanho, $Fps, ([int]$Matches[1] / $Segundos) }
else { "sem quadros (placa sem sinal?)" }
