# Lumina — escolher a placa: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** O Lumina passa a funcionar com qualquer câmera que o Windows mostre: o menu escolhe a placa, os modos vêm da placa e o som acha a entrada do mesmo aparelho.

**Architecture:** Toda regra nova fica no núcleo testável: `IdUsb` (VID/PID/pai de um link ou caminho de hardware), `Placa.Resolver` (escolha salva → mesma placa em outra porta → MS2109 → nenhuma), `Modos` (filtro 16:9 ≥ 720p 30/60, defeito conhecido da MS2109, formato preferido), `EntradaSom.Resolver` e os problemas novos. O app só liga isso ao Windows: `Dispositivos` lista as câmeras, a captura abre a placa recebida e publica os modos, o áudio usa a entrada resolvida, a janela monta o menu e procura a placa quando ela falta.

**Tech Stack:** o mesmo do Lumina (.NET 8 WinForms, Vortice 3.8.3, NAudio 2.2.1, xUnit).

**Spec:** `docs/superpowers/specs/2026-10-03-lumina-escolher-placa-design.md`

## Global Constraints

- Branch `escolher-placa`; commits terminam com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Nunca abrir câmera sem escolha, exceto a MS2109 (`VID_534D&PID_2109`) quando nada foi escolhido.
- Placa escolhida desconectada: aviso, **sem** trocar por outra câmera (nem pela MS2109).
- Modos no menu: 16:9, altura ≥ 720, 30 ou 60 fps (arredondado); esconder o 1080p60 da MS2109 (falso: 30 imagens reais, medido).
- Formato nativo preferido: MJPG > NV12 > YUY2 > qualquer.
- Correção 96k mono → 48k estéreo só para o áudio da MS2109.
- Configurações antigas continuam valendo: o nome do modo segue "720p60", "1080p30"…
- A 1ª abertura de cada exe recompilado é isolada pelo Norton (câmera negada): fechar e abrir de novo — não é defeito do código.
- Interação (menu, cliques, desplugar) quem prova é o Douglas.
- Entre as Tasks 1 e 3 o projeto do app não compila (o núcleo muda antes): rodar só `dotnet test tests/Lumina.Testes`.

## Review Focus

1. **Duas câmeras com o mesmo nome** (duas placas genéricas "USB Video"): a escolha salva reabre a certa pelo link; em outra porta, só reconhece pelo nome+modelo. → `A_escolhida_em_outra_porta_USB_e_reconhecida_pelo_nome_e_modelo` (Task 1) + prova da Task 4.
2. **Câmera sem nenhum modo útil** (só 4:3 ou só 480p): mensagem clara, sem travar. → `Escolher_usa_o_salvo_senao_720p60_senao_o_primeiro` (`null`) e `Camera_sem_modo_util_pede_outra_placa` (Task 2).
3. **Trocar de placa no meio do jogo**: imagem e som mudam juntos, sem congelar a janela, e a antiga fica livre para outros programas. → prova da Task 4.
4. **Entrada de som escolhida que some** (headset USB desligado): volta ao automático. → `Entrada_escolhida_que_sumiu_volta_ao_automatico` (Task 2).
5. **Configuração antiga** (sem `PlacaLink`): abre a MS2109 como antes. → `Nada_salvo_usa_a_MS2109_mesmo_com_a_webcam_antes_na_lista` (Task 1) + prova da Task 3.

---

### Task 1: Reconhecer aparelhos USB e resolver a placa

**Files:**
- Create: `src/Lumina.Nucleo/IdUsb.cs`
- Modify: `src/Lumina.Nucleo/Placa.cs`, `tests/Lumina.Testes/PlacaTestes.cs` (substituir inteiros)

**Interfaces:**
- Produces: `IdUsb(string Vid, string Pid, string Pai)`, `IdUsb.De(string) → IdUsb?`, `.MesmoAparelho(IdUsb)`, `.MesmoModelo(IdUsb)`; `enum MotivoSemPlaca { NenhumaEscolhida, EscolhidaAusente }`; `record ResultadoPlaca(DispositivoVideo? Placa, MotivoSemPlaca? Motivo)`; `Placa.IdMs2109`, `Placa.EhMs2109(string)`, `Placa.Resolver(IReadOnlyList<DispositivoVideo>, string? linkSalvo, string? nomeSalvo) → ResultadoPlaca`. `DispositivoVideo`, `DispositivoAudio`, `FormatoNativo` não mudam. Os dispositivos de teste `C270`, `Ms2109`, `MicC270`, `Realtek`, `AudioMs2109` ficam `internal` para as outras classes de teste.
- Remove: `Placa.IdHardware`, `Placa.EscolherVideo`, `Placa.EscolherAudio`, `Placa.EscolherFormatoNativo` (o app volta a compilar na Task 3).

- [ ] **Step 1: Escrever os testes que falham**

`tests/Lumina.Testes/PlacaTestes.cs` (substitui inteiro):

```csharp
using Lumina.Nucleo;

namespace Lumina.Testes;

public class PlacaTestes
{
    // Links e IDs copiados da máquina do Douglas em 03/10/2026.
    internal static readonly DispositivoVideo C270 = new("Logi C270 HD WebCam",
        @"\\?\usb#vid_046d&pid_0825&mi_00#7&33b9e16e&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global");
    internal static readonly DispositivoVideo Ms2109 = new("USB Video",
        @"\\?\usb#vid_534d&pid_2109&mi_00#7&2cbab050&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global");
    internal static readonly DispositivoAudio MicC270 = new("{0.0.1}.{a}", "Microfone (Logi C270 HD WebCam)", @"{1}.USB\VID_046D&PID_0825&MI_02\7&33B9E16E&0&0002");
    internal static readonly DispositivoAudio Realtek = new("{0.0.1}.{b}", "Microfone (Realtek(R) Audio)", @"{1}.HDAUDIO\FUNC_01&VEN_10EC&DEV_0897&SUBSYS_1458A194&REV_1004\5&E50FC96&0&0001");
    internal static readonly DispositivoAudio AudioMs2109 = new("{0.0.1}.{08d9fed2}", "Interface de áudio digital (USB Digital Audio)", @"{1}.USB\VID_534D&PID_2109&MI_02\7&2CBAB050&0&0002");

    [Fact]
    public void IdUsb_le_o_link_do_video_e_o_hardware_do_audio_do_mesmo_aparelho()
    {
        var video = IdUsb.De(Ms2109.Link);
        var audio = IdUsb.De(AudioMs2109.Hardware);
        Assert.Equal(new IdUsb("534D", "2109", "7&2CBAB050&0"), video);
        Assert.True(video!.Value.MesmoAparelho(audio!.Value));
    }

    [Fact]
    public void IdUsb_nao_confunde_aparelhos_diferentes()
    {
        Assert.False(IdUsb.De(Ms2109.Link)!.Value.MesmoAparelho(IdUsb.De(MicC270.Hardware)!.Value));
    }

    [Fact]
    public void IdUsb_de_algo_que_nao_e_USB_e_nulo()
    {
        Assert.Null(IdUsb.De(Realtek.Hardware));
        Assert.Null(IdUsb.De(@"\\?\root#camera#0000#{e5323777}\global")); // câmera virtual
    }

    [Fact]
    public void Nada_salvo_usa_a_MS2109_mesmo_com_a_webcam_antes_na_lista()
    {
        Assert.Equal(new ResultadoPlaca(Ms2109, null), Placa.Resolver([C270, Ms2109], null, null));
    }

    [Fact]
    public void Nada_salvo_e_sem_MS2109_nunca_abre_a_webcam_sozinho()
    {
        Assert.Equal(new ResultadoPlaca(null, MotivoSemPlaca.NenhumaEscolhida), Placa.Resolver([C270], null, null));
    }

    [Fact]
    public void A_escolhida_e_usada_mesmo_com_a_MS2109_conectada()
    {
        Assert.Equal(new ResultadoPlaca(C270, null), Placa.Resolver([C270, Ms2109], C270.Link, C270.Nome));
    }

    [Fact]
    public void A_escolhida_em_outra_porta_USB_e_reconhecida_pelo_nome_e_modelo()
    {
        var outraPorta = Ms2109 with { Link = @"\\?\usb#vid_534d&pid_2109&mi_00#7&99aa11bb&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global" };
        Assert.Equal(new ResultadoPlaca(outraPorta, null), Placa.Resolver([C270, outraPorta], Ms2109.Link, Ms2109.Nome));
    }

    [Fact]
    public void A_escolhida_desconectada_nao_troca_por_outra_nem_pela_MS2109()
    {
        Assert.Equal(new ResultadoPlaca(null, MotivoSemPlaca.EscolhidaAusente), Placa.Resolver([Ms2109], C270.Link, C270.Nome));
    }

    [Fact]
    public void Outra_placa_generica_de_mesmo_nome_nao_e_a_MS2109()
    {
        var outra = new DispositivoVideo("USB Video", @"\\?\usb#vid_345f&pid_2130&mi_00#8&1&0&0000#{e5323777}\global");
        Assert.False(Placa.EhMs2109(outra.Link));
        Assert.Equal(MotivoSemPlaca.NenhumaEscolhida, Placa.Resolver([outra], null, null).Motivo);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Lumina.Testes`
Expected: FALHA de compilação (`IdUsb`, `ResultadoPlaca`, `MotivoSemPlaca`, `Placa.Resolver` não existem).

- [ ] **Step 3: Implementar**

`src/Lumina.Nucleo/IdUsb.cs` (novo):

```csharp
using System.Text.RegularExpressions;

namespace Lumina.Nucleo;

/// <summary>
/// VID, PID e o "pai" da instância de um aparelho USB, tirados do link do vídeo ou do caminho de hardware do áudio.
/// Vídeo e áudio do mesmo aparelho têm o mesmo pai (medido em 03/10/2026):
/// <c>…usb#vid_534d&amp;pid_2109&amp;mi_00#7&amp;2cbab050&amp;0&amp;0000#…</c> × <c>{1}.USB\VID_534D&amp;PID_2109&amp;MI_02\7&amp;2CBAB050&amp;0&amp;0002</c>.
/// </summary>
public readonly record struct IdUsb(string Vid, string Pid, string Pai)
{
    static readonly Regex Padrao = new(
        @"usb[#\\]vid_([0-9a-f]{4})&pid_([0-9a-f]{4})(?:&mi_[0-9a-f]{2})?[#\\]([^#\\]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>null se não for um aparelho USB (placa PCIe, câmera virtual...).</summary>
    public static IdUsb? De(string texto)
    {
        var m = Padrao.Match(texto);
        if (!m.Success) return null;
        string instancia = m.Groups[3].Value;
        int ultimo = instancia.LastIndexOf('&');
        string pai = ultimo > 0 ? instancia[..ultimo] : instancia;
        return new(m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value.ToUpperInvariant(), pai.ToUpperInvariant());
    }

    /// <summary>Vídeo e áudio do mesmo aparelho físico.</summary>
    public bool MesmoAparelho(IdUsb outro) => MesmoModelo(outro) && Pai == outro.Pai;

    /// <summary>Mesmo modelo, talvez em outra porta USB.</summary>
    public bool MesmoModelo(IdUsb outro) => Vid == outro.Vid && Pid == outro.Pid;
}
```

