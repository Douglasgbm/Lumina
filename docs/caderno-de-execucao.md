# Caderno de execução — Lumina (03/10/2026)

Registro feito durante a execução do plano: conclusão de cada task, decisões tomadas (Ruling:) e achados.


Spec: docs/superpowers/specs/2026-10-03-lumina-design.md (lida)

## Pré-checagem de interfaces
- T1→T2: ModoVideo(Nome,Largura,Altura,Fps)+Hd60/FullHd30/PorNome/Alternar; T2 testa os mesmos nomes — ok
- T1/T2→T3: Placa.EscolherVideo/EscolherFormatoNativo, FormatoNativo, Enquadramento.Encaixar, ContadorQuadros — ok
- T4→T5: CorrecaoMs2109/FormatoPcm/FilaAudio.DeveLimpar/SaidaAudio.Resolver; T5 usa os mesmos — ok
- T3/T5→T6: CapturaVideo.Iniciar, MotorAudio.Saidas/SaidaFixaId/Volume/Mudo — ok
- T6→T8: _limitesNormais, _telaCheia, SalvarConfiguracao() stub "// --- memória (Task 8)" — ok
- T7→T8: ArmazemConfiguracao/Configuracao — ok
- T3/T5→T9: MsDesdeUltimoQuadro, MotorAudio.Ativo — ok

