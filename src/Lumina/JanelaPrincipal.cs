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
    readonly bool _diagnostico;

    Renderizador? _tela;
    CapturaVideo? _captura;
    ModoVideo _modo = ModoVideo.Hd60;

    // Diagnóstico
    readonly ContadorQuadros _contador = new();
    readonly Stopwatch _relogio = Stopwatch.StartNew();
    readonly ResumoAtraso _atraso = new();

    /// <param name="forcarMudo">Usado a partir do andar 2 (Task 5).</param>
    public JanelaPrincipal(bool diagnostico, bool forcarMudo)
    {
        _diagnostico = diagnostico;

        Text = "Lumina";
        BackColor = Color.Black;
        KeyPreview = true;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(320, 180);
        Bounds = new Rectangle(100, 100, 1280, 720);
        Controls.Add(_painel);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _tela = new Renderizador(_painel.Handle);
        _captura = new CapturaVideo(_tela, _diagnostico ? AoQuadro : null);
        _captura.Iniciar(_modo);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        var r = Enquadramento.Encaixar(ClientSize.Width, ClientSize.Height, _modo.Largura, _modo.Altura);
        _painel.Bounds = new Rectangle(r.X, r.Y, r.Largura, r.Altura);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _captura?.Dispose();
        _tela?.Dispose();
        base.OnFormClosing(e);
    }

    // --- diagnóstico (thread de captura)

    void AoQuadro(double atrasoMs)
    {
        if (_contador.Registrar(_relogio.Elapsed) is double fps) Registro.Diagnostico($"fps={fps:F1}");
        if (_atraso.Registrar(atrasoMs) is { } r)
            Registro.Diagnostico($"atraso chegada→tela: mediana={r.Mediana:F1} p95={r.P95:F1} máx={r.Maximo:F1} ms");
    }
}