`src/Lumina.Nucleo/Placa.cs` (substitui inteiro):

```csharp
namespace Lumina.Nucleo;

public sealed record DispositivoVideo(string Nome, string Link);

public sealed record DispositivoAudio(string Id, string Nome, string Hardware);

public sealed record FormatoNativo(int Indice, string Subtipo, int Largura, int Altura, int FpsNumerador, int FpsDenominador);

public enum MotivoSemPlaca { NenhumaEscolhida, EscolhidaAusente }

public sealed record ResultadoPlaca(DispositivoVideo? Placa, MotivoSemPlaca? Motivo);

/// <summary>Qual câmera do Windows é a placa de captura.</summary>
public static class Placa
{
    /// <summary>MacroSilicon MS2109, a placa do Douglas (medido em 03/10/2026).</summary>
    public const string IdMs2109 = "VID_534D&PID_2109";

    public static bool EhMs2109(string linkOuHardware) =>
        linkOuHardware.Contains(IdMs2109, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 1º a escolha salva pelo link; 2º a mesma placa em outra porta USB (mesmo nome e modelo);
    /// nada salvo → a MS2109 se estiver conectada. Nunca troca a escolhida por outra câmera.
    /// </summary>
    public static ResultadoPlaca Resolver(IReadOnlyList<DispositivoVideo> lista, string? linkSalvo, string? nomeSalvo)
    {
        if (linkSalvo is not null)
        {
            var exata = lista.FirstOrDefault(d => string.Equals(d.Link, linkSalvo, StringComparison.OrdinalIgnoreCase));
            if (exata is not null) return new(exata, null);
            var idSalvo = IdUsb.De(linkSalvo);
            var outraPorta = lista.FirstOrDefault(d => d.Nome == nomeSalvo
                && idSalvo is { } s && IdUsb.De(d.Link) is { } i && i.MesmoModelo(s));
            return outraPorta is not null ? new(outraPorta, null) : new(null, MotivoSemPlaca.EscolhidaAusente);
        }
        var ms2109 = lista.FirstOrDefault(d => EhMs2109(d.Link));
        return ms2109 is not null ? new(ms2109, null) : new(null, MotivoSemPlaca.NenhumaEscolhida);
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Lumina.Testes`
Expected: `Aprovado! – Com falha: 0, Aprovado: 77` (simulado em 03/10/2026). O app ainda não compila: usa os métodos removidos.

- [ ] **Step 5: Commit**

```powershell
git add src/Lumina.Nucleo/IdUsb.cs src/Lumina.Nucleo/Placa.cs tests/Lumina.Testes/PlacaTestes.cs
git commit -m "Núcleo: reconhecer aparelho USB e resolver a placa escolhida"
```

---

### Task 2: Modos da placa, entrada de som, configuração e avisos

Uma task só: o `ModoVideo` novo derruba o `PorNome` que a configuração usa, então modos e configuração
mudam juntos (simulado em 03/10/2026: separados, a task de modos terminava sem compilar).

**Files:**
- Create: `src/Lumina.Nucleo/Modos.cs`, `tests/Lumina.Testes/ModosTestes.cs`, `tests/Lumina.Testes/EnquadramentoTestes.cs`
- Modify (substituir inteiros): `src/Lumina.Nucleo/ModoVideo.cs`, `src/Lumina.Nucleo/RegrasAudio.cs`, `src/Lumina.Nucleo/Configuracao.cs`, `src/Lumina.Nucleo/Problemas.cs`, `tests/Lumina.Testes/RegrasAudioTestes.cs`, `tests/Lumina.Testes/ConfiguracaoTestes.cs`, `tests/Lumina.Testes/ProblemasTestes.cs`
- Delete: `tests/Lumina.Testes/ModoEEnquadramentoTestes.cs` (enquadramento vai para `EnquadramentoTestes.cs`; `Alternar`/`PorNome` somem com os métodos; o título vai para `ModosTestes`)

**Interfaces:**
- Consumes: `IdUsb`, `Placa.EhMs2109`, `MotivoSemPlaca` (Task 1)
- Produces: `record ModoVideo(int Largura, int Altura, int Fps)` com `Nome` ("720p60"), `Titulo`, `Hd60`, `FullHd30`; `Modos.Disponiveis(IEnumerable<FormatoNativo>, string? link) → List<ModoVideo>`; `Modos.Escolher(IReadOnlyList<ModoVideo>, string? nomeSalvo) → ModoVideo?`; `Modos.FormatoPara(IEnumerable<FormatoNativo>, ModoVideo) → int?`; `EntradaSom.Nenhuma`, `EntradaSom.Resolver(string? escolha, IReadOnlyList<DispositivoAudio>, DispositivoVideo? placa) → DispositivoAudio?`; `Configuracao.PlacaLink`, `.PlacaNome`, `.EntradaSom`; `ProblemaCaptura.NenhumaPlacaEscolhida`, `.PlacaEscolhidaAusente`, `.SemModoUtil`; `Problemas.De(MotivoSemPlaca)`; `Problemas.Mensagem(ProblemaCaptura, string? nomePlaca = null)`.
- Remove: `ModoVideo.PorNome`, `ModoVideo.Alternar`.

- [ ] **Step 1: Escrever os testes que falham**

`tests/Lumina.Testes/ModosTestes.cs` (novo):

```csharp
using Lumina.Nucleo;

namespace Lumina.Testes;

public class ModosTestes
{
    static FormatoNativo F(int i, string sub, int l, int a, int fps, int den = 1) => new(i, sub, l, a, fps * den, den);

    // Amostras reais (03/10/2026) do que cada câmera anuncia, incluindo o que o filtro tem que jogar fora.
    static readonly FormatoNativo[] FormatosMs2109 =
    [
        F(0, "NV12", 1920, 1080, 60), F(1, "MJPG", 1920, 1080, 60), F(3, "MJPG", 1920, 1080, 30),
        F(5, "MJPG", 1920, 1080, 25), F(11, "MJPG", 1600, 1200, 60), F(21, "MJPG", 1360, 768, 60),
        F(50, "NV12", 1280, 720, 60), F(51, "MJPG", 1280, 720, 60), F(53, "MJPG", 1280, 720, 50),
        F(55, "MJPG", 1280, 720, 30), F(101, "MJPG", 640, 480, 60), F(111, "YUY2", 1920, 1080, 5),
    ];

    static readonly FormatoNativo[] FormatosC270 =
    [
        F(0, "MJPG", 1280, 720, 30), F(1, "NV12", 1280, 720, 30), F(2, "YUY2", 1280, 720, 7, 2), // 7,5 fps
        F(3, "MJPG", 1184, 656, 30), F(4, "MJPG", 1280, 960, 30), F(5, "MJPG", 640, 360, 30),
    ];

    [Fact]
    public void MS2109_mostra_720p30_720p60_1080p30_e_esconde_o_1080p60_falso()
    {
        var modos = Modos.Disponiveis(FormatosMs2109, PlacaTestes.Ms2109.Link);
        Assert.Equal(["720p30", "720p60", "1080p30"], modos.Select(m => m.Nome));
    }

    [Fact]
    public void Outra_placa_com_1080p60_mostra_o_1080p60()
    {
        var modos = Modos.Disponiveis(FormatosMs2109, @"\\?\usb#vid_345f&pid_2130&mi_00#8&1&0&0000#{e5323777}\global");
        Assert.Contains(modos, m => m.Nome == "1080p60");
    }

    [Fact]
    public void C270_so_tem_720p30()
    {
        Assert.Equal(["720p30"], Modos.Disponiveis(FormatosC270, PlacaTestes.C270.Link).Select(m => m.Nome));
    }

    [Fact]
    public void Fps_fracionado_arredonda()
    {
        var modos = Modos.Disponiveis([new FormatoNativo(0, "NV12", 1920, 1080, 60000, 1001)], null); // 59,94
        Assert.Equal(["1080p60"], modos.Select(m => m.Nome));
    }

    [Fact]
    public void Escolher_usa_o_salvo_senao_720p60_senao_o_primeiro()
    {
        var ms = Modos.Disponiveis(FormatosMs2109, PlacaTestes.Ms2109.Link);
        Assert.Equal("1080p30", Modos.Escolher(ms, "1080p30")!.Nome);
        Assert.Equal("720p60", Modos.Escolher(ms, "4k")!.Nome);
        Assert.Equal("720p60", Modos.Escolher(ms, "1080p60")!.Nome); // escondido na MS2109
        var c270 = Modos.Disponiveis(FormatosC270, PlacaTestes.C270.Link);
        Assert.Equal("720p30", Modos.Escolher(c270, "720p60")!.Nome);
        Assert.Null(Modos.Escolher([], "720p60"));
    }

    [Fact]
    public void Formato_prefere_MJPG_depois_NV12_depois_YUY2()
    {
        Assert.Equal(51, Modos.FormatoPara(FormatosMs2109, ModoVideo.Hd60));
        Assert.Equal(0, Modos.FormatoPara([F(0, "NV12", 1280, 720, 60), F(1, "YUY2", 1280, 720, 60)], ModoVideo.Hd60));
        Assert.Equal(7, Modos.FormatoPara([F(7, "outro", 1280, 720, 60)], ModoVideo.Hd60));
        Assert.Null(Modos.FormatoPara(FormatosC270, ModoVideo.Hd60));
    }

    [Fact]
    public void Nome_e_titulo_do_modo()
    {
        Assert.Equal("720p60", ModoVideo.Hd60.Nome);
        Assert.Equal("Lumina — 1080p30", ModoVideo.FullHd30.Titulo);
        Assert.Equal(ModoVideo.Hd60, new ModoVideo(1280, 720, 60));
    }
}
```

`tests/Lumina.Testes/EnquadramentoTestes.cs` (novo):