## Decisões
- Ruling: trabalho no branch lumina-v1, não na master — a skill proíbe implementar na master sem consentimento; merge no fim — custo se errado: um merge.
- Ruling: ordem física — as provas com câmera dependem do Douglas na bancada e fora do isolamento. Regra 7 dele (andar só sobe com o anterior provado) é absoluta: faço T0 passos 1–3, o núcleo testado (T1, T2, T4, T7 = base) e o código do andar 1 (T3); paro para ele provar andar 0 e andar 1 antes de escrever T5 em diante — custo se errado: uma pausa a mais.
- Task 0: Ruling: passos 1–3 feitos e commitados em separado (ferramentas); passos 4–8 (bancada) e medicoes.md ficam para o Douglas — custo se errado: nenhum.
Task 1: complete (commits 5289b91..d15f5ed, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:     7, Ignorado:     0, Total:     7, Duração: 5 ms - Lumina.Testes.dll (net8.0))
Task 2: complete (commits d15f5ed..b73fd95, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    31, Ignorado:     0, Total:    31, Duração: 23 ms - Lumina.Testes.dll (net8.0))
Task 4: complete (commits b73fd95..bd2925b, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    40, Ignorado:     0, Total:    40, Duração: 23 ms - Lumina.Testes.dll (net8.0))
Task 7: complete (commits bd2925b..2ec89fb, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    49, Ignorado:     0, Total:    49, Duração: 44 ms - Lumina.Testes.dll (net8.0))
- Task 3: Ruling: contagem esperada de testes 31 → 49, porque T4 e T7 vieram antes (ordem da decisão acima) — custo se errado: nenhum.
- Task 3: código commitado (passos 1–7: build 0 avisos/0 erros, 49/49). Passos 8–9 (prova sem sinal e prova do andar 1) PENDENTES com o Douglas — task NÃO concluída.
- PAUSA: aguardando Douglas para a bancada (Task 0 passos 4–8) e provas da Task 3. T5 em diante só depois (regra 7).
- Task 3: Ruling: o plano mandava Parar() chamar Shutdown na fonte para destravar um ReadSample "parado sem sinal". Prova do passo 8 (Douglas, fora do isolamento, 10:55–10:58): 3/3 fechamentos presos 3 s em ReadSample logo após o Shutdown. A premissa vinha do ffmpeg travado DENTRO do isolamento; fora dele a MS2109 manda 60 fps sem sinal. Correção: Parar só sinaliza; a thread sai no próximo quadro e desliga a fonte no finally. Medido 3/3: thread termina ~0,3 s após o X — custo se errado: se algum dia a placa parar de mandar quadros sem sinal, fechar espera o limite de 3 s (logado), não trava.
- Task 3: passo 8 (prova sem sinal) OK após a correção. Janela fica branca sem quadro até o andar 5 (tela "sem sinal") — visual, anotado.
- Task 0/3: Ruling (decisão do Douglas, 03/10 ~11:15): bancada PC→placa impossível (um só cabo HDMI, dock→placa). Prova do andar 0/1 vira A+B: (A) Lumina e ffplay lado a lado com o Switch, comparando nos prints quem mostra a mudança primeiro (resolução ~1 quadro); (B) Lumina mede por dentro o atraso carimbo-do-dispositivo → Present (mediana/p95/máx no diagnóstico). Atraso absoluto da placa fica sem medir — custo se errado: o número da placa continua suposição (50–100 ms) até haver 2º cabo.
- Task 0: fps real medido (Douglas, fonte = Switch no menu): 720p60 → 60,0; 1080p "60" → 30,0 (quadros repetidos); 1080p30 → 30,2. 1080p60 da placa é falso; alternativo continua 1080p30, sem decisão pendente.
- Task 0: confirmado pelo Douglas: nos testes de fechar, o Switch estava DESLIGADO → "placa manda 60 fps sem sinal" vale.
- Task 3: Ruling: medição B usa o tempo da amostra do ReadSample (base QPC 100 ns, provado: 1º quadro tempo 2248527147211 × relógio 2248527567580, Douglas 11:32) em vez do MFSampleExtension_DeviceReferenceSystemTime — GUID não está no Vortice nem há SDK para conferir; não chuto GUID. ResumoAtraso (núcleo) com 3 testes RED→GREEN, suíte 52/52 — custo se errado: a medida começa no carimbo do MF, que pode estar até ~16 ms antes do fim da chegada do quadro (o outro carimbo da placa).
- Task 3: B medido (Douglas 11:38, Switch ligado, 720p60): fps 59,5–60,5; atraso chegada→Present mediana 17,6–20,0 ms, p95 27–39 ms, máx 37–66 ms (66 no início). O 2º carimbo da placa fica ~16 ms após o tempo da amostra → parte do Lumina ≈ 2–4 ms.
- Task 3: A impossível: a MS2109 aceita um programa por vez (Lumina segurando → ffplay "already in use"; ffplay segurando → Lumina MF_E_HW_MFT_FAILED_START_STREAMING 0xC00D3704). Ruling (decisão do Douglas): prova do andar 1 = B + teste de uso dele jogando (sente igual/melhor/pior que OBS/ffplay) — custo se errado: atraso absoluto continua sem número.
- Aberto (observar): E_ACCESSDENIED fora do isolamento 1× (11:36, atalho .cmd pelo Explorer); o mesmo atalho funcionou às 11:39 e o PowerShell dele em 10:55, 10:57, 11:00, 11:32, 11:37. Sem evento no FrameServer nas recusas → bloqueio na camada de permissão. Causa desconhecida.
- Task 3: prova do andar 1 APROVADA pelo Douglas (Monster Hunter: 'fluindo bem, não vejo problemas').
Task 0: complete (commits 049cd31..258c907, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    52, Ignorado:     0, Total:    52, Duração: 44 ms - Lumina.Testes.dll (net8.0))
Task 3: complete (commits 2ec89fb..258c907, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    52, Ignorado:     0, Total:    52, Duração: 41 ms - Lumina.Testes.dll (net8.0))
- Task 5: código commitado (build 0/0 numa pasta separada — o Lumina.exe estava aberto com o Douglas jogando e travava a cópia; não fechei o processo dele). 52/52.
- Task 5: Ruling pendente do Douglas: o teste do tom (1000/1500 Hz) exige HDMI do PC na placa — impossível com um cabo só. Proposta: prova de ouvido com o Switch (altura/velocidade normal, sem chiado, junto com a imagem); erros da correção (velocidade pela metade, ruído) são audíveis.
- Task 5: Ruling (Douglas aceitou): prova de ouvido no lugar do tom. APROVADA: 'som normal e junto com a imagem'; log 96k mono → 48k estéreo; L≠R no diagnóstico.
Task 5: complete (commits 258c907..7e9c0be, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    52, Ignorado:     0, Total:    52, Duração: 39 ms - Lumina.Testes.dll (net8.0))
- Task 6: código commitado (build 0/0, 52/52). Prova do andar 3 pendente com o Douglas.
- Task 6: prova do andar 3 APROVADA pelo Douglas: 'todos funcionaram' (8/8 linhas da tabela).
Task 6: complete (commits 7e9c0be..8ceb6ce, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    52, Ignorado:     0, Total:    52, Duração: 48 ms - Lumina.Testes.dll (net8.0))
- Task 8: código commitado (build 0/0, 52/52). Prova do andar 4 pendente com o Douglas.
- Task 8: Ruling: achado do Douglas na prova — maximizada (botão do Windows) não era lembrada ao reabrir nem ao sair da tela cheia. Causa (código): Configuracao sem campo; AlternarTelaCheia forçava Normal e só devolvia Bounds. Correção: Configuracao.Maximizada (teste Salva_e_le_igual + padrão falso, RED→GREEN, 52/52) + janela guarda/devolve o estado. O plano não previa — o desenho diz 'lembra como deixou' — custo se errado: nenhum.
- Task 8: prova do andar 4 APROVADA pelo Douglas (posição/tamanho/2º monitor, tela cheia, modo+volume, maximizada nos 3 casos). PENDENTE por decisão dele: 'monitor desligado → volta ao principal' (coberto só pelos testes GarantirVisivel).
Task 8: complete (commits 8ceb6ce..a261334, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    52, Ignorado:     0, Total:    52, Duração: 43 ms - Lumina.Testes.dll (net8.0))
- Task 9: código commitado (build 0/0, 52/52). Nota: como a MS2109 manda quadros sem sinal, a tela 'sem sinal' aparece quando a placa some/está ocupada/negada, não quando o Switch desliga (aí aparece a imagem que a própria placa gera).
- Task 9: prova do andar 5 APROVADA pelo Douglas (Switch off/on, USB tirado/recolocado — log 12:05:33→41/48, webcam livre). PENDENTES: headset fixado desligado; volta ao fechar o ffplay.
Task 9: complete (commits a261334..a318294, tests: dotnet test → Aprovado!  – Com falha:     0, Aprovado:    52, Ignorado:     0, Total:    52, Duração: 42 ms - Lumina.Testes.dll (net8.0))
- Extra (pedido do Douglas): ícone. Arte dele em arte/lumina-original.png; ele escolheu o recorte do desenho (B) e sem fundo. Fundo removido pelo contorno convexo da borda prateada (1ª tentativa por preenchimento vazou e furou o miolo — vista na prévia, refeita). .ico com 9 tamanhos (16–256) no exe (ApplicationIcon) e na janela (recurso embutido); conferido extraindo do exe.
Final review: revisor opus (fresh context), 0 Critical, 2 Important, 9 Minor; Review Focus 1–5 OK com ressalvas.
- Final: re-grade: Minor 1 (fechar durante a abertura → 3 s congelado + Dispose da tela sob thread viva → possível AccessViolation) SOBE para Important — gatilho real (fechar nos primeiros ~4 s ou com o OBS segurando a placa).
- Final: fixed Important 1 (troca de modo durante a abertura → duas threads) — Revezamento (núcleo): Trocar_durante_a_abertura_nunca_deixa_dois_trabalhos_ao_mesmo_tempo RED (tipo ausente) → GREEN; mutação (sem o Join da anterior) dá máximo 3 ≠ 1, provando que o teste pega o defeito; suíte 56/56 (3× seguidas).
- Final: fixed Minor→Important 1 (fechar durante a abertura) — Parar() devolve se terminou (teste Parar_avisa_quando_o_trabalho_nao_termina_no_limite RED→GREEN); janela faz Hide() e, se não terminou, Environment.Exit(0) sem desmontar a tela (config já salva). Verificação da janela: build 0/0; prova manual pendente com o Douglas.
- Final: Ruling: Important 2 (áudio 'ativo' e mudo para sempre) corrigido SEM teste automático — exige injetar falha no WASAPI/NAudio; correção: Ativo = entrada E saída; Iniciar desfaz tudo se StartRecording/IniciarSaida falhar. Achei o mesmo defeito também no ReiniciarSaida (troca de padrão do Windows), coberto pela mesma mudança do Ativo. Verificado por build 0/0 e leitura — custo se errado: o mudo-para-sempre pode voltar em algum caminho não lido.
- Final: Ruling: Minor 9 (vazamento do leitor se SetCurrentMediaType falhar) foi corrigido junto, porque o novo aviso de parada depois do SetCurrentMediaType precisava do mesmo try/Dispose — custo se errado: nenhum.
- Final: minor (deferred): GPU removida (TDR/atualização de driver) deixa 'sem sinal' até reabrir.
- Final: minor (deferred): tela cheia + monitor desligado no meio da sessão → ao sair da tela cheia a janela vai para fora da tela (próxima sessão resgata).
- Final: minor (deferred): config.json com X/Y gigantes estoura int em GarantirVisivel; barra de título pode ficar fora da tela.
- Final: minor (deferred): fechar minimizada perde o 'maximizada'.
- Final: minor (deferred): headset fixado religado não volta sozinho; menu fica sem item marcado enquanto ele está ausente.
- Final: minor (deferred): log sem limite (linhas repetidas a cada 2–6,5 s com a placa ocupada) e Registro.Escrever só captura IOException.
- Final: minor (deferred): OnHandleCreated não é idempotente (recriação de handle criaria 2ª captura).
- Extra (pedido do Douglas): título 'Lumina — <modo>' — ele jogou ~20 min em 1080p30 lembrado do teste do andar 4 sem perceber. Teste Titulo_da_janela_mostra_o_modo RED→GREEN, 57/57. Relato dele: 'perda' de imagem em 1080p30 foi da placa (não do jogo); pico único 12:16:02 (p95 482 ms, máx 643 ms) em ~20 min.
- Extra (aprovado pelo Douglas): MODO SUAVE como o OBS — fila + relógio no vblank, quadro escolhido pelo carimbo com atraso fixo adaptativo (p95 da chegada + folga). Ruling: suave vem marcado por padrão (recomendação aceita implicitamente — ele não escolheu o padrão) — custo se errado: um clique no menu.
- Suave: Ruling: Agenda.Adicionar devolve bool + out (não T?): com int, 'nada despejado' virava 0 = índice válido — o teste Fila_cheia pegou (RED real), 66/66 — custo se errado: nenhum.
- Suave: integração commitada (build 0/0, 66/66). Marcações DIAG da investigação descartadas (achados no caderno). Prova pendente com o Douglas jogando o Rise.
- CORREÇÃO (13:40): o E_ACCESSDENIED NÃO era o isolamento do terminal do Claude. Causa provada: Auto-Sandbox do Norton 360 — autosandbox.log mostra "Sandboxing (no custody)" na 1ª abertura de cada exe recompilado e "Not sandboxing (exception list)" nas seguintes; bate com todas as recusas e sucessos do dia; às 13:37 a sonda abriu as 2 câmeras pelo meu terminal. Rulings anteriores que citam o isolamento como causa estão errados nesse ponto (as decisões em si continuam válidas: quem testa é o Douglas).
- Extra (item 2 pedido): Problemas.DeHResult/Mensagem (núcleo, 9 testes RED→GREEN) + captura guarda o último problema + tela "sem sinal" mostra a frase. 75/75.
- Item 3 (13:50): publicado em %LOCALAPPDATA%\Programs\Lumina (Release, framework-dependent), atalho Lumina.lnk na área de trabalho com ícone; README para amigos; merge ff lumina-v1→master (75/75 na master); push para https://github.com/Douglasgbm/Lumina (já PUBLIC, estava vazio); branch lumina-v1 apagado (merged).
- Item 1 (14:25): degrau = reancoragem do carimbo fabricado pelo MF; carimbos da placa estáveis; exibição real 68,7 → 62,5 ms (não subiu). Sem correção de código. DIAG descartado (não commitado).
