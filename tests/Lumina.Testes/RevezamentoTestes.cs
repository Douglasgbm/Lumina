using Lumina.Nucleo;

namespace Lumina.Testes;

public class RevezamentoTestes
{
    // Imita a captura: abrir demora e não olha o sinal (como o MFCreateDeviceSource, ~4 s medidos),
    // depois lê até mandarem parar.
    static Action<Revezamento.Vez> TrabalhoLento(string nome, List<string> ordem, int[] simultaneos, int[] maximo)
    {
        return vez =>
        {
            int agora = Interlocked.Increment(ref simultaneos[0]);
            lock (maximo) maximo[0] = Math.Max(maximo[0], agora);
            lock (ordem) ordem.Add(nome);
            Thread.Sleep(150); // "abrindo"
            while (!vez.Parar) Thread.Sleep(5); // "lendo"
            Interlocked.Decrement(ref simultaneos[0]);
        };
    }

    [Fact]
    public void Trocar_durante_a_abertura_nunca_deixa_dois_trabalhos_ao_mesmo_tempo()
    {
        var r = new Revezamento("teste");
        var ordem = new List<string>();
        int[] simultaneos = [0], maximo = [0];
        r.Iniciar(TrabalhoLento("A", ordem, simultaneos, maximo));
        Thread.Sleep(30);
        r.Iniciar(TrabalhoLento("B", ordem, simultaneos, maximo));
        Thread.Sleep(30);
        r.Iniciar(TrabalhoLento("C", ordem, simultaneos, maximo));
        Thread.Sleep(400);
        Assert.True(r.Parar(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, maximo[0]);
        Assert.Equal("C", ordem[^1]); // o último pedido é o que vale
    }

    [Fact]
    public void O_trabalho_antigo_ve_o_proprio_sinal_mesmo_depois_de_um_novo_comecar()
    {
        var r = new Revezamento("teste");
        Revezamento.Vez? primeira = null;
        using var comecou = new ManualResetEventSlim();
        r.Iniciar(vez => { primeira = vez; comecou.Set(); while (!vez.Parar) Thread.Sleep(5); });
        comecou.Wait(TimeSpan.FromSeconds(2));
        r.Iniciar(vez => { while (!vez.Parar) Thread.Sleep(5); });
        Assert.True(primeira!.Parar);
        Assert.True(r.Parar(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Parar_avisa_quando_o_trabalho_nao_termina_no_limite()
    {
        var r = new Revezamento("teste");
        using var solta = new ManualResetEventSlim();
        r.Iniciar(_ => solta.Wait(TimeSpan.FromSeconds(5))); // preso, ignorando o sinal
        Thread.Sleep(30);
        Assert.False(r.Parar(TimeSpan.FromMilliseconds(100)));
        solta.Set();
    }

    [Fact]
    public void Parar_que_estourou_o_tempo_ainda_faz_o_proximo_esperar()
    {
        // Revisão final (05/10/2026): o Parar esquecia a thread presa e o Iniciar seguinte rodava junto com ela.
        var r = new Revezamento("teste");
        using var solta = new ManualResetEventSlim();
        using var rodou = new ManualResetEventSlim();
        int simultaneos = 0, maximo = 0;
        void Conta() { int agora = Interlocked.Increment(ref simultaneos); lock (r) maximo = Math.Max(maximo, agora); }
        r.Iniciar(_ => { Conta(); solta.Wait(TimeSpan.FromSeconds(5)); Interlocked.Decrement(ref simultaneos); });
        Thread.Sleep(30);
        Assert.False(r.Parar(TimeSpan.FromMilliseconds(100)));
        r.Iniciar(vez => { Conta(); rodou.Set(); Interlocked.Decrement(ref simultaneos); });
        Thread.Sleep(150);
        Assert.False(rodou.IsSet); // ainda esperando a presa terminar
        solta.Set();
        Assert.True(rodou.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, maximo);
        Assert.True(r.Parar(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Parar_sem_nada_rodando_devolve_verdadeiro()
    {
        Assert.True(new Revezamento("teste").Parar(TimeSpan.FromMilliseconds(10)));
    }
}
