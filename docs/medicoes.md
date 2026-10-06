# Medições

## Andar 0 — régua (03/10/2026 11:07–11:18)

A bancada do plano (HDMI do PC na placa + cronômetro) não pôde ser montada: há um só cabo HDMI,
do dock do Switch direto na MS2109. Decisão do Douglas: provar o andar 1 por medição interna (B)
e teste de uso, sem atraso absoluto.

Fonte: Switch 2 no menu, medido com `ferramentas/regua/fps.ps1` (quadros diferentes por segundo).

| Medida | Valor |
|---|---|
| fps real 1280x720 pedido a 60 | 60,0 |
| fps real 1920x1080 pedido a 60 | 30,0 — a placa anuncia 60 e repete cada quadro |
| fps real 1920x1080 pedido a 30 | 30,2 |
| atraso absoluto da placa | não medido (sem 2º cabo); suposição do desenho: 50–100 ms |

Outros fatos medidos:
- Sem sinal (Switch desligado) a MS2109 continua mandando ~60 quadros por segundo.
- A placa aceita um programa por vez: com o Lumina aberto o ffplay falha ("already in use");
  com o ffplay aberto o Lumina recebe `MF_E_HW_MFT_FAILED_START_STREAMING` e tenta de novo a cada 2 s.

## Andar 1 — Lumina só imagem (03/10/2026 11:38 e teste de uso ~11:45)

`--diagnostico`, Switch ligado, 720p60. O atraso é do tempo da amostra (início da chegada do
quadro pelo USB, relógio QPC) até o `Present`; a placa marca o fim da chegada ~16 ms depois.

| Medida | Valor |
|---|---|
| fps no diagnóstico (720p60) | 59,5–60,5 |
| atraso chegada→tela, mediana | 17,6–20,0 ms |
| atraso chegada→tela, p95 | 27–39 ms |
| atraso chegada→tela, máximo | 37–66 ms (66 no primeiro bloco) |
| parte só do Lumina (descomprimir + copiar + apresentar) | ≈ 2–4 ms |
| fechar com o Switch desligado | thread termina ~0,3 s após o X (3 de 3) |
| teste de uso (Douglas, Monster Hunter) | "fluindo bem, não vejo problemas" |

## Andar 2 — som (03/10/2026 11:48–11:52)

Teste de tom (1000/1500 Hz) impossível sem o HDMI do PC na placa; decisão: prova de ouvido com o Switch.

| Medida | Valor |
|---|---|
| formato anunciado → usado (lumina.log) | float32 96000 Hz 1 canal → float32 48000 Hz 2 canais |
| saída | Alto-falantes (Realtek(R) Audio), a padrão do Windows |
| canais esquerdo × direito (diagnóstico, som do jogo) | diferentes entre si o tempo todo (ex.: 1253 × 994 Hz), coerente com estéreo real |
| ouvido do Douglas | "som normal e junto com a imagem" |

## Andar 5 — sem sinal e reconexão (03/10/2026 ~12:05)

| Situação | Resultado |
|---|---|
| Switch desligado e religado | imagem volta sozinha (Douglas) |
| USB da placa tirado e recolocado | erro às 12:05:33 nos dois; áudio volta 12:05:41, vídeo 12:05:48, sem fechar nada |
| Webcam com o Lumina aberto | funciona (Douglas) |
| Headset fixado desligado no meio | **pendente** (não testado) |
| Volta sozinho ao fechar o ffplay | **pendente**; parcial às 11:41: com o ffplay aberto o Lumina recebe `MF_E_HW_MFT_FAILED_START_STREAMING` e tenta de novo a cada 2 s |

## Correção — a câmera negada era o Norton (03/10/2026 ~13:40)

