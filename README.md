# YMS Protocol Engine

Sistema de gerenciamento de pátio (Yard Management System) implementado como um Autômato Finito Determinístico (DFA) com estados hierárquicos e validação de pesos via Máquinas de Turing, desenvolvido em ASP.NET Core 10.0.

---

## Visao Geral

O projeto simula o fluxo logístico completo de cargas em um armazém: desde a entrada pela portaria com leitura de RFID, passando por duas etapas de pesagem (entrada e saída), armazenamento em doca, até a finalização do processo. A lógica de controle é modelada formalmente como um DFA, garantindo determinismo e rastreabilidade em cada transição de estado. Pesos capturados são validados por Máquinas de Turing antes de qualquer transição.

---

## Arquitetura

### Fundamento Teorico

O sistema é definido pela 5-tupla formal de um Autômato Finito Determinístico:

```
M = (Q, Σ, δ, q0, F)
```

| Componente | Definicao |
|---|---|
| **Q** | {Portaria, Pesagem_Entrada, Doca, Pesagem_Saida, Aguardando_Aprovacao, Aguardando_Aprovacao_Saida, Finalizado, Erro} |
| **Σ** | {evento_rfid_portaria, evento_balanca_entrada, evento_estabilizacao, evento_captura, evento_liberacao_doca, evento_balanca_saida, evento_aprovacao_manual, evento_rejeicao_manual, ev_saida_finalizada} |
| **δ** | Funcao de transicao implementada como dicionario de tuplas |
| **q0** | Portaria |
| **F** | {Finalizado} |

### Tabela de Transicao (δ)

| Estado Atual | Evento | Proximo Estado | Sub-Estado |
|---|---|---|---|
| Portaria | evento_rfid_portaria | Pesagem_Entrada | Aguardando |
| Pesagem_Entrada | evento_balanca_entrada | Pesagem_Entrada | Estabilizando |
| Pesagem_Entrada | evento_estabilizacao | Pesagem_Entrada | Capturado |
| Pesagem_Entrada | evento_captura | Doca *(ou Aguardando_Aprovacao)* | Nenhum |
| Doca | evento_liberacao_doca | Pesagem_Saida | Aguardando |
| Pesagem_Saida | evento_balanca_saida | Pesagem_Saida | Estabilizando |
| Pesagem_Saida | evento_estabilizacao | Pesagem_Saida | Capturado |
| Pesagem_Saida | evento_captura | Pesagem_Saida *(ou Aguardando_Aprovacao_Saida)* | Nenhum |
| Pesagem_Saida | ev_saida_finalizada | Finalizado | Nenhum |
| Aguardando_Aprovacao | evento_aprovacao_manual | Doca | Nenhum |
| Aguardando_Aprovacao | evento_rejeicao_manual | Erro | Nenhum |
| Aguardando_Aprovacao_Saida | evento_aprovacao_manual | Finalizado | Nenhum |
| Aguardando_Aprovacao_Saida | evento_rejeicao_manual | Erro | Nenhum |

> *Transições condicionais são decididas pela Máquina de Turing antes de aplicar o destino da matriz.*

### Maquinas de Turing

O sistema utiliza duas MTs para validação de peso, executadas como uma etapa anterior à aplicação da transição do DFA:

#### MT1 — ValidadorPesoTuring (Validacao de Formato e Tara)

```
M = (Q, Σ, Γ, δ, q0, q_aceito, q_rejeitado)

Q = { q_inicio, q_zero_inicial, q_inteiro, q_decimal, q_aceito, q_rejeitado }
Σ = { '0'-'9', '.' }
Γ = Σ ∪ { '#' }   ('#' = símbolo branco / fim de fita)
```

Lê a representação em string do peso caractere a caractere e rejeita se:
- Peso nulo ou negativo (guarda de entrada, antes da MT)
- Zero líder detectado (ex: `"015000"`)
- Símbolo inválido na fita

Se o formato for aceito, compara o valor ao peso de tara esperado (configurável). Divergência desvia o DFA para `Aguardando_Aprovacao`.

**Tabela de transicao da MT1 (δ):**

