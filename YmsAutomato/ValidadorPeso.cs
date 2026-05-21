namespace YmsAutomato.Services
{
    // Estados internos da Máquina de Turing — NÃO confundir com EstadoLogistico do DFA
    public enum EstadoTuring
    {
        q_inicio,
        q_zero_inicial,
        q_inteiro,
        q_decimal,
        q_aceito,
        q_rejeitado
    }

    public record ResultadoValidacao(bool Valido, decimal Valor, string Motivo);

    /// <summary>
    /// Máquina de Turing para validação de formato e valor do peso.
    /// Fita: representação em string do valor decimal.
    /// Cabeçote: lê cada caractere da esquerda para a direita (somente leitura).
    /// </summary>
    public class ValidadorPeso
    {
        private const decimal PesoEsperado = 50_000m;

        public ResultadoValidacao Validar(decimal? peso)
        {
            // Guarda de entrada — reprovação direta antes de rodar a MT
            if (peso is null)
                return new ResultadoValidacao(false, 0, "Peso nulo.");
            if (peso < 0)
                return new ResultadoValidacao(false, 0, "Peso negativo.");

            // Monta a fita: string do valor em cultura invariante (ponto como separador decimal)
            var fita = peso.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Executa a MT lendo a fita caractere a caractere
            var estadoAtual = EstadoTuring.q_inicio;
            var cabecote = 0;

            while (estadoAtual != EstadoTuring.q_aceito && estadoAtual != EstadoTuring.q_rejeitado)
            {
                // '#' representa o símbolo branco (fim de fita)
                var simbolo = cabecote < fita.Length ? fita[cabecote] : '#';
                estadoAtual = Transicao(estadoAtual, simbolo);
                cabecote++;
            }

            if (estadoAtual == EstadoTuring.q_rejeitado)
                return new ResultadoValidacao(false, peso.Value, "Formato inválido: zeros extras ou símbolo inesperado.");

            return new ResultadoValidacao(true, peso.Value, "Formato válido.");
        }

        public bool PesoIgualEsperado(decimal valor) => valor == PesoEsperado;

        // δ: função de transição — (estado atual, símbolo lido) → próximo estado
        private static EstadoTuring Transicao(EstadoTuring estado, char simbolo) => (estado, simbolo) switch
        {
            // q_inicio: verifica o primeiro caractere
            (EstadoTuring.q_inicio, '#')                         => EstadoTuring.q_rejeitado,   // fita vazia
            (EstadoTuring.q_inicio, '-')                         => EstadoTuring.q_rejeitado,   // negativo
            (EstadoTuring.q_inicio, '0')                         => EstadoTuring.q_zero_inicial, // pode ser zero isolado
            (EstadoTuring.q_inicio, >= '1' and <= '9')           => EstadoTuring.q_inteiro,      // dígito normal

            // q_zero_inicial: "0" foi o primeiro dígito — verifica se é zero sozinho ou "0.xxx"
            (EstadoTuring.q_zero_inicial, '#')                   => EstadoTuring.q_aceito,       // "0" válido
            (EstadoTuring.q_zero_inicial, '.')                   => EstadoTuring.q_decimal,      // "0.xxx" ok
            (EstadoTuring.q_zero_inicial, >= '0' and <= '9')     => EstadoTuring.q_rejeitado,    // zero líder: "050000"

            // q_inteiro: lendo a parte inteira
            (EstadoTuring.q_inteiro, >= '0' and <= '9')          => EstadoTuring.q_inteiro,
            (EstadoTuring.q_inteiro, '.')                        => EstadoTuring.q_decimal,
            (EstadoTuring.q_inteiro, '#')                        => EstadoTuring.q_aceito,

            // q_decimal: lendo a parte decimal
            (EstadoTuring.q_decimal, >= '0' and <= '9')          => EstadoTuring.q_decimal,
            (EstadoTuring.q_decimal, '#')                        => EstadoTuring.q_aceito,

            // qualquer outro símbolo em qualquer estado → rejeita
            _                                                    => EstadoTuring.q_rejeitado
        };
    }
}
