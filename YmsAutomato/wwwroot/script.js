const API_URL = '';

// Índices devem refletir exatamente a ordem dos enums em Models.cs
// EstadoLogistico: Portaria=0, Pesagem_Entrada=1, Doca=2, Pesagem_Saida=3, Aguardando_Aprovacao=4, Finalizado=5, Erro=6
// EventoLogistico: evento_rfid_portaria=0, ..., evento_aprovacao_manual=6, evento_rejeicao_manual=7, ev_saida_finalizada=8

async function enviarEvento(eventoId, peso = null) {
    try {
        const body = { evento: eventoId };
        if (peso !== null) body.peso = peso;

        const response = await fetch(`${API_URL}/processar-evento`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        const data = await response.json();

        if (response.ok) {
            atualizarUI(data);
            const subNome = data.subEstado !== 0 ? ` (${obterSubEstadoNome(data.subEstado)})` : '';
            adicionarLog(`Evento ${eventoId} processado → ${obterEstadoNome(data.estado)}${subNome}`, 'normal');
            mostrarStatus(data.mensagem, 'success');
        } else {
            adicionarLog(`ERRO: ${data.mensagem}`, 'error');
            mostrarStatus(data.mensagem, 'error');
            // Atualiza o card mesmo em erro (para refletir estado Erro)
            atualizarUI(data);
        }
    } catch (error) {
        console.error('Erro ao enviar evento:', error);
        adicionarLog('Erro de conexão com o servidor.', 'error');
    }
}

// Captura o peso do input e envia o evento_captura (índice 3)
function capturarPeso() {
    const inputPeso = document.getElementById('input-peso');
    const valor = inputPeso.value.trim();

    if (valor === '') {
        mostrarStatus('Informe o peso antes de capturar.', 'error');
        return;
    }

    const peso = parseFloat(valor);
    enviarEvento(3, peso);
}

// Aprovação manual (evento_aprovacao_manual = índice 6)
async function aprovar() {
    adicionarLog('Aprovação manual enviada.', 'warning');
    await enviarEvento(6);
}

// Rejeição manual (evento_rejeicao_manual = índice 7)
async function rejeitar() {
    adicionarLog('Rejeição manual enviada.', 'warning');
    await enviarEvento(7);
}

async function resetar() {
    const response = await fetch(`${API_URL}/reset`, { method: 'POST' });
    const data = await response.json();
    atualizarUI(data);
    document.getElementById('input-peso').value = '';
    document.getElementById('log').innerHTML = '<div class="log-entry">Sistema inicializado. Aguardando eventos...</div>';
}

function atualizarUI(data) {
    // Remove todas as classes de estado dos cards
    document.querySelectorAll('.state-card').forEach(card => {
        card.classList.remove('active', 'finalized', 'approval', 'erro');
    });
    document.querySelectorAll('.sub-state').forEach(div => div.innerText = '');

    const estadoNome = obterEstadoNome(data.estado);
    const card = document.getElementById(`state-${estadoNome}`);

    if (card) {
        card.classList.add('active');
        if (estadoNome === 'Finalizado')            card.classList.add('finalized');
        if (estadoNome === 'Aguardando_Aprovacao')  card.classList.add('approval');
        if (estadoNome === 'Erro')                  card.classList.add('erro');

        if (data.subEstado !== 0) {
            const subDiv = document.getElementById(`sub-${estadoNome}`);
            if (subDiv) subDiv.innerText = `[${obterSubEstadoNome(data.subEstado)}]`;
        }
    }

    // Painel de aprovação: visível somente em Aguardando_Aprovacao
    const painel = document.getElementById('painel-aprovacao');
    if (estadoNome === 'Aguardando_Aprovacao') {
        painel.style.display = 'block';
        const pesoDisplay = document.getElementById('peso-capturado-display');
        if (data.peso != null) {
            pesoDisplay.innerText = `Peso capturado: ${data.peso.toLocaleString('pt-BR')} kg  |  Esperado: 50.000 kg`;
        } else {
            pesoDisplay.innerText = 'Peso diverge do esperado.';
        }
    } else {
        painel.style.display = 'none';
    }
}

function adicionarLog(msg, tipo = 'normal') {
    const log = document.getElementById('log');
    const entry = document.createElement('div');
    entry.className = 'log-entry' + (tipo === 'error' ? ' log-error' : tipo === 'warning' ? ' log-warning' : '');
    entry.innerText = `[${new Date().toLocaleTimeString()}] ${msg}`;
    log.prepend(entry);
}

function mostrarStatus(msg, type) {
    const statusDiv = document.getElementById('status-msg');
    statusDiv.innerText = msg;
    statusDiv.style.display = 'block';
    statusDiv.style.backgroundColor = type === 'success' ? '#d4edda' : '#f8d7da';
    statusDiv.style.color = type === 'success' ? '#155724' : '#721c24';
    setTimeout(() => { statusDiv.style.display = 'none'; }, 3000);
}

function obterEstadoNome(id) {
    const estados = ['Portaria', 'Pesagem_Entrada', 'Doca', 'Pesagem_Saida', 'Aguardando_Aprovacao', 'Finalizado', 'Erro'];
    return estados[id] ?? 'Desconhecido';
}

function obterSubEstadoNome(id) {
    const subs = ['Nenhum', 'Aguardando', 'Estabilizando', 'Capturado'];
    return subs[id] ?? '';
}

// Inicializa estado ao carregar a página
window.onload = async () => {
    const response = await fetch(`${API_URL}/estado`);
    const data = await response.json();
    atualizarUI(data);
};