```csharp
using Lumina.Nucleo;

namespace Lumina.Testes;

public class EnquadramentoTestes
{
    [Fact]
    public void Encaixar_area_16_9_ocupa_tudo()
    {
        Assert.Equal(new Retangulo(0, 0, 1920, 1080), Enquadramento.Encaixar(1920, 1080, 1280, 720));
    }

    [Fact]
    public void Encaixar_area_mais_alta_poe_barras_em_cima_e_embaixo()
    {
        Assert.Equal(new Retangulo(0, 60, 1920, 1080), Enquadramento.Encaixar(1920, 1200, 1280, 720));
        Assert.Equal(new Retangulo(0, 219, 1000, 562), Enquadramento.Encaixar(1000, 1000, 1280, 720));
    }

    [Fact]
    public void Encaixar_area_mais_larga_poe_barras_dos_lados()
    {
        Assert.Equal(new Retangulo(320, 0, 1920, 1080), Enquadramento.Encaixar(2560, 1080, 1920, 1080));
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-5, 720)]
    public void Encaixar_janela_minimizada_ou_sem_area_da_retangulo_vazio(int l, int a)
    {
        Assert.Equal(new Retangulo(0, 0, 0, 0), Enquadramento.Encaixar(l, a, 1280, 720));
    }

    static readonly Retangulo Principal = new(0, 0, 1920, 1080);
    static readonly Retangulo Segundo = new(1920, 0, 1920, 1080);

    [Fact]
    public void Visivel_no_segundo_monitor_fica_onde_estava()
    {
        var j = new Retangulo(2000, 100, 1280, 720);
        Assert.Equal(j, Enquadramento.GarantirVisivel(j, [Principal, Segundo], Principal));
    }

    [Fact]
    public void Segundo_monitor_desligado_traz_a_janela_para_o_principal()
    {
        var j = new Retangulo(2000, 100, 1280, 720);
        Assert.Equal(new Retangulo(320, 180, 1280, 720), Enquadramento.GarantirVisivel(j, [Principal], Principal));
    }

    [Fact]
    public void So_uma_tira_visivel_conta_como_perdida()
    {
        var j = new Retangulo(1880, 100, 1280, 720); // 40 px no principal
        Assert.Equal(new Retangulo(320, 180, 1280, 720), Enquadramento.GarantirVisivel(j, [Principal], Principal));
    }

    [Fact]
    public void Janela_maior_que_o_monitor_principal_encolhe()
    {
        var j = new Retangulo(5000, 0, 3840, 2160);
        Assert.Equal(Principal, Enquadramento.GarantirVisivel(j, [Principal], Principal));
    }
}
```

Apagar `tests/Lumina.Testes/ModoEEnquadramentoTestes.cs`.

`tests/Lumina.Testes/RegrasAudioTestes.cs` (substitui inteiro):

```csharp
using Lumina.Nucleo;

namespace Lumina.Testes;

public class RegrasAudioTestes
{
    [Fact]
    public void Corrige_o_anuncio_da_MS2109_float()
    {
        // Formato que o WASAPI entregou na máquina do Douglas em 03/10/2026.
        Assert.Equal(new FormatoPcm(48000, 2, 32, true), CorrecaoMs2109.Corrigir(new FormatoPcm(96000, 1, 32, true)));
    }

    [Fact]
    public void Corrige_o_anuncio_da_MS2109_16_bits()
    {
        Assert.Equal(new FormatoPcm(48000, 2, 16, false), CorrecaoMs2109.Corrigir(new FormatoPcm(96000, 1, 16, false)));
    }

    [Theory]
    [InlineData(48000, 2)]
    [InlineData(96000, 2)]
    [InlineData(44100, 1)]
    public void Formato_que_ja_e_verdadeiro_nao_muda(int taxa, int canais)
    {
        var f = new FormatoPcm(taxa, canais, 32, true);
        Assert.Equal(f, CorrecaoMs2109.Corrigir(f));
    }

    [Fact]
    public void Fila_so_limpa_depois_do_limite()
    {
        Assert.False(FilaAudio.DeveLimpar(TimeSpan.FromMilliseconds(60)));
        Assert.True(FilaAudio.DeveLimpar(TimeSpan.FromMilliseconds(61)));
    }

    [Fact]
    public void Saida_fixada_que_existe_vence_o_padrao()
    {
        Assert.Equal("headset", SaidaAudio.Resolver("headset", ["headset", "caixa"], "caixa"));
    }

    [Fact]
    public void Saida_fixada_que_sumiu_cai_no_padrao()
    {
        Assert.Equal("caixa", SaidaAudio.Resolver("headset", ["caixa"], "caixa"));
    }

    [Fact]
    public void Sem_saida_fixada_segue_o_padrao()
    {
        Assert.Equal("caixa", SaidaAudio.Resolver(null, ["headset", "caixa"], "caixa"));
    }

    [Fact]
    public void Entrada_automatica_e_a_do_mesmo_aparelho_da_placa()
    {
        var entradas = new[] { PlacaTestes.MicC270, PlacaTestes.Realtek, PlacaTestes.AudioMs2109 };
        Assert.Equal(PlacaTestes.AudioMs2109, EntradaSom.Resolver(null, entradas, PlacaTestes.Ms2109));
        Assert.Equal(PlacaTestes.MicC270, EntradaSom.Resolver(null, entradas, PlacaTestes.C270));
    }

    [Fact]
    public void Entrada_automatica_sem_audio_do_aparelho_fica_sem_som()
    {
        Assert.Null(EntradaSom.Resolver(null, [PlacaTestes.Realtek], PlacaTestes.Ms2109));
        Assert.Null(EntradaSom.Resolver(null, [PlacaTestes.AudioMs2109], null));
    }

    [Fact]
    public void Entrada_escolhida_vence_e_nenhuma_e_sem_som()
    {
        var entradas = new[] { PlacaTestes.MicC270, PlacaTestes.Realtek, PlacaTestes.AudioMs2109 };
        Assert.Equal(PlacaTestes.Realtek, EntradaSom.Resolver(PlacaTestes.Realtek.Id, entradas, PlacaTestes.Ms2109));
        Assert.Null(EntradaSom.Resolver(EntradaSom.Nenhuma, entradas, PlacaTestes.Ms2109));
    }

    [Fact]
    public void Entrada_escolhida_que_sumiu_volta_ao_automatico()
    {
        Assert.Equal(PlacaTestes.AudioMs2109, EntradaSom.Resolver("{id-que-sumiu}", [PlacaTestes.AudioMs2109], PlacaTestes.Ms2109));
    }
}
```

`tests/Lumina.Testes/ConfiguracaoTestes.cs` (substitui inteiro):

```csharp
using Lumina.Nucleo;

namespace Lumina.Testes;

public sealed class ConfiguracaoTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "lumina-testes-" + Guid.NewGuid().ToString("N"));
    string Caminho => Path.Combine(_pasta, "config.json");

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
    }

    [Fact]
    public void Sem_arquivo_devolve_o_padrao()
    {
        var c = new ArmazemConfiguracao(Caminho).Ler();
        Assert.Equal(new Configuracao(), c);
        Assert.Equal("720p60", c.Modo);
        Assert.False(c.TelaCheia);
        Assert.False(c.Maximizada);
        Assert.True(c.ModoSuave);
    }

    [Fact]
    public void Salva_e_le_igual_criando_a_pasta()
    {
        var armazem = new ArmazemConfiguracao(Caminho);
        var c = new Configuracao { X = 2000, Y = 40, Largura = 1600, Altura = 900, Maximizada = true, TelaCheia = true, Modo = "1080p30", SaidaFixaId = "{0.0.0}.{x}", Volume = 0.5f, Mudo = true, ModoSuave = false };
        armazem.Salvar(c);
        Assert.Equal(c, armazem.Ler());
        Assert.False(File.Exists(Caminho + ".tmp"));
    }

    [Theory]
    [InlineData("{{{ não é json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"Volume\": \"alto\"}")]
    [InlineData("[1,2,3]")]
    public void Arquivo_corrompido_volta_ao_padrao(string conteudo)
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, conteudo);
        Assert.Equal(new Configuracao(), new ArmazemConfiguracao(Caminho).Ler());
    }

    [Fact]
    public void Valores_absurdos_sao_corrigidos()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, "{\"Largura\": -5, \"Altura\": 999999, \"Volume\": 7, \"Modo\": \"4k\"}");
        var c = new ArmazemConfiguracao(Caminho).Ler();
        Assert.Equal(320, c.Largura);
        Assert.Equal(16384, c.Altura);
        Assert.Equal(1f, c.Volume);
        Assert.Equal("4k", c.Modo); // o modo depende da placa: quem cai no padrão é Modos.Escolher
    }

    [Theory]
    [InlineData("{\"Modo\": \"\"}")]
    [InlineData("{\"Modo\": null}")]
    public void Modo_vazio_vira_720p60(string conteudo)
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, conteudo);
        Assert.Equal("720p60", new ArmazemConfiguracao(Caminho).Ler().Modo);
    }

    [Fact]
    public void Lembra_a_placa_e_a_entrada_de_som()
    {
        var armazem = new ArmazemConfiguracao(Caminho);
        var c = new Configuracao { PlacaLink = PlacaTestes.C270.Link, PlacaNome = PlacaTestes.C270.Nome, EntradaSom = EntradaSom.Nenhuma };
        armazem.Salvar(c);
        Assert.Equal(c, armazem.Ler());
        Assert.Null(new Configuracao().PlacaLink);
        Assert.Null(new Configuracao().EntradaSom);
    }

    [Fact]
    public void Campo_que_falta_fica_com_o_padrao()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, "{\"Mudo\": true}");
        var c = new ArmazemConfiguracao(Caminho).Ler();
        Assert.True(c.Mudo);
        Assert.Equal(1280, c.Largura);
        Assert.Equal(1f, c.Volume);
    }
}
```

`tests/Lumina.Testes/ProblemasTestes.cs` (substitui inteiro):

