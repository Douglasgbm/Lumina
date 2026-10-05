# Lumina

Mostra a imagem e toca o som da placa de captura numa janela, com o menor atraso possível,
para jogar o console pelo PC. Abre só a placa: a webcam continua livre para outros programas.

## Antes de usar

- **Windows 10/11** e o [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).
- **Placa:** qualquer câmera que o Windows mostre (clique direito → **Placa**). A MS2109 (as "USB Video" genéricas
  com `VID_534D&PID_2109`) é escolhida sozinha na 1ª vez; outras placas, escolha no menu. O som vem sozinho do mesmo
  aparelho USB; se não vier, escolha em **Entrada de som**. Os modos do menu são os que a placa oferece (16:9, 720p+, 30/60).
  Abrir uma placa leva alguns segundos (medido: 4 s a MS2109, 8 s uma webcam C270).
- **Antivírus:** alguns (ex.: Norton 360) isolam um programa novo na primeira vez que ele abre, e isolado ele
  não acessa a câmera. Se aparecer "Acesso à câmera negado", feche e abra de novo.
- A placa aceita **um programa por vez**: com o OBS (ou outro) usando a placa, o Lumina espera e volta sozinho.

## Uso

- **Clique direito:** modo (720p60 / 1080p30), modo suave × menor atraso, saída de som, volume, mudo, tela cheia.
- **F11** ou **clique duplo:** tela cheia. **Esc** sai da tela cheia. **M:** mudo.
- **Modo suave** (padrão): mostra os quadros no ritmo do monitor, como o OBS — imagem lisa, ~10–20 ms a mais.
  **Menor atraso:** cada quadro vai para a tela na hora em que chega.
- O título da janela mostra o modo atual. O Lumina lembra monitor, tamanho, maximizada, tela cheia, modo e som
  em `%APPDATA%\Lumina\config.json`. Problemas vão para `%APPDATA%\Lumina\lumina.log`.

## Desenvolvimento

    dotnet test                                    # testes do núcleo
    dotnet run --project src/Lumina -- --diagnostico   # grava fps/atraso em %APPDATA%\Lumina\diagnostico.txt
    dotnet publish src/Lumina -c Release -r win-x64 --self-contained false -o publicar

Desenho: `docs/superpowers/specs/2026-10-03-lumina-design.md` ·
Plano e andares: `docs/superpowers/plans/2026-10-03-lumina.md` · Medições: `docs/medicoes.md`
