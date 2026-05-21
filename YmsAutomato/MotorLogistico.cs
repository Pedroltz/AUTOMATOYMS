using YmsAutomato.Models;

namespace YmsAutomato.Services
{
    /// <summary>
    /// Motor de Autômatos para Validação Logística.
    /// Implementação de um Autômato Finito Determinístico (DFA) com Estados Hierárquicos.
    /// </summary>
    public class MotorLogistico
    {
        // Definição da 5-tupla {Q, Σ, δ, q0, F}
        // Q: Conjunto de Estados (EstadoLogistico)
        // Σ: Alfabeto de Eventos (EventoLogistico)
        // δ: Função de Transição (matrizTransicao)
        // q0: Estado Inicial (Portaria)
        // F: Estado Final (Finalizado)

        private EstadoLogistico _estadoAtual = EstadoLogistico.Portaria;
        private SubEstadoPesagem _subEstadoAtual = SubEstadoPesagem.Nenhum;
        private decimal _pesoCapturado;

        private readonly ValidadorPeso _validadorPeso = new();

        // Tabela de Transição: (EstadoAtual, Evento) -> (ProximoEstado, ProximoSubEstado)
        private readonly Dictionary<(EstadoLogistico, EventoLogistico), (EstadoLogistico, SubEstadoPesagem)> _matrizTransicao;

        public MotorLogistico()
        {
            _matrizTransicao = new Dictionary<(EstadoLogistico, EventoLogistico), (EstadoLogistico, SubEstadoPesagem)>
            {
                // De Portaria para Pesagem Entrada
                { (EstadoLogistico.Portaria, EventoLogistico.evento_rfid_portaria), (EstadoLogistico.Pesagem_Entrada, SubEstadoPesagem.Aguardando) },

                // Fluxo Pesagem Entrada (Hierárquico)
                { (EstadoLogistico.Pesagem_Entrada, EventoLogistico.evento_balanca_entrada), (EstadoLogistico.Pesagem_Entrada, SubEstadoPesagem.Estabilizando) },
                { (EstadoLogistico.Pesagem_Entrada, EventoLogistico.evento_estabilizacao),   (EstadoLogistico.Pesagem_Entrada, SubEstadoPesagem.Capturado) },
                { (EstadoLogistico.Pesagem_Entrada, EventoLogistico.evento_captura),         (EstadoLogistico.Doca,            SubEstadoPesagem.Nenhum) },

                // Doca para Pesagem Saída
                { (EstadoLogistico.Doca, EventoLogistico.evento_liberacao_doca), (EstadoLogistico.Pesagem_Saida, SubEstadoPesagem.Aguardando) },

                // Fluxo Pesagem Saída (Hierárquico)
                { (EstadoLogistico.Pesagem_Saida, EventoLogistico.evento_balanca_saida), (EstadoLogistico.Pesagem_Saida, SubEstadoPesagem.Estabilizando) },
                { (EstadoLogistico.Pesagem_Saida, EventoLogistico.evento_estabilizacao), (EstadoLogistico.Pesagem_Saida, SubEstadoPesagem.Capturado) },
                { (EstadoLogistico.Pesagem_Saida, EventoLogistico.evento_captura),       (EstadoLogistico.Finalizado,    SubEstadoPesagem.Nenhum) },

                // Aprovação / Rejeição manual (de Aguardando_Aprovacao)
                { (EstadoLogistico.Aguardando_Aprovacao, EventoLogistico.evento_aprovacao_manual), (EstadoLogistico.Doca,  SubEstadoPesagem.Nenhum) },
                { (EstadoLogistico.Aguardando_Aprovacao, EventoLogistico.evento_rejeicao_manual),  (EstadoLogistico.Erro,  SubEstadoPesagem.Nenhum) },
            };
        }

        public (bool Sucesso, RespostaEstado Resposta) ProcessarEvento(EventoLogistico evento, decimal? peso = null)
        {
            var chave = (_estadoAtual, evento);

            if (_matrizTransicao.TryGetValue(chave, out var transicao))
            {
                if (!ValidarSubTransicao(evento))
                {
                    return (false, new RespostaEstado(_estadoAtual, _subEstadoAtual,
                        $"Transição de sub-estado inválida para o evento {evento} no estado {_estadoAtual}."));
                }

                // Intercepta captura de peso na entrada para validação pela Máquina de Turing
                if (_estadoAtual == EstadoLogistico.Pesagem_Entrada && evento == EventoLogistico.evento_captura)
                {
                    var resultado = _validadorPeso.Validar(peso);

                    if (!resultado.Valido)
                    {
                        _estadoAtual = EstadoLogistico.Erro;
                        _subEstadoAtual = SubEstadoPesagem.Nenhum;
                        return (false, new RespostaEstado(_estadoAtual, _subEstadoAtual,
                            $"Peso inválido: {resultado.Motivo}"));
                    }

                    _pesoCapturado = resultado.Valor;

                    if (!_validadorPeso.PesoIgualEsperado(resultado.Valor))
                    {
                        _estadoAtual = EstadoLogistico.Aguardando_Aprovacao;
                        _subEstadoAtual = SubEstadoPesagem.Nenhum;
                        return (true, new RespostaEstado(_estadoAtual, _subEstadoAtual,
                            $"Peso {resultado.Valor:N0}kg diverge do esperado (50.000kg). Aguardando aprovação manual.",
                            _pesoCapturado));
                    }
                }

                _estadoAtual = transicao.Item1;
                _subEstadoAtual = transicao.Item2;
                return (true, new RespostaEstado(_estadoAtual, _subEstadoAtual, "Transição realizada com sucesso."));
            }

            return (false, new RespostaEstado(_estadoAtual, _subEstadoAtual,
                $"Transição inválida: Não é possível processar {evento} no estado {_estadoAtual}."));
        }

        private bool ValidarSubTransicao(EventoLogistico evento)
        {
            // Lógica específica para garantir a ordem interna da pesagem
            if (_estadoAtual == EstadoLogistico.Pesagem_Entrada || _estadoAtual == EstadoLogistico.Pesagem_Saida)
            {
                return evento switch
                {
                    EventoLogistico.evento_balanca_entrada or EventoLogistico.evento_balanca_saida => _subEstadoAtual == SubEstadoPesagem.Aguardando,
                    EventoLogistico.evento_estabilizacao => _subEstadoAtual == SubEstadoPesagem.Estabilizando,
                    EventoLogistico.evento_captura => _subEstadoAtual == SubEstadoPesagem.Capturado,
                    _ => true
                };
            }
            return true;
        }

        public RespostaEstado ObterEstadoAtual() =>
            new RespostaEstado(_estadoAtual, _subEstadoAtual, "Estado atual consultado.",
                _estadoAtual == EstadoLogistico.Aguardando_Aprovacao ? _pesoCapturado : null);

        public void Reset()
        {
            _estadoAtual = EstadoLogistico.Portaria;
            _subEstadoAtual = SubEstadoPesagem.Nenhum;
            _pesoCapturado = 0;
        }
    }
}