```csharp
using Lumina.Nucleo;

namespace Lumina.Testes;

public class ProblemasTestes
{
    // Códigos vistos no lumina.log do Douglas em 03/10/2026.
    [Theory]
    [InlineData(unchecked((int)0x80070005), ProblemaCaptura.AcessoNegado)]  // E_ACCESSDENIED
    [InlineData(unchecked((int)0xC00D3704), ProblemaCaptura.PlacaOcupada)]  // MF_E_HW_MFT_FAILED_START_STREAMING (ffplay segurando, 11:41)
    [InlineData(unchecked((int)0xC00D3EA2), ProblemaCaptura.PlacaAusente)]  // MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED (USB tirado, 12:05)
    [InlineData(unchecked((int)0x80004005), ProblemaCaptura.Outro)]         // E_FAIL qualquer
    public void Codigo_do_Windows_vira_um_problema_conhecido(int hresult, ProblemaCaptura esperado)
    {
        Assert.Equal(esperado, Problemas.DeHResult(hresult));
    }

    [Fact]
    public void Acesso_negado_aponta_o_antivirus_e_a_privacidade_do_Windows()
    {
        // Causa medida em 03/10/2026: Auto-Sandbox do Norton 360 isola cada versão nova na 1ª abertura.
        var m = Problemas.Mensagem(ProblemaCaptura.AcessoNegado);
        Assert.Contains("negado", m);
        Assert.Contains("antivírus", m);
        Assert.Contains("abra de novo", m);
        Assert.Contains("Privacidade", m);
    }

    [Fact]
    public void Placa_ocupada_sugere_fechar_o_outro_programa()
    {
        Assert.Contains("outro programa", Problemas.Mensagem(ProblemaCaptura.PlacaOcupada));
    }

    [Fact]
    public void Placa_ausente_sugere_conferir_o_USB()
    {
        Assert.Contains("USB", Problemas.Mensagem(ProblemaCaptura.PlacaAusente));
    }

    [Theory]
    [InlineData(ProblemaCaptura.Nenhum)]
    [InlineData(ProblemaCaptura.Outro)]
    public void Sem_problema_identificado_continua_sem_sinal(ProblemaCaptura p)
    {
        Assert.Equal("sem sinal", Problemas.Mensagem(p));
    }

    [Fact]
    public void Sem_placa_escolhida_ensina_onde_escolher()
    {
        Assert.Equal(ProblemaCaptura.NenhumaPlacaEscolhida, Problemas.De(MotivoSemPlaca.NenhumaEscolhida));
        Assert.Contains("Placa", Problemas.Mensagem(ProblemaCaptura.NenhumaPlacaEscolhida));
    }

    [Fact]
    public void Placa_escolhida_ausente_diz_o_nome_dela()
    {
        Assert.Equal(ProblemaCaptura.PlacaEscolhidaAusente, Problemas.De(MotivoSemPlaca.EscolhidaAusente));
        Assert.Contains("\"Logi C270 HD WebCam\" não está conectada", Problemas.Mensagem(ProblemaCaptura.PlacaEscolhidaAusente, "Logi C270 HD WebCam"));
    }

    [Fact]
    public void Camera_sem_modo_util_pede_outra_placa()
    {
        Assert.Contains("720p", Problemas.Mensagem(ProblemaCaptura.SemModoUtil));
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Lumina.Testes`
Expected: FALHA de compilação (`Modos`, `EntradaSom`, `PlacaLink`, `Problemas.De` não existem; `new ModoVideo(1280, 720, 60)` não bate com o construtor antigo).

- [ ] **Step 3: Implementar**

`src/Lumina.Nucleo/ModoVideo.cs` (substitui inteiro):

```csharp
namespace Lumina.Nucleo;

/// <summary>Um modo de imagem da placa. O nome ("720p60") é o que fica salvo na configuração.</summary>
public sealed record ModoVideo(int Largura, int Altura, int Fps)
{
    public static readonly ModoVideo Hd60 = new(1280, 720, 60);
    public static readonly ModoVideo FullHd30 = new(1920, 1080, 30);

    public string Nome => $"{Altura}p{Fps}";

    /// <summary>O modo fica à vista: o Douglas jogou 20 min em 1080p30 sem perceber (03/10/2026).</summary>
    public string Titulo => $"Lumina — {Nome}";
}
```

`src/Lumina.Nucleo/Modos.cs` (novo):

```csharp
namespace Lumina.Nucleo;

/// <summary>Os modos que a placa oferece e que fazem sentido para jogar: 16:9, de 720p para cima, 30 ou 60 fps.</summary>
public static class Modos
{
    static readonly string[] Preferencia = ["MJPG", "NV12", "YUY2"];

    public static List<ModoVideo> Disponiveis(IEnumerable<FormatoNativo> formatos, string? link) =>
        formatos
            .Where(f => f.FpsDenominador > 0 && f.Altura >= 720 && (long)f.Largura * 9 == (long)f.Altura * 16)
            .Select(f => new ModoVideo(f.Largura, f.Altura, Fps(f)))
            .Where(m => m.Fps is 30 or 60)
            .Distinct()
            .Where(m => !DefeitoConhecido(link, m))
            .OrderBy(m => m.Altura).ThenBy(m => m.Fps)
            .ToList();

    /// <summary>O salvo se a placa tiver; senão 720p60; senão o primeiro; null se a placa não tem nenhum modo útil.</summary>
    public static ModoVideo? Escolher(IReadOnlyList<ModoVideo> modos, string? nomeSalvo) =>
        modos.FirstOrDefault(m => m.Nome == nomeSalvo)
        ?? modos.FirstOrDefault(m => m == ModoVideo.Hd60)
        ?? modos.FirstOrDefault();

    /// <summary>Índice do formato nativo do modo, preferindo MJPG (comprimido, cabe no USB 2.0), depois NV12, YUY2.</summary>
    public static int? FormatoPara(IEnumerable<FormatoNativo> formatos, ModoVideo modo)
    {
        var candidatos = formatos
            .Where(f => f.FpsDenominador > 0 && f.Largura == modo.Largura && f.Altura == modo.Altura && Fps(f) == modo.Fps)
            .ToList();
        foreach (var subtipo in Preferencia)
            if (candidatos.FirstOrDefault(f => f.Subtipo == subtipo) is { } f) return f.Indice;
        return candidatos.FirstOrDefault()?.Indice;
    }

    /// <summary>MS2109: anuncia 1080p60 mas entrega 30 imagens diferentes por segundo (medido em 03/10/2026).</summary>
    static bool DefeitoConhecido(string? link, ModoVideo m) =>
        link is not null && Placa.EhMs2109(link) && m.Altura == 1080 && m.Fps == 60;

    static int Fps(FormatoNativo f) => (int)Math.Round((double)f.FpsNumerador / f.FpsDenominador);
}
```

`src/Lumina.Nucleo/RegrasAudio.cs` (substitui inteiro):

```csharp
namespace Lumina.Nucleo;

public readonly record struct FormatoPcm(int Taxa, int Canais, int Bits, bool PontoFlutuante);

public static class CorrecaoMs2109
{
    /// <summary>
    /// A MS2109 se anuncia como 96 kHz mono, mas manda 48 kHz estéreo intercalado
    /// (anúncio medido no registro do Windows em 03/10/2026). Os bytes não mudam, só a leitura deles.
    /// </summary>
    public static FormatoPcm Corrigir(FormatoPcm f) =>
        f.Taxa == 96000 && f.Canais == 1 ? f with { Taxa = 48000, Canais = 2 } : f;
}

public static class FilaAudio
{
    public static readonly TimeSpan Limite = TimeSpan.FromMilliseconds(60);

    /// <summary>
    /// Captura e reprodução têm relógios diferentes e a fila cresce devagar. Passou do limite,
    /// joga fora: perder 60 ms de som é melhor que o som ficar atrás da imagem.
    /// </summary>
    public static bool DeveLimpar(TimeSpan acumulado) => acumulado > Limite;
}

public static class SaidaAudio
{
    /// <summary>A saída fixada, se ainda existe; senão a padrão do Windows.</summary>
    public static string Resolver(string? fixaId, IReadOnlyCollection<string> disponiveis, string padraoId) =>
        fixaId is not null && disponiveis.Contains(fixaId) ? fixaId : padraoId;
}

public static class EntradaSom
{
    /// <summary>Valor guardado na configuração para "sem som".</summary>
    public const string Nenhuma = "nenhuma";

    /// <summary>
    /// null = automático: a entrada do mesmo aparelho USB que a placa. "nenhuma" = sem som.
    /// Uma entrada escolhida que sumiu volta ao automático (como a saída fixada que some volta à padrão).
    /// </summary>
    public static DispositivoAudio? Resolver(string? escolha, IReadOnlyList<DispositivoAudio> entradas, DispositivoVideo? placa)
    {
        if (escolha == Nenhuma) return null;
        if (escolha is not null && entradas.FirstOrDefault(e => e.Id == escolha) is { } fixa) return fixa;
        if (placa is null || IdUsb.De(placa.Link) is not { } video) return null;
        return entradas.FirstOrDefault(e => IdUsb.De(e.Hardware) is { } a && a.MesmoAparelho(video));
    }
}
```

`src/Lumina.Nucleo/Configuracao.cs` (substitui inteiro):

```csharp
using System.Text.Json;

namespace Lumina.Nucleo;

/// <summary>Tudo que o Lumina lembra entre uma abertura e outra.</summary>
public sealed record Configuracao
{
    public int X { get; init; } = 100;
    public int Y { get; init; } = 100;
    public int Largura { get; init; } = 1280;
    public int Altura { get; init; } = 720;
    /// <summary>Maximizada pelo botão do Windows; X/Y/Largura/Altura guardam o tamanho de quando não está.</summary>
    public bool Maximizada { get; init; }
    public bool TelaCheia { get; init; }
    public string Modo { get; init; } = ModoVideo.Hd60.Nome;
    public string? SaidaFixaId { get; init; }
    public float Volume { get; init; } = 1f;
    public bool Mudo { get; init; }
    /// <summary>Mostra no ritmo do monitor com uma fila curta (como o OBS); falso = cada quadro na hora que chega.</summary>
    public bool ModoSuave { get; init; } = true;
    /// <summary>A placa escolhida no menu; null = nenhuma escolhida (usa a MS2109 se estiver conectada).</summary>
    public string? PlacaLink { get; init; }
    public string? PlacaNome { get; init; }
    /// <summary>null = automático (a do aparelho da placa); "nenhuma" = sem som; ou o id da entrada.</summary>
    public string? EntradaSom { get; init; }

    /// <summary>
    /// Corrige valores fora do possível (arquivo editado à mão, versão antiga). O modo não é conferido aqui:
    /// depende da placa — um nome que a placa não tem cai no padrão em Modos.Escolher.
    /// </summary>
    public Configuracao Saneada() => this with
    {
        Largura = Math.Clamp(Largura, 320, 16384),
        Altura = Math.Clamp(Altura, 180, 16384),
        Volume = float.IsFinite(Volume) ? Math.Clamp(Volume, 0f, 1f) : 1f,
        Modo = string.IsNullOrWhiteSpace(Modo) ? ModoVideo.Hd60.Nome : Modo,
    };
}

public sealed class ArmazemConfiguracao(string caminho)
{
    static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    /// <summary>Arquivo ausente, corrompido ou ilegível: volta ao padrão, nunca quebra.</summary>
    public Configuracao Ler()
    {
        try
        {
            if (!File.Exists(caminho)) return new Configuracao();
            var lida = JsonSerializer.Deserialize<Configuracao>(File.ReadAllText(caminho), Opcoes);
            return (lida ?? new Configuracao()).Saneada();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new Configuracao();
        }
    }

    /// <summary>Grava num .tmp e troca: uma queda de luz no meio não deixa o arquivo pela metade.</summary>
    public void Salvar(Configuracao c)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
        var tmp = caminho + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(c, Opcoes));
        File.Move(tmp, caminho, overwrite: true);
    }
}
```

