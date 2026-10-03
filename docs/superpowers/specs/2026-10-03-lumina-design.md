# Lumina — desenho

Data: 03/10/2026 · Autor da ideia e das decisões: Douglas

## O problema

O setup tinha um splitter de HDMI para levar o Switch 2 ao segundo monitor. Ele queimou duas vezes.
A placa de captura já recebe o sinal, mas para ver a imagem era preciso abrir o OBS e montar um
projetor de janela toda vez — e o OBS prende a webcam (Logi C270), que fica inutilizável em outros apps.

## O que o Lumina é

Um app de Windows que mostra a imagem e toca o som da placa de captura numa janela, com o menor
atraso possível, para **jogar o Switch 2 olhando pela janela**. Abre só a placa; a webcam fica livre.

## Fatos medidos (03/10/2026)

- Placa: **MacroSilicon MS2109** — `USB\VID_534D&PID_2109`; vídeo "USB Video" (MI_00), áudio
  "USB Digital Audio" (MI_02). USB 2.0, entrega MJPEG.
- Webcam presente: "Logi C270 HD WebCam" — nunca pode ser aberta pelo Lumina.
- Máquina: .NET SDK 8.0.425, ffmpeg/ffplay 8.1.1, Python 3.12.10.
- O Switch 2 já aparecia pela placa no OBS (sem bloqueio de HDCP).

## Suposições (a confirmar nos andares)

- A MS2109 soma ~50–100 ms de atraso por conta própria; o Lumina não remove isso, só não soma mais.
- O áudio da MS2109 é anunciado pelo Windows como 96 kHz mono, mas é 48 kHz estéreo.
- Modos úteis da placa: 720p60 e 1080p30 em MJPEG.

## Decisões do Douglas

| Tema | Decisão |
|---|---|
| Uso | Jogar (latência é crítica) |
| Resolução | Botão para trocar; começa em **720p60**, alternativa 1080p30 |
| Janela | Lembra a última vez: monitor, posição, tamanho, tela cheia |
| Som | Segue a saída padrão do Windows, mas dá para fixar um dispositivo; volume e mudo |
| Tecnologia | C# nativo (.NET 8), Media Foundation + Direct3D 11 + WASAPI |
| Andar 0 | Medir o `ffplay` antes e usar como régua |

## Peças

| Peça | Responsabilidade |
|---|---|
| **Escolha do dispositivo** | Acha a placa por `VID_534D&PID_2109` (vídeo e áudio); nunca escolhe outra câmera. Lógica pura, testável. |
| **Captura de vídeo** | Media Foundation SourceReader no modo pedido (MJPEG 720p60 ou 1080p30), decodificação na GPU (D3D11). |
| **Tela** | Swapchain D3D11 em flip model; desenha sempre o quadro mais novo e descarta os atrasados; mantém proporção 16:9 com barras pretas. |
| **Áudio** | Captura WASAPI da placa → correção 96k mono → 48k estéreo (lógica pura, testável) → reprodução WASAPI com buffer de ~20–30 ms; volume e mudo; segue o padrão do Windows ou um dispositivo fixo. |
| **Memória** | `%APPDATA%\Lumina\config.json`: monitor, posição, tamanho, tela cheia, modo, dispositivo de som, volume, mudo. Arquivo ausente ou corrompido → padrões, sem quebrar. Janela fora de qualquer monitor atual → volta ao principal. |
| **Controles** | Clique direito = menu (modo, saída de som, volume, mudo). F11 ou clique duplo = tela cheia. M = mudo. Nada desenhado por cima da imagem. |
| **Sem sinal** | Placa desconectada ou sem sinal → tela "sem sinal" e nova tentativa periódica, sem fechar o app. |

Bibliotecas: **Vortice.Windows** (D3D11 / Media Foundation / DXGI) e **NAudio** (WASAPI). Ambas MIT.

## Andares

| Andar | Entrega | Prova |
|---|---|---|
| 0 – Régua | Latência do `ffplay` com a MS2109 | HDMI do PC na placa, cronômetro em ms; tela real e janela capturada no mesmo print; diferença = atraso. Várias amostras, mediana. |
| 1 – Imagem | Janela mostrando 720p60 | fps medido = 60; latência ≤ régua do andar 0 (mesmo método). |
| 2 – Som | Áudio corrigido tocando | Tom de 1 kHz saindo do PC pela placa; frequência medida no que o Lumina captura = 1 kHz. Sincronia: ouvido do Douglas. |
| 3 – Controles | Troca de modo, menu, saída, volume, mudo | Testes automáticos da lógica; cliques testados pelo Douglas. |
| 4 – Memória | Estado lembrado | Testes automáticos (salvar/ler, arquivo corrompido, monitor que sumiu); abrir/fechar no 2º monitor pelo Douglas. |
| 5 – Sem sinal | Reconexão sozinha | Douglas tira o USB e desliga o Switch; app não fecha e volta sozinho. |

Teste automático transversal: a escolha de dispositivo nunca retorna a C270.

## Fora do escopo

Gravação, transmissão, sobreposições, filtros, múltiplas placas, outros sistemas além do Windows.
