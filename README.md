# Lumina

Mostra a imagem e toca o som da placa de captura (MacroSilicon MS2109) numa janela,
com o menor atraso possível, para jogar o Switch 2. Abre só a placa: a webcam fica livre.

- Clique direito: modo (720p60/1080p30), saída de som, volume, mudo, tela cheia.
- F11 ou clique duplo: tela cheia. Esc sai da tela cheia. M: mudo.
- Lembra monitor, tamanho, tela cheia, modo e som em `%APPDATA%\Lumina\config.json`.
- Problemas vão para `%APPDATA%\Lumina\lumina.log`.

Desenho: `docs/superpowers/specs/2026-10-03-lumina-design.md` ·
Plano e andares: `docs/superpowers/plans/2026-10-03-lumina.md` · Medições: `docs/medicoes.md`

    dotnet test                       # testes do núcleo
    dotnet run --project src/Lumina   # o app (fora do isolamento do terminal)