`src/Lumina.Nucleo/Problemas.cs` (substitui inteiro):

```csharp
namespace Lumina.Nucleo;

public enum ProblemaCaptura { Nenhum, PlacaAusente, PlacaOcupada, AcessoNegado, Outro, NenhumaPlacaEscolhida, PlacaEscolhidaAusente, SemModoUtil }

/// <summary>O que mostrar no lugar da imagem: uma frase que diga o que houve, em vez de tela branca muda.</summary>
public static class Problemas
{
    public static ProblemaCaptura DeHResult(int hresult) => unchecked((uint)hresult) switch
    {
        0x80070005 => ProblemaCaptura.AcessoNegado,  // E_ACCESSDENIED
        0xC00D3704 => ProblemaCaptura.PlacaOcupada,  // MF_E_HW_MFT_FAILED_START_STREAMING
        0xC00D3EA2 => ProblemaCaptura.PlacaAusente,  // MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED
        _ => ProblemaCaptura.Outro,
    };

    public static ProblemaCaptura De(MotivoSemPlaca motivo) => motivo switch
    {
        MotivoSemPlaca.NenhumaEscolhida => ProblemaCaptura.NenhumaPlacaEscolhida,
        _ => ProblemaCaptura.PlacaEscolhidaAusente,
    };

    /// <param name="nomePlaca">Nome da placa escolhida, para dizer qual está faltando.</param>
    public static string Mensagem(ProblemaCaptura p, string? nomePlaca = null) => p switch
    {
        // Medido em 03/10/2026: o Auto-Sandbox do Norton 360 isola cada versão nova na 1ª abertura.
        ProblemaCaptura.AcessoNegado =>
            "Acesso à câmera negado.\nAlguns antivírus (ex.: Norton) isolam um programa novo na primeira vez: feche e abra de novo.\nSe continuar, veja Configurações > Privacidade > Câmera no Windows.",
        ProblemaCaptura.PlacaOcupada =>
            "A placa está em uso por outro programa (OBS, Discord...).\nFeche o outro programa: a imagem volta sozinha.",
        ProblemaCaptura.PlacaAusente =>
            "Placa de captura não encontrada.\nConfira o cabo USB: a imagem volta sozinha.",
        ProblemaCaptura.NenhumaPlacaEscolhida =>
            "Escolha a placa de captura no menu.\nClique com o botão direito → Placa.",
        ProblemaCaptura.PlacaEscolhidaAusente =>
            $"A placa \"{nomePlaca ?? "escolhida"}\" não está conectada.\nConecte de novo, ou escolha outra no menu (botão direito → Placa).",
        ProblemaCaptura.SemModoUtil =>
            "Esta câmera não tem nenhum modo 16:9 de 720p ou mais.\nEscolha outra placa no menu (botão direito → Placa).",
        _ => "sem sinal",
    };
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Lumina.Testes`
Expected: `Aprovado! – Com falha: 0, Aprovado: 87` (simulado em 03/10/2026).

- [ ] **Step 5: Commit**

```powershell
git add -A src/Lumina.Nucleo tests/Lumina.Testes
git commit -m "Núcleo: modos da placa, entrada de som do mesmo aparelho, placa lembrada e avisos"
```

---

### Task 3: Ligar no app — andar 2 (a MS2109 continua igual)

**Files:**
- Create: `src/Lumina/Dispositivos.cs`
- Modify (substituir inteiros): `src/Lumina/CapturaVideo.cs`, `src/Lumina/MotorAudio.cs`, `src/Lumina/JanelaPrincipal.cs`

**Interfaces:**
- Consumes: tudo das Tasks 1–2.
- Produces: `Dispositivos.Video() → List<DispositivoVideo>`; `CapturaVideo.Iniciar(DispositivoVideo placa, string? modoDesejado)`, `.ModosDaPlaca`, `.ModoAtual`; `MotorAudio.Placa`, `.EntradaEscolhida`, `.Entradas()`.

Partes sem teste automático (precisam da placa): a prova é o Douglas.

- [ ] **Step 1: Escrever**

`src/Lumina/Dispositivos.cs` (novo):

```csharp
using Lumina.Nucleo;
using Vortice.MediaFoundation;

namespace Lumina;

/// <summary>As câmeras que o Windows enxerga agora (placas de captura e webcams, sem distinção).</summary>
static class Dispositivos
{
    public static List<DispositivoVideo> Video()
    {
        var lista = new List<DispositivoVideo>();
        using (var ativos = MediaFactory.MFEnumVideoDeviceSources())
            foreach (var a in ativos) lista.Add(new DispositivoVideo(a.FriendlyName, a.SymbolicLink));
        return lista;
    }
}
```

`src/Lumina/CapturaVideo.cs` (substitui inteiro):

```csharp
using Lumina.Nucleo;
using Vortice.Direct3D11;
using Vortice.MediaFoundation;

namespace Lumina;

/// <summary>
/// Lê a placa escolhida numa thread própria e entrega cada quadro à tela.
/// Placa ausente, em uso ou desconectada: espera 2 s e tenta de novo, até Parar.
/// Ao abrir, publica os modos que a placa oferece (para o menu) e o modo em uso (para o título).
/// </summary>
sealed class CapturaVideo : IDisposable
{
    readonly Renderizador _tela;
    readonly Action<double>? _aoQuadro;
    readonly Revezamento _revezamento = new("captura");
    readonly ResumoAtraso _chegada = new();
    long _ultimoQuadro; // Environment.TickCount64; 0 = nenhum ainda
    volatile ProblemaCaptura _problema;
    volatile IReadOnlyList<ModoVideo> _modos = [];
    volatile ModoVideo? _modoAtual;

    sealed class PlacaNaoConectada() : Exception("placa não conectada");
    sealed class SemModoUtil() : Exception("a câmera não tem modo 16:9 de 720p ou mais");

    /// <summary>Os modos da placa aberta por último (vazio antes de abrir).</summary>
    public IReadOnlyList<ModoVideo> ModosDaPlaca => _modos;

    /// <summary>O modo aberto agora; null antes de abrir.</summary>
    public ModoVideo? ModoAtual => _modoAtual;

    /// <summary>O último problema ao abrir ou ler a placa; Nenhum quando chegou quadro.</summary>
    public ProblemaCaptura Problema => _problema;

    /// <param name="aoQuadro">Recebe o atraso em ms entre a chegada do quadro e o envio dele para a tela.</param>
    public CapturaVideo(Renderizador tela, Action<double>? aoQuadro)
    {
        _tela = tela;
        _aoQuadro = aoQuadro;
    }

    /// <summary>Milissegundos desde o último quadro desenhado; long.MaxValue se nenhum chegou.</summary>
    public long MsDesdeUltimoQuadro
    {
        get
        {
            long t = Interlocked.Read(ref _ultimoQuadro);
            return t == 0 ? long.MaxValue : Environment.TickCount64 - t;
        }
    }

    /// <summary>
    /// Não espera: a thread anterior é avisada e a nova só começa quando ela terminar (abrir leva ~4 s).
    /// Antes, trocar de modo durante a abertura deixava duas capturas disputando a placa (revisão final).
    /// </summary>
    /// <param name="modoDesejado">Nome do modo ("720p60"); se a placa não tiver, Modos.Escolher decide.</param>
    public void Iniciar(DispositivoVideo placa, string? modoDesejado)
    {
        Interlocked.Exchange(ref _ultimoQuadro, 0);
        _revezamento.Iniciar(vez => Laco(placa, modoDesejado, vez));
    }

    /// <summary>
    /// Só avisa: a thread sai no próximo quadro, e a placa manda quadros mesmo sem sinal (60 fps, medido em 03/10/2026).
    /// Desligar a fonte daqui travava o ReadSample até o limite de 3 s (medido 3 de 3 vezes).
    /// Falso: a thread ainda está presa abrindo a placa — quem chama não deve desmontar a tela.
    /// </summary>
    public bool Parar()
    {
        bool terminou = _revezamento.Parar(TimeSpan.FromSeconds(3));
        if (!terminou) Registro.Log("captura: a thread não terminou em 3 s");
        return terminou;
    }

    public void Dispose() => Parar();

    void Laco(DispositivoVideo placa, string? modoDesejado, Revezamento.Vez vez)
    {
        while (!vez.Parar)
        {
            IMFMediaSource? fonte = null;
            IMFSourceReader? leitor = null;
            try
            {
                leitor = Abrir(placa, modoDesejado, vez, out fonte);
                while (!vez.Parar && LerUm(leitor)) { }
            }
            catch (Exception e) when (!vez.Parar)
            {
                Registro.Erro("captura", e);
                _problema = e switch
                {
                    PlacaNaoConectada => ProblemaCaptura.PlacaAusente,
                    SemModoUtil => ProblemaCaptura.SemModoUtil,
                    SharpGen.Runtime.SharpGenException s => Problemas.DeHResult(s.HResult),
                    _ => ProblemaCaptura.Outro,
                };
            }
            catch (Exception)
            {
                // Parada pedida no meio da abertura (OperationCanceledException). Não é erro.
            }
            finally
            {
                leitor?.Dispose();
                if (fonte is not null)
                {
                    try { fonte.Shutdown(); } catch (Exception) { }
                    fonte.Dispose();
                }
            }
            for (int i = 0; i < 20 && !vez.Parar; i++) Thread.Sleep(100);
        }
    }

    IMFSourceReader Abrir(DispositivoVideo placa, string? modoDesejado, Revezamento.Vez vez, out IMFMediaSource? fonte)
    {
        fonte = null;
        // Acha a placa de novo a cada abertura: pode ter mudado de porta USB (link novo, mesmo nome e modelo).
        var link = Placa.Resolver(Dispositivos.Video(), placa.Link, placa.Nome).Placa?.Link ?? throw new PlacaNaoConectada();
        using var fa = MediaFactory.MFCreateAttributes(2);
        fa.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVidcap);
        fa.Set(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink, link);
        fonte = MediaFactory.MFCreateDeviceSource(fa);
        if (vez.Parar) throw new OperationCanceledException();

        // Fixa o formato nativo do modo, preferindo MJPG; sem isso o leitor pode escolher NV12 (decodificado na CPU).
        ModoVideo modo;
        using (var pd = fonte.CreatePresentationDescriptor())
        {
            pd.GetStreamDescriptorByIndex(0, out _, out IMFStreamDescriptor sd);
            using (sd)
            using (var mth = sd.MediaTypeHandler)
            {
                var formatos = new List<FormatoNativo>();
                for (int i = 0; i < mth.MediaTypeCount; i++)
                {
                    using var t = mth.GetMediaTypeByIndex(i);
                    formatos.Add(Descrever(i, t));
                }
                var modos = Modos.Disponiveis(formatos, link);
                _modos = modos;
                modo = Modos.Escolher(modos, modoDesejado) ?? throw new SemModoUtil();
                int indice = Modos.FormatoPara(formatos, modo)!.Value;
                using var escolhido = mth.GetMediaTypeByIndex(indice);
                mth.CurrentMediaType = escolhido;
            }
        }
        if (vez.Parar) throw new OperationCanceledException();

        using var atr = MediaFactory.MFCreateAttributes(4);
        atr.Set(SourceReaderAttributeKeys.D3DManager, _tela.Gerente);
        atr.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, true);
        atr.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
        atr.Set(SinkWriterAttributeKeys.LowLatency, true);
        var leitor = MediaFactory.MFCreateSourceReaderFromMediaSource(fonte, atr);
        try
        {
            // ARGB32 vira textura B8G8R8A8, o mesmo formato da cadeia de imagens: a cópia é direta.
            using var pedido = MediaFactory.MFCreateMediaType();
            pedido.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            pedido.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Argb32);
            MediaFactory.MFSetAttributeSize(pedido, MediaTypeAttributeKeys.FrameSize, (uint)modo.Largura, (uint)modo.Altura);
            leitor.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, pedido);
            if (vez.Parar) throw new OperationCanceledException();
        }
        catch
        {
            leitor.Dispose();
            throw;
        }
        _modoAtual = modo;
        Registro.Log($"captura aberta: {placa.Nome} {modo.Nome}");
        return leitor;
    }

    bool LerUm(IMFSourceReader leitor)
    {
        using var amostra = leitor.ReadSample(SourceReaderIndex.FirstVideoStream, SourceReaderControlFlag.None,
            out _, out SourceReaderFlag flags, out long tempoAmostra);
        if ((flags & (SourceReaderFlag.EndOfStream | SourceReaderFlag.Error)) != 0) return false;
        if (amostra is null) return true;
        using var buffer = amostra.GetBufferByIndex(0);
        using var dxgi = buffer.QueryInterface<IMFDXGIBuffer>();
        using var textura = new ID3D11Texture2D(dxgi.GetResource(typeof(ID3D11Texture2D).GUID));
        if (_tela.Suave)
        {
            _tela.Guardar(textura, dxgi.SubresourceIndex, tempoAmostra);
            // O atraso da fila acompanha o p95 da chegada: cobre os trancos sem esperar à toa.
            if (_chegada.Registrar((Relogio.Agora100ns() - tempoAmostra) / 10_000.0) is { } r)
            {
                _tela.AtrasoMs = AtrasoSuave.De(r);
                if (_aoQuadro is not null)
                    Registro.Diagnostico($"chegada: mediana={r.Mediana:F1} p95={r.P95:F1} máx={r.Maximo:F1} ms → atraso da fila {_tela.AtrasoMs:F0} ms");
            }
        }
        else
        {
            _tela.Apresentar(textura, dxgi.SubresourceIndex);
            _aoQuadro?.Invoke((Relogio.Agora100ns() - tempoAmostra) / 10_000.0);
        }
        Interlocked.Exchange(ref _ultimoQuadro, Environment.TickCount64);
        _problema = ProblemaCaptura.Nenhum;
        return true;
    }

    static FormatoNativo Descrever(int indice, IMFMediaType t)
    {
        var sub = t.GetGUID(MediaTypeAttributeKeys.Subtype);
        MediaFactory.MFGetAttributeSize(t, MediaTypeAttributeKeys.FrameSize, out uint l, out uint a);
        MediaFactory.MFGetAttributeRatio(t, MediaTypeAttributeKeys.FrameRate, out uint n, out uint d);
        string nome = sub == VideoFormatGuids.Mjpg ? "MJPG" : sub == VideoFormatGuids.NV12 ? "NV12" : sub == VideoFormatGuids.YUY2 ? "YUY2" : sub.ToString();
        return new FormatoNativo(indice, nome, (int)l, (int)a, (int)n, (int)d);
    }
}
```

