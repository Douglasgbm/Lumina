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
    FormWindowState _ultimoEstadoVisivel = FormWindowState.Normal;
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
        if (_tela is not null) return; // handle recriado: não montar uma segunda captura segurando a placa
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
        // Fechar pela barra de tarefas com a janela minimizada não pode esquecer que ela estava maximizada.
        if (WindowState != FormWindowState.Minimized) _ultimoEstadoVisivel = WindowState;
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
            var telas = Screen.AllScreens.Select(t => Para(t.WorkingArea)).ToList();
            var r = Enquadramento.GarantirVisivel(Para(_limitesNormais), telas, Para(Screen.PrimaryScreen!.WorkingArea));
            Bounds = new Rectangle(r.X, r.Y, r.Largura, r.Altura); // o monitor de antes pode ter sido desligado
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
        if (_placa is not null && string.Equals(placa.Link, _placa.Link, StringComparison.OrdinalIgnoreCase)
            && _captura.ModoAtual is not null && _captura.Problema == ProblemaCaptura.Nenhum)
            return; // já é a placa aberta e funcionando: reiniciar só derrubaria imagem e som por segundos
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

    /// <summary>
    /// A GPU foi perdida (driver reiniciado/atualizado): sem isso ficava "sem sinal" até reabrir (revisão, 03/10/2026).
    /// Se a captura ainda estiver abrindo a placa, tenta de novo no próximo tique do vigia.
    /// </summary>
    void RecriarTela()
    {
        if (_tela is null || _captura is null) return;
        if (!_captura.Parar()) return;
        Registro.Log("tela: recriando a GPU");
        bool suave = _tela.Suave;
        _tela.Dispose();
        _tela = new Renderizador(_painel.Handle);
        if (_diagnostico) _tela.AoExibir = AoQuadro;
        if (suave) _tela.IniciarSuave();
        _captura = new CapturaVideo(_tela, _diagnostico ? AoQuadro : null);
        if (_placa is not null) _captura.Iniciar(_placa, _config.Modo);
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
        if (_placa is null)
        {
            _audio.Parar(); // sem placa, sem som: um microfone escolhido à mão tocaria nas caixas
            return;
        }
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
            var entradas = _audio?.Entradas() ?? [];
            bool escolhidaSumiu = _config.EntradaSom is { } idEscolhida && idEscolhida != EntradaSom.Nenhuma && entradas.All(e => e.Id != idEscolhida);
            entrada.DropDownItems.Add(new ToolStripMenuItem(
                escolhidaSumiu ? "Automático (a escolhida não está conectada)" : "Automático (da placa)", null, (_, _) => EscolherEntrada(null))
            { Checked = _config.EntradaSom is null || escolhidaSumiu });
            if (_audio is not null)
                foreach (var e in entradas)
                    entrada.DropDownItems.Add(new ToolStripMenuItem(e.Nome, null, (_, _) => EscolherEntrada(e.Id))
                    { Checked = _config.EntradaSom == e.Id });
            entrada.DropDownItems.Add(new ToolStripMenuItem("Nenhuma", null, (_, _) => EscolherEntrada(EntradaSom.Nenhuma))
            { Checked = _config.EntradaSom == EntradaSom.Nenhuma });
            menu.Items.Add(entrada);

            var saida = new ToolStripMenuItem("Saída de som");
            saida.DropDownItems.Add(new ToolStripMenuItem("Padrão do Windows", null, (_, _) => FixarSaida(null))
            { Checked = _audio?.SaidaFixaId is null });
            if (_audio is not null)
            {
                var saidas = _audio.Saidas();
                foreach (var (id, nome) in saidas)
                    saida.DropDownItems.Add(new ToolStripMenuItem(nome, null, (_, _) => FixarSaida(id))
                    { Checked = _audio.SaidaFixaId == id });
                if (_audio.SaidaFixaId is { } fixa && saidas.All(x => x.Id != fixa))
                    saida.DropDownItems.Add(new ToolStripMenuItem("(a fixada não está conectada — tocando na padrão)")
                    { Checked = true, Enabled = false });
            }
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
        if (_tela?.Perdido == true) RecriarTela();
        if (_placa is null && ++_tiquesSemPlaca >= 4)
        {
            _tiquesSemPlaca = 0;
            try { ProcurarPlaca(); }
            catch (Exception ex) { Registro.Erro("placa.procurar", ex); }
        }
        var titulo = _captura?.ModoAtual is { } modo && _placa is not null ? modo.Titulo : "Lumina";
        if (Text != titulo) { Text = titulo; OnResize(EventArgs.Empty); }

        bool semSinal = _captura is null || _placa is null || _captura.MsDesdeUltimoQuadro > 1000;
        var problema = Problemas.NaTela(_captura?.Problema ?? ProblemaCaptura.Nenhum, _semPlaca, _placa is not null);
        var texto = Problemas.Mensagem(problema, _placa?.Nome ?? _config.PlacaNome);
        if (_semSinal.Text != texto) _semSinal.Text = texto;
        if (_semSinal.Visible != semSinal)
        {
            _semSinal.Visible = semSinal;
            _painel.Visible = !semSinal;
            if (semSinal) _semSinal.BringToFront();
        }

        // A mesma placa voltou em outra porta USB: o som é do aparelho novo (antes ficava mudo até reabrir).
        if (_placa is not null && _captura?.PlacaAberta is { } aberta
            && !string.Equals(aberta.Link, _placa.Link, StringComparison.OrdinalIgnoreCase))
        {
            Registro.Log($"placa em outra porta USB: {aberta.Nome}");
            _placa = aberta;
            ReiniciarAudio();
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
            Maximizada = _telaCheia ? _maximizadaAntesDaTelaCheia
                : (WindowState == FormWindowState.Minimized ? _ultimoEstadoVisivel : WindowState) == FormWindowState.Maximized,
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
