# Caderno de execução — escolher a placa (04–05/10/2026)

Conclusão de cada task, decisões (Ruling:), revisões e provas.


Spec: docs/superpowers/specs/2026-10-03-lumina-escolher-placa-design.md (lida)
Pré-checagem: T1→T2 (IdUsb, Placa.EhMs2109, MotivoSemPlaca) e T1–T2→T3 (Resolver, Modos, EntradaSom, Problemas) — conferidos na simulação de 03/10 (compila e 87/87); sem conflito.
Execução: Native (escolha do Douglas, 04/10).
Task 1: complete (commits 683e4f1..2bc578b, tests: dotnet test tests/Lumina.Testes → Aprovado!  – Com falha:     0, Aprovado:    77, Ignorado:     0, Total:    77, Duração: 664 ms - Lumina.Testes.dll (net8.0))
Task 2: complete (commits 2bc578b..c826914, tests: dotnet test tests/Lumina.Testes → Aprovado!  – Com falha:     0, Aprovado:    87, Ignorado:     0, Total:    87, Duração: 642 ms - Lumina.Testes.dll (net8.0))
- Task 3: código commitado (build 0/0, 87/87). Prova do andar 2 pendente com o Douglas.
- Task 3: prova do andar 2 APROVADA pelo Douglas ('tudo funcionando, menu de placa aparece'); log 15:57: 'captura aberta: USB Video 1080p30' + áudio MS2109 96k→48k.
Task 3: complete (commits c826914..5aed7e9, tests: dotnet test tests/Lumina.Testes → Aprovado!  – Com falha:     0, Aprovado:    87, Ignorado:     0, Total:    87, Duração: 644 ms - Lumina.Testes.dll (net8.0))
- Task 4: achado na prova da C270 — menu mostrava os modos da placa anterior durante a troca; Douglas clicou 720p30 e virou a preferência da MS2109. Ruling: Iniciar zera ModosDaPlaca/ModoAtual (sem teste automático: estado da captura no app; prova manual) + log 'abrindo/abriu em N ms' para medir os ~20 s da troca para a C270 — custo se errado: menu vazio por alguns segundos durante a troca.
- Extra (05/10): teste 1080p60 × 1080p30 pedido pelo Douglas — 60 = 299 bons + 300 vazios, mesmo ritmo/qualidade dos bons; defeito conhecido mantido. Gravações apagadas.
- Task 4: provas dos andares 3 e 4 feitas pelo Douglas (C270, USB, Nenhuma); 'escolhida desconectada ao abrir' PENDENTE por decisão dele (cabo de difícil acesso), coberta por teste. Tempos: C270 7,7 s, MS2109 3,7–4,3 s (8,9 s na reconexão). Melhoria anotada: mostrar 'Abrindo X...' durante a abertura.
Task 4: complete (commits 5aed7e9..9914b3b, tests: dotnet test tests/Lumina.Testes → Aprovado!  – Com falha:     0, Aprovado:    87, Ignorado:     0, Total:    87, Duração: 648 ms - Lumina.Testes.dll (net8.0))
Final review: revisor opus (contexto limpo), 0 Critical, 3 Important, 8 Minor.
- Final: fixed Important 1 (duas placas do mesmo modelo → pegava a outra) — Duas_placas_do_mesmo_modelo_e_a_escolhida_sumiu_nao_pega_a_outra RED (Actual = a outra placa 7&99aa11bb) → GREEN; suíte 89/89.
- Final: fixed Important 3 (câmera sem modo útil reaberta a cada 2 s) — So_camera_sem_modo_util_e_problema_permanente RED (compilação) → GREEN + captura para de reabrir; suíte 89/89.
- Final: Ruling: Important 2 (outra porta USB com o app aberto → som mudo) corrigido SEM teste automático — captura publica PlacaAberta; o vigia compara o link e reinicia o áudio. Exige hardware para provar (mudar a MS2109 de porta com o app aberto) — custo se errado: o caso continua mudo até reabrir.
- Final: minor (deferred): aviso da placa anterior aparece durante a abertura da nova (zerar _problema no Iniciar; casa com "Abrindo X...").
- Final: minor (deferred): corrida rara — modos da placa antiga republicados depois do Iniciar zerá-los.
- Final: minor (deferred): "captura: abrindo X" dobra as linhas do log com a placa desplugada.
- Final: minor (deferred): escolher pelo menu uma placa que sumiu deixa o menu com modos velhos; Revezamento.Parar descarta _fio se o join estoura.
- Final: minor (deferred): com entrada de som manual e sem placa, o microfone toca nas caixas.
- Final: minor (deferred): entrada escolhida que sumiu — o menu não marca nada.
- Final: minor (deferred): clicar na placa já aberta reinicia imagem e som.
- Final: minor (deferred): placa escolhida desplugada durante o jogo mostra a mensagem genérica, não a com o nome.
- Pedido do Douglas (05/10): 'pode fazer os achados menores, ajeita tudo' → os 8 menores desta revisão + os 7 da revisão anterior + 'Abrindo X…'.
- Minors (núcleo): Parar_que_estourou_o_tempo_ainda_faz_o_proximo_esperar RED→GREEN; Coordenada_gigante_do_config_nao_estoura… RED→GREEN; Barra_de_titulo_fora_da_tela_conta_como_perdida — 1ª versão passou sem correção (testava caso já coberto), reescrita com 120 px visíveis → RED→GREEN; Posicao_absurda_e_limitada RED→GREEN; Abrindo/NaTela (RED de compilação → GREEN). 96/96.
- Minors (app, 05/10) aplicados SEM teste automático (threads, COM, áudio e janela; build 0/0, 96/96): Abrindo X…; NaTela na janela; publicação/zeragem sob _travaEstado (Iniciar avisa dentro da trava; Parar espera FORA dela — a 1ª versão esperava dentro e travaria até 3 s, visto na releitura); log por mudança + rotação 5 MB + catch geral; Parar zera menu; clique na placa aberta é no-op; ReiniciarAudio sem placa para o som; menu marca entrada/saída sumida; OnDeviceStateChanged religa saída fixa/entrada escolhida; GPU perdida → RecriarTela; tela cheia sai por GarantirVisivel; Maximizada lembra _ultimoEstadoVisivel; OnHandleCreated idempotente. Ruling: sem teste automático nesses (exigem hardware/driver) — custo se errado: regressão só aparece na prova do Douglas.
- Revisão focada dos minors (opus): 0 Critical, 3 Important (GPU perdida não detectada no menor atraso; RecriarTela sem proteção; entrada religada sem placa tocava) + 7 Minor — todos corrigidos em 50571e3, build 0/0, 96/96, sem teste automático (hardware/driver). Ruling: GPU perdida segue sem prova real — não há como provocar a remoção do driver aqui; custo se errado: 'sem sinal' até reabrir, como antes.
- Prova final (Douglas, 05/10 21:42–21:43): 'testei tudo, funcionou'. Log confirma USB Video→C270 (7761 ms)→USB Video (3756 ms) com 'abrindo/abriu em'; USB tirado e outra porta NÃO aparecem no log desta versão (não confirmados por log). Merge autorizado.
