using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.MediaFoundation;

namespace Lumina;

/// <summary>
/// Dono da GPU. A cadeia de imagens tem o tamanho do vídeo e o Windows estica para o painel
/// (Scaling.Stretch): copiar o quadro é tudo que se faz por quadro, sem shader.
/// Só a thread de captura chama Apresentar.
/// </summary>
sealed class Renderizador : IDisposable
{
    public ID3D11Device Dispositivo { get; }
    public IMFDXGIDeviceManager Gerente { get; }

    readonly ID3D11DeviceContext _ctx;
    readonly IntPtr _hwnd;
    IDXGISwapChain1? _cadeia;
    uint _largura, _altura;
    Format _formato;

    public Renderizador(IntPtr hwnd)
    {
        _hwnd = hwnd;
        D3D11.D3D11CreateDevice(null, DriverType.Hardware,
            DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport,
            new[] { FeatureLevel.Level_11_0 }, out ID3D11Device? dispositivo).CheckError();
        Dispositivo = dispositivo ?? throw new InvalidOperationException("sem dispositivo Direct3D 11");
        _ctx = Dispositivo.ImmediateContext;
        // O Media Foundation usa o mesmo dispositivo em outra thread.
        using (var mt = Dispositivo.QueryInterface<ID3D11Multithread>()) mt.SetMultithreadProtected(true);
        Gerente = MediaFactory.MFCreateDXGIDeviceManager();
        Gerente.ResetDevice(Dispositivo).CheckError();
    }

    public void Apresentar(ID3D11Texture2D textura, uint subrecurso)
    {
        var d = textura.Description;
        if (_cadeia is null || d.Width != _largura || d.Height != _altura || d.Format != _formato)
            CriarCadeia(d.Width, d.Height, d.Format);
        using var fundo = _cadeia!.GetBuffer<ID3D11Texture2D>(0);
        _ctx.CopySubresourceRegion(fundo, 0, 0, 0, 0, textura, subrecurso);
        _cadeia.Present(0, PresentFlags.None);
    }

    void CriarCadeia(uint largura, uint altura, Format formato)
    {
        _cadeia?.Dispose();
        using var dxgi = Dispositivo.QueryInterface<IDXGIDevice>();
        using var adaptador = dxgi.GetAdapter();
        using var fabrica = adaptador.GetParent<IDXGIFactory2>();
        var desc = new SwapChainDescription1
        {
            Width = largura,
            Height = altura,
            Format = formato,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = new SampleDescription(1, 0),
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
        };
        _cadeia = fabrica.CreateSwapChainForHwnd(Dispositivo, _hwnd, desc);
        fabrica.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAll);
        _largura = largura;
        _altura = altura;
        _formato = formato;
        Registro.Log($"tela: {largura}x{altura} {formato}");
    }

    public void Dispose()
    {
        _cadeia?.Dispose();
        Gerente.Dispose();
        _ctx.Dispose();
        Dispositivo.Dispose();
    }
}
