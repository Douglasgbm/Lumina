# Lumina — escolher a placa (desenho)

Data: 03/10/2026 · Ideia e decisões: Douglas

## O problema

Amigos do Douglas vão usar o Lumina com placas que ninguém sabe quais são. Hoje tudo está amarrado na
MS2109 dele: reconhecer o vídeo e o áudio pelo `VID_534D&PID_2109`, os modos 720p60/1080p30 e a correção
do áudio 96k → 48k.

## Decisões do Douglas

| Tema | Decisão |
|---|---|
| Que placas | **Qualquer coisa que o Windows mostre como câmera** (C) |
| 1ª vez / placa ausente | MS2109 conectada e nada escolhido → usa ela sozinho; senão mostra **"Escolha a placa no menu"** e **não abre nenhuma câmera**; placa escolhida desconectada → aviso, **sem trocar por outra** (A) |
| Modos | O menu mostra os modos **que a placa oferece**, filtrados: 16:9, ≥ 720p, 30 ou 60 fps; padrão 720p60 se existir (A) |
| Som | **Automático**: a entrada do mesmo aparelho USB; reserva manual no menu, com "Nenhuma" (A) |

## Fatos medidos (03/10/2026)

- Link do vídeo e hardware do áudio do mesmo aparelho USB compartilham VID, PID e o "pai" da instância:
  - MS2109: `…usb#vid_534d&pid_2109&mi_00#7&2cbab050&0&0000#…` × `{1}.USB\VID_534D&PID_2109&MI_02\7&2CBAB050&0&0002` → pai `7&2cbab050&0`.
  - C270: `…usb#vid_046d&pid_0825&mi_00#7&33b9e16e&0&0000#…` × `{1}.USB\VID_046D&PID_0825&MI_02\7&33B9E16E&0&0002` → pai `7&33b9e16e&0`.
- Modos que passam no filtro: **C270 → só 720p30** (MJPG e NV12); **MS2109 → 720p30, 720p60, 1080p30, 1080p60**.
  O 1080p60 da MS2109 é **falso** (30 imagens diferentes por segundo, medido de manhã).
- Formatos oferecidos pelas duas: MJPG, NV12, YUY2.

## Peças

| Peça | Responsabilidade |
|---|---|
| **IdUsb** (núcleo) | Extrai VID, PID e o pai da instância de um link de vídeo ou de um caminho de hardware de áudio. Não-USB → nenhum. |
| **EscolhaPlaca** (núcleo) | Dada a lista de câmeras e a escolha salva (link + nome): 1º o link exato; 2º mesmo nome + mesmo VID/PID (trocou de porta USB); nada salvo → MS2109 se houver; senão nenhuma. Diz também **por que** não há placa: "nenhuma escolhida" ou "a escolhida não está conectada". |
| **Modos** (núcleo) | Dos formatos nativos, os modos 16:9, altura ≥ 720, fps 30 ou 60 (arredondado), sem repetição, em ordem; **defeitos conhecidos** escondem o 1080p60 da MS2109. Modo padrão: 720p60 se existir, senão o primeiro. Para um modo, o formato nativo preferido: MJPG > NV12 > YUY2 > qualquer. |
| **ModoVideo** (núcleo) | Passa a ser genérico (largura, altura, fps); o nome continua "720p60", "1080p30"… (configurações antigas continuam valendo). |
| **AudioDaPlaca** (núcleo) | Entrada de som automática = a do mesmo aparelho USB (VID, PID e pai). |
| **Correção 96k → 48k** | Só quando a placa é a MS2109. |
| **Configuração** | Guarda `PlacaLink`, `PlacaNome` e `EntradaSom` (`null` = automático, `"nenhuma"` = sem som, ou o id da entrada). |
| **CapturaVideo** | Recebe a placa a abrir (link) e o modo; publica os modos da placa aberta para o menu. Problemas novos: "nenhuma placa escolhida" e "a placa X não está conectada". |
| **Menu** | "Placa" (lista de câmeras, a atual marcada), modos da placa, "Entrada de som" (Automático / lista / Nenhuma). |

## Andares

| Andar | Entrega | Prova |
|---|---|---|
| 1 – Núcleo | IdUsb, EscolhaPlaca, Modos, AudioDaPlaca, ModoVideo genérico, configuração | testes com os links e modos reais medidos acima |
| 2 – A MS2109 continua igual | abre sozinha, som, modo suave, 720p60 padrão; menu mostra 720p30/720p60/1080p30 (sem o 1080p60 falso) | Douglas jogando |
| 3 – Outra placa de verdade | escolher a **C270**: imagem dela, modo 720p30, microfone dela achado sozinho; voltar para a MS2109 | Douglas |
| 4 – Casos ruins | desplugar a placa escolhida (aviso, sem trocar); "Nenhuma" no som; apagar a escolha salva sem a MS2109 conectada → "Escolha a placa no menu" | Douglas |

## Fora do escopo

Placas PCIe testadas de verdade (o caminho manual cobre o som delas), configurações por placa (cada placa
lembrando seu próprio modo), detectar automaticamente modos falsos de outras placas.
