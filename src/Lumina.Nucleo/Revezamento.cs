namespace Lumina.Nucleo;

/// <summary>
/// Um trabalho por vez numa thread própria. Iniciar avisa o trabalho anterior e a thread nova só
/// começa depois que ele termina: nunca há dois rodando juntos, mesmo se o anterior estiver preso
/// num passo que não olha o aviso (abrir a placa leva ~4 s, medido em 03/10/2026).
/// Iniciar e Parar são chamados só de uma thread (a da janela).
/// </summary>
public sealed class Revezamento(string nome)
{
    /// <summary>O aviso de parada de UM trabalho; cada Iniciar cria o seu.</summary>
    public sealed class Vez
    {
        volatile bool _parar;
        public bool Parar => _parar;
        internal void Avisar() => _parar = true;
    }

    Thread? _fio;
    Vez? _vez;

    public void Iniciar(Action<Vez> trabalho)
    {
        var anterior = _fio;
        _vez?.Avisar();
        var vez = new Vez();
        var fio = new Thread(() =>
        {
            anterior?.Join();
            if (!vez.Parar) trabalho(vez);
        }) { IsBackground = true, Name = nome };
        _fio = fio;
        _vez = vez;
        fio.Start();
    }

    /// <summary>
    /// Avisa e espera até o limite. Falso: o trabalho ainda roda (vai terminar sozinho depois) e continua
    /// guardado — o próximo Iniciar espera por ele (antes era esquecido e os dois rodavam juntos; revisão final, 05/10/2026).
    /// </summary>
    public bool Parar(TimeSpan limite)
    {
        _vez?.Avisar();
        _vez = null;
        var fio = _fio;
        if (fio is null || fio.Join(limite))
        {
            _fio = null;
            return true;
        }
        return false;
    }
}