`src/Lumina/MotorAudio.cs` (substitui inteiro):

```csharp
using Lumina.Nucleo;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Lumina;

/// <summary>
/// Captura o som da placa, corrige o formato anunciado e toca na saída escolhida.
/// Tudo aqui roda na thread da janela; avisos do Windows chegam por _ui.Post.
/// </summary>
sealed class MotorAudio : IMMNotificationClient, IDisposable
{
    static readonly Guid SubtipoFloat = new("00000003-0000-0010-8000-00aa00389b71");
    // Guarda o caminho do hardware pai, ex.: {1}.USB\VID_534D&PID_2109&MI_02\... (medido em 03/10/2026).
    static readonly PropertyKey ChaveHardware = new(new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), 2);

    readonly MMDeviceEnumerator _enumerador = new();
    readonly SynchronizationContext _ui;
    readonly Action<float[], float[]>? _aoBloco;
    WasapiCapture? _captura;
    WasapiOut? _saida;
    BufferedWaveProvider? _fila;
    VolumeSampleProvider? _volume;
    string? _saidaFixaId;
    float _nivel = 1f;
    bool _mudo;
    string? _ultimoAviso;

    /// <summary>A placa de vídeo em uso: a entrada automática é a do mesmo aparelho USB.</summary>
    public DispositivoVideo? Placa { get; set; }

    /// <summary>null = automático; EntradaSom.Nenhuma = sem som; ou o id de uma entrada.</summary>
    public string? EntradaEscolhida { get; set; }

    /// <param name="aoBloco">Só no diagnóstico: recebe os canais esquerdo e direito já corrigidos.</param>
    public MotorAudio(SynchronizationContext ui, Action<float[], float[]>? aoBloco)
    {
        _ui = ui;
        _aoBloco = aoBloco;
        _enumerador.RegisterEndpointNotificationCallback(this);
    }

    /// <summary>
    /// Entrada E saída de pé. Se uma falhar (placa sumiu, saída em uso exclusivo, permissão), fica falso
    /// e a janela tenta Iniciar de novo; antes ficava "ativo" e mudo para sempre (revisão final).
    /// </summary>
    public bool Ativo => _captura is not null && _saida is not null;

    public float Volume
    {
        get => _nivel;
        set { _nivel = Math.Clamp(value, 0f, 1f); AplicarVolume(); }
    }

    public bool Mudo
    {
        get => _mudo;
        set { _mudo = value; AplicarVolume(); }
    }

    /// <summary>null = segue a saída padrão do Windows.</summary>
    public string? SaidaFixaId
    {
        get => _saidaFixaId;
        set { _saidaFixaId = value; if (Ativo) ReiniciarSaida(); }
    }

    public List<DispositivoAudio> Entradas() =>
        _enumerador.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new DispositivoAudio(d.ID, d.FriendlyName, Hardware(d))).ToList();

    public List<(string Id, string Nome)> Saidas() =>
        _enumerador.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Select(d => (d.ID, d.FriendlyName)).ToList();

    public void Iniciar()
    {
        Parar();
        var escolhido = EntradaSom.Resolver(EntradaEscolhida, Entradas(), Placa);
        if (escolhido is null)
        {
            Avisar(EntradaEscolhida == EntradaSom.Nenhuma ? "áudio: sem som (escolhido no menu)" : "áudio: nenhuma entrada de som da placa encontrada");
            return;
        }
        var dispositivo = _enumerador.GetDevice(escolhido.Id);

        var captura = new WasapiCapture(dispositivo, true, 10);
        var bruto = captura.WaveFormat;
        bool flutuante = bruto.Encoding == WaveFormatEncoding.IeeeFloat
            || (bruto is WaveFormatExtensible x && x.SubFormat == SubtipoFloat);
        var anunciado = new FormatoPcm(bruto.SampleRate, bruto.Channels, bruto.BitsPerSample, flutuante);
        // O defeito do 96 kHz mono é da MS2109; outras placas usam o formato como o Windows informa.
        var c = Nucleo.Placa.EhMs2109(escolhido.Hardware) ? CorrecaoMs2109.Corrigir(anunciado) : anunciado;
        var formato = flutuante ? WaveFormat.CreateIeeeFloatWaveFormat(c.Taxa, c.Canais) : new WaveFormat(c.Taxa, c.Bits, c.Canais);
        _ultimoAviso = null;
        Registro.Log($"áudio: {escolhido.Nome}: anunciado {bruto} → usado {formato}");

        var fila = new BufferedWaveProvider(formato)
        {
            BufferDuration = TimeSpan.FromMilliseconds(500),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };
        _fila = fila;
        _volume = new VolumeSampleProvider(fila.ToSampleProvider());
        AplicarVolume();
        captura.DataAvailable += (_, e) =>
        {
            if (FilaAudio.DeveLimpar(fila.BufferedDuration)) fila.ClearBuffer();
            fila.AddSamples(e.Buffer, 0, e.BytesRecorded);
            if (_aoBloco is not null && flutuante && formato.Channels == 2) Separar(e.Buffer, e.BytesRecorded);
        };
        captura.RecordingStopped += (_, e) =>
        {
            // Placa desconectada: para tudo; a janela tenta Iniciar de novo.
            if (e.Exception is null) return;
            Registro.Erro("áudio.captura", e.Exception);
            _ui.Post(_ => { if (ReferenceEquals(_captura, captura)) Parar(); }, null);
        };
        _captura = captura;
        try
        {
            captura.StartRecording();
            IniciarSaida();
        }
        catch
        {
            Parar();
            throw;
        }
    }

    public void Parar()
    {
        _saida?.Dispose();
        _saida = null;
        var captura = _captura;
        _captura = null;
        if (captura is not null)
        {
            try { captura.StopRecording(); } catch (Exception e) { Registro.Erro("áudio.parar", e); }
            captura.Dispose();
        }
        _fila = null;
        _volume = null;
    }

    public void Dispose()
    {
        _enumerador.UnregisterEndpointNotificationCallback(this);
        Parar();
        _enumerador.Dispose();
    }

    /// <summary>O vigia tenta de novo a cada 2 s: sem isso o mesmo aviso encheria o log.</summary>
    void Avisar(string linha)
    {
        if (linha == _ultimoAviso) return;
        _ultimoAviso = linha;
        Registro.Log(linha);
    }

    void AplicarVolume()
    {
        if (_volume is not null) _volume.Volume = _mudo ? 0f : _nivel;
    }

    void IniciarSaida()
    {
        if (_volume is null) return;
        var ids = Saidas().Select(s => s.Id).ToList();
        string padrao = _enumerador.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        var alvo = _enumerador.GetDevice(SaidaAudio.Resolver(_saidaFixaId, ids, padrao));
        var saida = new WasapiOut(alvo, AudioClientShareMode.Shared, true, 20);
        saida.PlaybackStopped += (_, e) =>
        {
            // Saída removida (headset desligado): volta a tocar onde der.
            if (e.Exception is null) return;
            Registro.Erro("áudio.saída", e.Exception);
            _ui.Post(_ => { if (ReferenceEquals(_saida, saida)) ReiniciarSaida(); }, null);
        };
        saida.Init(_volume);
        saida.Play();
        _saida = saida;
        Registro.Log($"áudio: tocando em {alvo.FriendlyName}");
    }

    void ReiniciarSaida()
    {
        _saida?.Dispose();
        _saida = null;
        try { IniciarSaida(); }
        catch (Exception e) { Registro.Erro("áudio.saída", e); }
    }

    void Separar(byte[] buffer, int bytes)
    {
        int quadros = bytes / 8;
        var esquerdo = new float[quadros];
        var direito = new float[quadros];
        for (int i = 0; i < quadros; i++)
        {
            esquerdo[i] = BitConverter.ToSingle(buffer, i * 8);
            direito[i] = BitConverter.ToSingle(buffer, i * 8 + 4);
        }
        _aoBloco!(esquerdo, direito);
    }

    static string Hardware(MMDevice d)
    {
        try { return d.Properties.Contains(ChaveHardware) ? d.Properties[ChaveHardware].Value?.ToString() ?? "" : ""; }
        catch (Exception) { return ""; }
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia)
            _ui.Post(_ => { if (Ativo && _saidaFixaId is null) ReiniciarSaida(); }, null);
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
    public void OnDeviceAdded(string pwstrDeviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
}
```

