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