| Estado | Simbolo lido | Proximo estado |
|---|---|---|
| q_inicio | `0` | q_zero_inicial |
| q_inicio | `1`–`9` | q_inteiro |
| q_inicio | qualquer outro | q_rejeitado |
| q_zero_inicial | `#` | q_aceito |
| q_zero_inicial | `.` | q_decimal |
| q_zero_inicial | `0`–`9` | q_rejeitado (zero lider) |
| q_inteiro | `0`–`9` | q_inteiro |
| q_inteiro | `.` | q_decimal |
| q_inteiro | `#` | q_aceito |
| q_decimal | `0`–`9` | q_decimal |
| q_decimal | `#` | q_aceito |
| qualquer | outro | q_rejeitado |

#### MT2 — ValidadorSomaPesosTuring (Validacao da Soma)

Executa na captura da pesagem de saída. Soma `PesoEntrada + PesoSaida` e verifica se o total atinge o limite mínimo (50.000 kg). Caso contrário, desvia para `Aguardando_Aprovacao_Saida`.

### Stack Tecnologico

- **Backend:** ASP.NET Core 10.0 (Minimal API)
- **Frontend:** HTML5, JavaScript (Vanilla), CSS externo (`styles.css`)
- **Linguagem:** C# 13
- **Comunicacao:** REST API com CORS habilitado
- **Ciclo de Vida do Servico:** Singleton (estado persistente entre requisicoes)

---

## Diagrama de Classes

![Diagrama de Classes](docs/diagrams/class-diagram.png)

O sistema e organizado em quatro camadas:

- **Domain Models:** Enums e records que definem o vocabulario do autômato (estados, eventos, sub-estados e tipos de comunicacao).
- **Application Services:** `MotorLogistico` encapsula a maquina de estados, a matriz de transicao e as duas camadas de validacao. `ValidadorPesoTuring` e `ValidadorSomaPesosTuring` implementam as Maquinas de Turing.
- **API Layer:** `Program` expoe tres endpoints REST e injeta o `MotorLogistico` como Singleton.
- **Frontend:** Consome a API via HTTP e atualiza a interface em tempo real, incluindo o painel de aprovacao manual.

---

## Diagrama de Casos de Uso

![Diagrama de Casos de Uso](docs/diagrams/usecase-diagram.png)

### Atores

| Ator | Descricao |
|---|---|
| Operador (Interface Web) | Usuario humano que interage com o simulador via navegador |
| Sensor RFID | Dispositivo externo que dispara o evento de entrada na portaria |
| Balanca de Entrada | Sensor de peso que controla a pesagem no recebimento |
| Balanca de Saida | Sensor de peso que controla a pesagem na expedicao |

### Casos de Uso

| Identificador | Nome | Descricao |
|---|---|---|
| UC01 | Consultar Estado Atual | Recupera o estado e sub-estado correntes do autômato |
| UC02 | Resetar Simulacao | Retorna o sistema ao estado inicial (Portaria) |
| UC03 | Registrar Entrada na Portaria | Processa leitura de RFID e transiciona para pesagem de entrada |
| UC04 | Processar Pesagem de Entrada | Controla o ciclo de estabilizacao e captura do peso na entrada com validacao MT |
| UC05 | Liberar Carga para Doca | Confirma armazenamento na doca e prepara pesagem de saida |
| UC06 | Processar Pesagem de Saida | Controla o ciclo de estabilizacao, captura e validacao de soma via MT |
| UC07 | Finalizar Processo de Carga | Registra conclusao do fluxo logistico completo |
| UC08 | Validar Transicao de Estado | Verifica se o evento e valido para o estado atual na matriz delta |
| UC09 | Validar Sequencia de Sub-Estado | Verifica a ordem correta dos sub-estados durante pesagem |
| UC10 | Registrar Log de Evento | Persiste historico de eventos com timestamp na interface |
| UC11 | Aprovar Peso Manualmente | Operador aprova divergencia de peso e libera fluxo normal |
| UC12 | Rejeitar Peso Manualmente | Operador rejeita divergencia de peso e encerra com Erro |

---

## Estrutura do Projeto