`src/Lumina/JanelaPrincipal.cs` (substitui inteiro):

```csharp
using System.Diagnostics;
using Lumina.Nucleo;

namespace Lumina;

/// <summary>Superfície onde a GPU desenha. Não pinta nada por conta própria, para não piscar por cima do vídeo.</summary>
sealed class PainelVideo : Control
{
    public PainelVideo()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.UserPaint, true);
    }
}

sealed class JanelaPrincipal : Form
{
    readonly PainelVideo _painel = new();
    readonly Label _semSinal = new()
    {
        Text = "sem sinal",
        ForeColor = Color.Gray,
        BackColor = Color.Black,
        Font = new Font("Segoe UI", 14f),
        TextAlign = ContentAlignment.MiddleCenter,
        Dock = DockStyle.Fill,
        Visible = false,
    };
    readonly System.Windows.Forms.Timer _vigia = new() { Interval = 500 };
    readonly ArmazemConfiguracao _armazem = new(Path.Combine(Registro.Pasta, "config.json"));
    readonly bool _diagnostico;
    readonly bool _forcarMudo;

    Renderizador? _tela;
    CapturaVideo? _captura;
    MotorAudio? _audio;
    Configuracao _config;
    DispositivoVideo? _placa;             // aberta agora; null = nenhuma (ver _semPlaca)
    MotivoSemPlaca? _semPlaca;
    int _tiquesSemPlaca;
    Rectangle _limitesNormais;
    bool _maximizadaAntesDaTelaCheia;
    bool _telaCheia;
    int _tiquesSemAudio;

    // Diagnóstico
    readonly ContadorQuadros _contador = new();
    readonly Stopwatch _relogio = Stopwatch.StartNew();
    readonly List<float> _esquerdo = new(), _direito = new();
    readonly ResumoAtraso _atraso = new();

    public JanelaPrincipal(bool diagnostico, bool forcarMudo)
    {
        _diagnostico = diagnostico;
        _forcarMudo = forcarMudo;
        _config = _armazem.Ler();

        Text = "Lumina";
        using (var icone = typeof(JanelaPrincipal).Assembly.GetManifestResourceStream("lumina.ico")!) Icon = new Icon(icone);
        BackColor = Color.Black;
        KeyPreview = true;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(320, 180);
        var telas = Screen.AllScreens.Select(s => Para(s.WorkingArea)).ToList();
        var r = Enquadramento.GarantirVisivel(new Retangulo(_config.X, _config.Y, _config.Largura, _config.Altura),
            telas, Para(Screen.PrimaryScreen!.WorkingArea));
        Bounds = new Rectangle(r.X, r.Y, r.Largura, r.Altura);
        if (_config.Maximizada) WindowState = FormWindowState.Maximized;
        Controls.Add(_painel);
        Controls.Add(_semSinal);

        ContextMenuStrip = MontarMenu();
        _painel.ContextMenuStrip = ContextMenuStrip;
        _semSinal.ContextMenuStrip = ContextMenuStrip;
        DoubleClick += (_, _) => AlternarTelaCheia();
        _painel.DoubleClick += (_, _) => AlternarTelaCheia();
        _semSinal.DoubleClick += (_, _) => AlternarTelaCheia();
        _vigia.Tick += (_, _) => Vigiar();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _tela = new Renderizador(_painel.Handle);
        if (_diagnostico) _tela.AoExibir = AoQuadro;
        if (_config.ModoSuave) _tela.IniciarSuave();
        _captura = new CapturaVideo(_tela, _diagnostico ? AoQuadro : null);

        _audio = new MotorAudio(SynchronizationContext.Current!, _diagnostico ? AoBlocoAudio : null)
        {
            Volume = _config.Volume,
            Mudo = _config.Mudo || _forcarMudo,
            EntradaEscolhida = _config.EntradaSom,
        };
        _audio.SaidaFixaId = _config.SaidaFixaId;
        ProcurarPlaca();
        _vigia.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_config.TelaCheia) AlternarTelaCheia();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        var modo = _captura?.ModoAtual ?? ModoVideo.Hd60;
        var r = Enquadramento.Encaixar(ClientSize.Width, ClientSize.Height, modo.Largura, modo.Altura);
        _painel.Bounds = new Rectangle(r.X, r.Y, r.Largura, r.Altura);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.F11: AlternarTelaCheia(); break;
            case Keys.Escape when _telaCheia: AlternarTelaCheia(); break;
            case Keys.M: AlternarMudo(); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _vigia.Stop();
        SalvarConfiguracao();
        Hide(); // some na hora, mesmo se a captura levar até 3 s para soltar a placa
        bool capturaParou = _captura?.Parar() ?? true;
        _audio?.Dispose();
        if (!capturaParou)
        {
            // A thread ainda está abrindo a placa e vai usar a tela: desmontar agora derruba o processo.
            // A configuração já foi salva; encerrar direto é o caminho seguro.
            Registro.Log("fechando sem desmontar a tela: a captura ainda estava abrindo a placa");
            Environment.Exit(0);
        }
        _tela?.Dispose();
        base.OnFormClosing(e);
    }

    // --- controles

    void AlternarTelaCheia()
    {
        if (!_telaCheia)
        {
            _limitesNormais = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _maximizadaAntesDaTelaCheia = WindowState == FormWindowState.Maximized;
            var monitor = Screen.FromControl(this).Bounds;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = monitor;
            _telaCheia = true;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _limitesNormais;
            if (_maximizadaAntesDaTelaCheia) WindowState = FormWindowState.Maximized;
            _telaCheia = false;
        }
        SalvarConfiguracao();
    }

    void AlternarMudo()
    {
        if (_audio is null) return;
        _audio.Mudo = !_audio.Mudo;
        SalvarConfiguracao();
    }

    void TrocarModo(ModoVideo modo)
    {
        if (_captura is null || _placa is null || modo == _captura.ModoAtual) return;
        _config = _config with { Modo = modo.Nome };
        _captura.Iniciar(_placa, modo.Nome);
        SalvarConfiguracao();
    }

    // --- placa e entrada de som

    /// <summary>Resolve a placa pela escolha salva (ou a MS2109) e, se achou, abre imagem e som dela.</summary>
    void ProcurarPlaca()
    {
        if (_captura is null || _audio is null) return;
        var r = Placa.Resolver(Dispositivos.Video(), _config.PlacaLink, _config.PlacaNome);
        _semPlaca = r.Motivo;
        if (r.Placa is null || r.Placa == _placa) return;
        _placa = r.Placa;
        _captura.Iniciar(_placa, _config.Modo);
        ReiniciarAudio();
    }

    void EscolherPlaca(DispositivoVideo placa)
    {
        if (_captura is null || _audio is null) return;
        _config = _config with { PlacaLink = placa.Link, PlacaNome = placa.Nome };
        var r = Placa.Resolver(Dispositivos.Video(), placa.Link, placa.Nome);
        _semPlaca = r.Motivo;
        _placa = r.Placa;
        if (_placa is not null)
        {
            _captura.Iniciar(_placa, _config.Modo); // o revezamento troca sem travar a janela
            ReiniciarAudio();
        }
        else
        {
            // Sumiu entre abrir o menu e clicar: para a placa antiga (raro; pode esperar até 3 s).
            _captura.Parar();
            _audio.Parar();
        }
        SalvarConfiguracao();
    }

    void EscolherEntrada(string? entrada)
    {
        _config = _config with { EntradaSom = entrada };
        ReiniciarAudio();
        SalvarConfiguracao();
    }

    void ReiniciarAudio()
    {
        if (_audio is null) return;
        _audio.Placa = _placa;
        _audio.EntradaEscolhida = _config.EntradaSom;
        try { _audio.Iniciar(); }
        catch (Exception ex) { Registro.Erro("áudio.iniciar", ex); }
    }

    ContextMenuStrip MontarMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var placas = new ToolStripMenuItem("Placa");
            foreach (var d in Dispositivos.Video())
                placas.DropDownItems.Add(new ToolStripMenuItem(d.Nome, null, (_, _) => EscolherPlaca(d))
                { Checked = _placa is not null && string.Equals(d.Link, _placa.Link, StringComparison.OrdinalIgnoreCase) });
            if (placas.DropDownItems.Count == 0) placas.DropDownItems.Add(new ToolStripMenuItem("(nenhuma câmera encontrada)") { Enabled = false });
            menu.Items.Add(placas);
            foreach (var m in _captura?.ModosDaPlaca ?? [])
                menu.Items.Add(new ToolStripMenuItem(m.Nome, null, (_, _) => TrocarModo(m)) { Checked = m == _captura?.ModoAtual });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Modo suave (como o OBS)", null, (_, _) => DefinirSuave(true))
            { Checked = _tela?.Suave == true });
            menu.Items.Add(new ToolStripMenuItem("Menor atraso", null, (_, _) => DefinirSuave(false))
            { Checked = _tela?.Suave == false });
            menu.Items.Add(new ToolStripSeparator());

            var entrada = new ToolStripMenuItem("Entrada de som");
            entrada.DropDownItems.Add(new ToolStripMenuItem("Automático (da placa)", null, (_, _) => EscolherEntrada(null))
            { Checked = _config.EntradaSom is null });
            if (_audio is not null)
                foreach (var e in _audio.Entradas())
                    entrada.DropDownItems.Add(new ToolStripMenuItem(e.Nome, null, (_, _) => EscolherEntrada(e.Id))
                    { Checked = _config.EntradaSom == e.Id });
            entrada.DropDownItems.Add(new ToolStripMenuItem("Nenhuma", null, (_, _) => EscolherEntrada(EntradaSom.Nenhuma))
            { Checked = _config.EntradaSom == EntradaSom.Nenhuma });
            menu.Items.Add(entrada);

            var saida = new ToolStripMenuItem("Saída de som");
            saida.DropDownItems.Add(new ToolStripMenuItem("Padrão do Windows", null, (_, _) => FixarSaida(null))
            { Checked = _audio?.SaidaFixaId is null });
            if (_audio is not null)
                foreach (var (id, nome) in _audio.Saidas())
                    saida.DropDownItems.Add(new ToolStripMenuItem(nome, null, (_, _) => FixarSaida(id))
                    { Checked = _audio.SaidaFixaId == id });
            menu.Items.Add(saida);

            var volume = new ToolStripMenuItem("Volume");
            foreach (var pct in new[] { 100, 75, 50, 25, 10 })
                volume.DropDownItems.Add(new ToolStripMenuItem($"{pct}%", null, (_, _) => DefinirVolume(pct / 100f))
                { Checked = _audio is not null && Math.Abs(_audio.Volume - pct / 100f) < 0.001f });
            menu.Items.Add(volume);
            menu.Items.Add(new ToolStripMenuItem("Mudo", null, (_, _) => AlternarMudo())
            { Checked = _audio?.Mudo == true, ShortcutKeyDisplayString = "M" });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Tela cheia", null, (_, _) => AlternarTelaCheia())
            { Checked = _telaCheia, ShortcutKeyDisplayString = "F11" });
            menu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => Close()));
        };
        menu.Items.Add("…"); // o Opening só dispara com pelo menos um item
        return menu;
    }

    void DefinirSuave(bool suave)
    {
        if (_tela is null) return;
        if (suave) _tela.IniciarSuave(); else _tela.PararSuave();
        SalvarConfiguracao();
    }

    void FixarSaida(string? id)
    {
        if (_audio is null) return;
        _audio.SaidaFixaId = id;
        SalvarConfiguracao();
    }

    void DefinirVolume(float v)
    {
        if (_audio is null) return;
        _audio.Volume = v;
        _audio.Mudo = false;
        SalvarConfiguracao();
    }

    // --- sem sinal e reconexão

    void Vigiar()
    {
        if (_placa is null && ++_tiquesSemPlaca >= 4)
        {
            _tiquesSemPlaca = 0;
            try { ProcurarPlaca(); }
            catch (Exception ex) { Registro.Erro("placa.procurar", ex); }
        }
        var titulo = _captura?.ModoAtual is { } modo && _placa is not null ? modo.Titulo : "Lumina";
        if (Text != titulo) { Text = titulo; OnResize(EventArgs.Empty); }

        bool semSinal = _captura is null || _placa is null || _captura.MsDesdeUltimoQuadro > 1000;
        var problema = _placa is null && _semPlaca is { } motivo ? Problemas.De(motivo) : _captura?.Problema ?? ProblemaCaptura.Nenhum;
        var texto = Problemas.Mensagem(problema, _config.PlacaNome);
        if (_semSinal.Text != texto) _semSinal.Text = texto;
        if (_semSinal.Visible != semSinal)
        {
            _semSinal.Visible = semSinal;
            _painel.Visible = !semSinal;
            if (semSinal) _semSinal.BringToFront();
        }

        if (_audio is not null && _placa is not null && !_audio.Ativo && _config.EntradaSom != EntradaSom.Nenhuma && ++_tiquesSemAudio >= 4)
        {
            _tiquesSemAudio = 0;
            try { _audio.Iniciar(); }
            catch (Exception ex) { Registro.Erro("áudio.reiniciar", ex); }
        }
    }

    // --- memória

    void SalvarConfiguracao()
    {
        var normal = _telaCheia ? _limitesNormais : (WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
        _config = _config with
        {
            X = normal.X,
            Y = normal.Y,
            Largura = normal.Width,
            Altura = normal.Height,
            Maximizada = _telaCheia ? _maximizadaAntesDaTelaCheia : WindowState == FormWindowState.Maximized,
            TelaCheia = _telaCheia,
            SaidaFixaId = _audio?.SaidaFixaId,
            Volume = _audio?.Volume ?? _config.Volume,
            Mudo = _forcarMudo ? _config.Mudo : _audio?.Mudo ?? _config.Mudo,
            ModoSuave = _tela?.Suave ?? _config.ModoSuave,
        };
        try { _armazem.Salvar(_config); }
        catch (Exception ex) { Registro.Erro("config.salvar", ex); }
    }

    static Retangulo Para(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    // --- diagnóstico (threads de captura)

    void AoQuadro(double atrasoMs)
    {
        if (_contador.Registrar(_relogio.Elapsed) is double fps) Registro.Diagnostico($"fps={fps:F1}");
        if (_atraso.Registrar(atrasoMs) is { } r)
            Registro.Diagnostico($"atraso chegada→tela: mediana={r.Mediana:F1} p95={r.P95:F1} máx={r.Maximo:F1} ms");
    }

    void AoBlocoAudio(float[] esquerdo, float[] direito)
    {
        _esquerdo.AddRange(esquerdo);
        _direito.AddRange(direito);
        if (_esquerdo.Count < 24000) return; // meio segundo a 48 kHz
        var fe = Frequencia.Estimar(_esquerdo.ToArray(), 48000);
        var fd = Frequencia.Estimar(_direito.ToArray(), 48000);
        Registro.Diagnostico($"audio esq={(fe is null ? "-" : $"{fe:F0}Hz")} dir={(fd is null ? "-" : $"{fd:F0}Hz")}");
        _esquerdo.Clear();
        _direito.Clear();
    }
}
```

