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
    readonly ArmazemConfiguracao _armazem = new(Path.Combine(Registro.Pasta, "config.json"));
    readonly bool _diagnostico;
    readonly bool _forcarMudo;

    Renderizador? _tela;
    CapturaVideo? _captura;
    MotorAudio? _audio;
    ModoVideo _modo;
    Configuracao _config;
    Rectangle _limitesNormais;
    bool _maximizadaAntesDaTelaCheia;
    bool _telaCheia;

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
        _modo = ModoVideo.PorNome(_config.Modo);

        Text = "Lumina";
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

        ContextMenuStrip = MontarMenu();
        _painel.ContextMenuStrip = ContextMenuStrip;
        DoubleClick += (_, _) => AlternarTelaCheia();
        _painel.DoubleClick += (_, _) => AlternarTelaCheia();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _tela = new Renderizador(_painel.Handle);
        _captura = new CapturaVideo(_tela, _diagnostico ? AoQuadro : null);
        _captura.Iniciar(_modo);

        _audio = new MotorAudio(SynchronizationContext.Current!, _diagnostico ? AoBlocoAudio : null)
        {
            Volume = _config.Volume,
            Mudo = _config.Mudo || _forcarMudo,
        };
        _audio.SaidaFixaId = _config.SaidaFixaId;
        try { _audio.Iniciar(); }
        catch (Exception ex) { Registro.Erro("áudio.iniciar", ex); }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_config.TelaCheia) AlternarTelaCheia();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        var r = Enquadramento.Encaixar(ClientSize.Width, ClientSize.Height, _modo.Largura, _modo.Altura);
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
        SalvarConfiguracao();
        _captura?.Dispose();
        _audio?.Dispose();
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
        if (modo == _modo || _captura is null) return;
        _modo = modo;
        OnResize(EventArgs.Empty);
        _captura.Iniciar(_modo);
        SalvarConfiguracao();
    }

    ContextMenuStrip MontarMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            foreach (var m in new[] { ModoVideo.Hd60, ModoVideo.FullHd30 })
                menu.Items.Add(new ToolStripMenuItem(m.Nome, null, (_, _) => TrocarModo(m)) { Checked = m == _modo });
            menu.Items.Add(new ToolStripSeparator());

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
            Modo = _modo.Nome,
            SaidaFixaId = _audio?.SaidaFixaId,
            Volume = _audio?.Volume ?? _config.Volume,
            Mudo = _forcarMudo ? _config.Mudo : _audio?.Mudo ?? _config.Mudo,
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