```
AUTOMATOYMS/
├── TrabalhoAutomatos.sln
├── YmsAutomato/
│   ├── Models.cs              # Enums, records e tipos de dominio
│   ├── MotorLogistico.cs      # Implementacao do DFA (motor principal)
│   ├── ValidadorPeso.cs       # Maquinas de Turing (MT1 e MT2)
│   ├── Program.cs             # Configuracao ASP.NET Core e endpoints REST
│   ├── appsettings.json
│   ├── Properties/
│   │   └── launchSettings.json
│   └── wwwroot/
│       ├── index.html         # Interface de simulacao
│       ├── script.js          # Logica de comunicacao com a API
│       └── styles.css         # Estilos da interface
└── docs/
    └── diagrams/
        ├── class-diagram.png
        └── usecase-diagram.png
```

---

## API REST

### `POST /processar-evento`

Processa um evento e aplica a transicao de estado correspondente. Para `evento_captura`, o campo `peso` e obrigatorio.

**Corpo da requisicao:**

```json
{ "evento": 3, "peso": 15000 }
```

| Valor | Evento |
|---|---|
| 0 | evento_rfid_portaria |
| 1 | evento_balanca_entrada |
| 2 | evento_estabilizacao |
| 3 | evento_captura |
| 4 | evento_liberacao_doca |
| 5 | evento_balanca_saida |
| 6 | evento_aprovacao_manual |
| 7 | evento_rejeicao_manual |
| 8 | ev_saida_finalizada |

**Resposta (200 OK):**

```json
{
  "estado": 1,
  "subEstado": 1,
  "mensagem": "Transição realizada com sucesso.",
  "pesoEntrada": 15000,
  "pesoSaida": null
}
```

**Resposta (400 Bad Request):**

```json
{
  "estado": 7,
  "subEstado": 0,
  "mensagem": "Peso inválido (MT Rejeitou): Formato inválido: zeros extras ou símbolo inesperado.",
  "pesoEntrada": 0,
  "pesoSaida": null
}
```

---

### `GET /estado`

Retorna o estado atual sem alterar o autômato.

**Resposta (200 OK):**

```json
{
  "estado": 0,
  "subEstado": 0,
  "mensagem": "Estado atual consultado.",
  "pesoEntrada": 0,
  "pesoSaida": 0
}
```

---

### `POST /reset`

Reinicializa o autômato para o estado inicial e zera os pesos capturados.

**Resposta (200 OK):**

```json
{
  "estado": 0,
  "subEstado": 0,
  "mensagem": "Estado atual consultado.",
  "pesoEntrada": 0,
  "pesoSaida": 0
}
```

---

## Como Executar

### Pre-requisitos

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)

### Execucao

```bash
cd YmsAutomato
dotnet run
```

O servidor inicia em `http://localhost:5034`. Acesse a interface de simulacao abrindo esse endereco em um navegador.

---

## Caracteristicas Principais

- **Determinismo:** Mesma entrada em mesmo estado produz sempre o mesmo resultado.
- **Estado persistente:** O `MotorLogistico` e registrado como Singleton no contêiner de injecao de dependencia, mantendo estado entre requisicoes HTTP.
- **Hierarquia de estados:** Sub-estados modelam o ciclo de estabilizacao das balancas sem introduzir estados paralelos no autômato principal.
- **Validacao em duas camadas (DFA):** A funcao `ProcessarEvento` consulta a matriz delta (primeira camada) e delega a `ValidarSubTransicao` para verificar a sequencia de sub-estados (segunda camada).
- **Validacao por Maquinas de Turing:** Antes de cada transicao de captura de peso, uma MT lê a fita (string do valor) e rejeita formatos invalidos (zero lider, nulo, negativo). Uma segunda MT valida a soma dos pesos de entrada e saida.
- **Fluxo de aprovacao manual:** Pesos divergentes nao encerram o processo com erro — transitam para `Aguardando_Aprovacao` ou `Aguardando_Aprovacao_Saida`, onde o operador decide via UI.
- **Interface reativa:** O frontend atualiza os cartoes de estado, exibe o painel de aprovacao automaticamente e registra log de eventos em tempo real.

---

## Licenca

Projeto academico. Consulte o repositorio original para informacoes de licenciamento.