- [ ] **Step 2: Compilar sem aviso e testar**

Run: `dotnet build` (se o Lumina estiver aberto e travar a cópia: `dotnet build src/Lumina -p:OutDir=<pasta temporária>\`) e `dotnet test`
Expected: `0 Aviso(s)`, `0 Erro(s)`; `Aprovado: 87` (simulado em 03/10/2026).

- [ ] **Step 3: Commit**

```powershell
git add -A src/Lumina
git commit -m "App: escolher a placa, modos da placa e entrada de som no menu"
```

- [ ] **Step 4: Prova do andar 2 (Douglas, pelo `ferramentas\diagnostico.cmd`; 1ª abertura pode ser isolada pelo Norton — abrir de novo)**

| Ação | Esperado |
|---|---|
| Abrir (config antiga, sem placa salva) | MS2109 abre sozinha, com som; título "Lumina — 720p60" (ou o modo salvo) |
| Clique direito | "Placa" com as câmeras e a MS2109 ("USB Video") marcada; modos **720p30, 720p60, 1080p30** (sem 1080p60); "Entrada de som" com "Automático (da placa)" marcado |
| Jogar uns minutos no modo suave | igual a antes |

`lumina.log` deve mostrar `captura aberta: USB Video 720p60` e `áudio: Interface de áudio digital (USB Digital Audio): anunciado … 96000Hz 1 channels → usado … 48000Hz 2 channels`.

---

### Task 4: Andares 3 e 4 — outra placa de verdade e casos ruins

**Files:**
- Modify: `README.md` (seção "Antes de usar"), `docs/medicoes.md` (seção nova)

- [ ] **Step 1: Prova do andar 3 — a C270 como "outra placa" (Douglas)**

| Ação | Esperado |
|---|---|
| Placa → **Logi C270 HD WebCam** | imagem da webcam; título "Lumina — 720p30"; menu de modos só com **720p30** |
| Entrada de som (automático) | o som vem do **microfone da C270** (`lumina.log`: `áudio: Microfone (Logi C270 HD WebCam): …`, sem correção 96k) |
| Placa → **USB Video** (MS2109) | volta a imagem do Switch e o som dele, sem fechar o app nem congelar a janela |
| Com a MS2109 escolhida, abrir a C270 em outro app (Câmera do Windows) | a webcam funciona: o Lumina soltou a C270 |

- [ ] **Step 2: Prova do andar 4 — casos ruins (Douglas)**

| Ação | Esperado |
|---|---|
| Com a MS2109 escolhida, tirar o USB dela | aviso de placa não encontrada; recolocar → volta sozinha |
| Escolher a C270, fechar o Lumina, desplugar a C270, abrir | aviso **"A placa "Logi C270 HD WebCam" não está conectada"** — e **não** abre a MS2109 sozinho; plugar a C270 → abre em até ~2 s |
| Entrada de som → **Nenhuma** | sem som; o log não repete a mesma linha a cada 2 s |
| Apagar `PlacaLink`/`PlacaNome` do `config.json`, desplugar a MS2109, abrir | **"Escolha a placa de captura no menu"**; nenhuma câmera acende |

- [ ] **Step 3: Documentar**

Em `README.md`, na seção "Antes de usar", trocar o item da placa por:

```markdown
- **Placa:** qualquer câmera que o Windows mostre (clique direito → **Placa**). A MS2109 (as "USB Video" genéricas
  com `VID_534D&PID_2109`) é escolhida sozinha na 1ª vez; outras placas, escolha no menu. O som vem sozinho do mesmo
  aparelho USB; se não vier, escolha em **Entrada de som**. Os modos do menu são os que a placa oferece (16:9, 720p+, 30/60).
```

Em `docs/medicoes.md`, uma seção "Escolher a placa (data)" com o resultado de cada linha das tabelas dos Steps 1–2.

- [ ] **Step 4: Commit**

```powershell
git add README.md docs/medicoes.md
git commit -m "Escolher a placa: provas e README"
```