O `E_ACCESSDENIED` que aparecia em algumas aberturas **não** vinha do terminal do Claude: o log do
Auto-Sandbox do Norton 360 (`C:\ProgramData\Norton\Antivirus\log\autosandbox.log`) mostra que cada
`Lumina.exe` recompilado roda isolado ("Sandboxing (no custody)") na 1ª abertura — e isolado não usa a
câmera. Nas seguintes entra na lista de exceções e funciona. Bate com todas as recusas (10:16, 10:51,
11:31–11:35, 13:16) e todos os sucessos do dia. Às 13:37 a câmera abriu inclusive pelo terminal do Claude.
A tela "sem sinal" agora diz "Acesso à câmera negado" e sugere abrir de novo.

## O "degrau de +18 ms" do modo suave era a régua (03/10/2026 14:10–14:24, 1080p30, Diablo 4)

Medida a chegada contra os 3 carimbos de cada quadro (MF, `DeviceTimestamp` e `{d06f8a46-…}` da placa):
às 14:12:01 só o do **MF** pulou (chegada 36,7 → 46 ms); os dois da placa ficaram parados
(dispositivo ~35–37 ms, d06 ~3–5 ms = fim da chegada) por 13 min. O MF reancora o carimbo que ele fabrica.

| Período | Exibição contra o MF | Exibição contra a PLACA | Fila |
|---|---|---|---|
| 14:11–14:12 (antes) | 62,9 ms | **68,7 ms** | 55 ms |
| 14:12–14:16 (depois) | 66,1 ms | **62,5 ms** | 57 ms |

Atraso real não subiu. Em 1080p30 o modo suave fica ~62–69 ms depois do início do quadro na placa
(o quadro de 1080p leva ~33 ms para chegar pelo USB). Melhoria possível, não feita: agendar pelo carimbo da placa.

## O "1080p60" da MS2109 são 30 quadros bons + 30 vazios (05/10/2026, Rise em movimento)

O OBS do Douglas pede 1080p60 (MJPEG, Buffering "Detecção automática" = sem fila). Medido com ffmpeg
(`-use_wallclock_as_timestamps`, 10 s em cada modo, Lumina fechado):

| Quadros bons (> 20 KB) | 1080p "60" | 1080p30 |
|---|---|---|
| por segundo | 30,0 | 30,1 |
| tamanho mediano | ~170 KB | ~162 KB |
| desvio do intervalo | 7,3 ms | 6,6 ms |
| no ritmo (33 ± 8 ms) | 85% | 86% |

No "60" chegam 600 pacotes: 299 bons e 300 vazios/quebrados (o menor com 4 bytes; o decodificador
diz "No JPEG data found in image"). Mesmo ritmo e mesma qualidade do 1080p30 → continua escondido no menu.

## Escolher a placa — provas (04–05/10/2026)

| Prova | Resultado |
|---|---|
| Andar 2: MS2109 sem placa salva | abre sozinha; menu "Placa" com a USB Video marcada; modos 720p30/720p60/1080p30; som 96k→48k |
| Andar 3: C270 pelo menu | imagem dela em 720p30, microfone dela achado sozinho (48 kHz estéreo, sem correção); volta para a USB Video sem fechar |
| Tempo de abertura | C270 **7,7 s** (começa no clique); MS2109 3,7–4,3 s; MS2109 após reconectar o USB 8,9 s |
| USB da escolhida tirado e recolocado | aviso, tentativas a cada ~2 s, volta sozinha (áudio e vídeo) |
| Entrada de som "Nenhuma" | sem som; "áudio: sem som (escolhido no menu)" registrado uma vez só |
| Escolhida desconectada ao abrir | **pendente** (cabo da C270 de difícil acesso); coberto pelo teste `A_escolhida_desconectada_nao_troca_por_outra_nem_pela_MS2109` |

## 1080p60 liberado na MS2109 (05/10/2026)

Decisão do Douglas: mostrar o 1080p60 no menu, sem aviso, mesmo sabendo que a placa entrega 30 quadros bons.
Testado por ele: "funcionando normal". Log: abriu em ~4 s, nenhum erro do decodificador com os pacotes vazios;
diagnóstico com fps=60,0 na tela (o Windows repete o quadro no lugar do vazio).
