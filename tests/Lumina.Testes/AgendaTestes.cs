using Lumina.Nucleo;

namespace Lumina.Testes;

public class AgendaTestes
{
    const long Ms = 10_000; // carimbos em 100 ns

    [Fact]
    public void Nada_para_mostrar_antes_do_atraso_passar()
    {
        var a = new Agenda<int>(4);
        a.Adicionar(100 * Ms, 1, out _);
        Assert.Null(a.Escolher(agora: 120 * Ms, atraso: 30 * Ms));
    }

    [Fact]
    public void Mostra_o_mais_novo_ja_vencido_e_descarta_os_mais_velhos()
    {
        var a = new Agenda<int>(4);
        a.Adicionar(0, 1, out _);
        a.Adicionar(16 * Ms, 2, out _);
        a.Adicionar(33 * Ms, 3, out _);
        var e = a.Escolher(agora: 50 * Ms, atraso: 30 * Ms); // vence até 20 ms
        Assert.NotNull(e);
        Assert.Equal(2, e!.Value.Item);
        Assert.Equal([1], e.Value.Descartados);
        Assert.Equal(1, a.Quantidade); // o 3 continua esperando
    }

    [Fact]
    public void Mesmo_quadro_nao_e_escolhido_duas_vezes()
    {
        var a = new Agenda<int>(4);
        a.Adicionar(0, 1, out _);
        Assert.NotNull(a.Escolher(40 * Ms, 30 * Ms));
        Assert.Null(a.Escolher(41 * Ms, 30 * Ms));
    }

    [Fact]
    public void Fila_cheia_devolve_o_mais_velho_para_reaproveitar()
    {
        var a = new Agenda<int>(2);
        Assert.False(a.Adicionar(0, 0, out _)); // índice 0 de propósito: não pode parecer "despejado"
        Assert.False(a.Adicionar(16 * Ms, 2, out _));
        Assert.True(a.Adicionar(33 * Ms, 3, out int despejado));
        Assert.Equal(0, despejado);
        Assert.Equal(2, a.Quantidade);
    }

    [Fact]
    public void Esvaziar_devolve_tudo_que_estava_na_fila()
    {
        var a = new Agenda<int>(4);
        a.Adicionar(0, 1, out _);
        a.Adicionar(16 * Ms, 2, out _);
        Assert.Equal([1, 2], a.Esvaziar());
        Assert.Equal(0, a.Quantidade);
    }

    [Theory]
    [InlineData(30.0, 34.0)]  // p95 medido no Rise (03/10/2026) + folga
    [InlineData(2.0, 10.0)]   // nunca abaixo de 10 ms
    [InlineData(300.0, 80.0)] // um engasgo não leva o atraso para meio segundo
    public void Atraso_suave_cobre_o_p95_com_folga_e_limites(double p95, double esperado)
    {
        Assert.Equal(esperado, AtrasoSuave.De(new ResumoAtraso.Resumo(20, p95, p95)));
    }

    [Fact]
    public void Atraso_suave_inicial_e_35_ms()
    {
        Assert.Equal(35.0, AtrasoSuave.Inicial);
    }
}
