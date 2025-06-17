// Configurações e variáveis globais
let charts = {
    realtime: null,
    status: null,
    trends: null,
    heatmap: null,
    distribution: null,
    boxPlot: null,
    correlation: null,
    anomalies: null
};

let updateInterval;
let lastFilteredData = null; // Armazena os últimos dados filtrados
const UPDATE_FREQUENCY = 1000; // 1 segundo

// Configurações padrão para os gráficos
const defaultChartOptions = {
    responsive: true,
    animation: {
        duration: 0 // desabilita animação para melhor performance em tempo real
    },
    scales: {
        x: {
            display: true,
            title: {
                display: true,
                text: 'Tempo'
            }
        },
        y: {
            display: true,
            title: {
                display: true,
                text: 'Valor'
            }
        }
    }
};

// Cores para status
const statusColors = {
    'Conectada': 'rgb(34, 197, 94)', // Verde
    'Inativa': 'rgb(239, 68, 68)',   // Vermelho
    'Erro': 'rgb(249, 115, 22)',     // Laranja
    'Manutenção': 'rgb(59, 130, 246)', // Azul
    'default': 'rgb(156, 163, 175)'   // Cinza para status desconhecidos
};

// Inicialização do dashboard
document.addEventListener('DOMContentLoaded', async function() {
    initializeCharts();
    await loadFilterOptions(); // Carregar opções dos filtros primeiro
    setupEventListeners();
    initializeRealTimeUpdates();
    loadStatusDistribution(); // Carrega distribuição inicial
});

// Inicialização dos gráficos
function initializeCharts() {
    // Gráfico em tempo real
    const realtimeCtx = document.getElementById('realtimeChart')?.getContext('2d');
    if (realtimeCtx) {
    charts.realtime = new Chart(realtimeCtx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [{
                label: 'Dados em Tempo Real',
                data: [],
                borderColor: 'rgb(75, 192, 192)',
                tension: 0.1
            }]
        },
            options: {
                ...defaultChartOptions,
                maintainAspectRatio: true,
                aspectRatio: 2
            }
        });
    }

    // Gráfico de status
    const statusCtx = document.getElementById('statusChart')?.getContext('2d');
    if (statusCtx) {
        charts.status = new Chart(statusCtx, {
            type: 'doughnut',
            data: {
                labels: [],
                datasets: [{
                    data: [],
                    backgroundColor: [],
                    borderWidth: 1,
                    borderColor: '#ffffff'
            }]
        },
        options: {
            responsive: true,
                maintainAspectRatio: true,
                aspectRatio: 1.5,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 20,
                            usePointStyle: true,
                            pointStyle: 'circle'
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                const label = context.label || '';
                                const value = context.raw || 0;
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = ((value / total) * 100).toFixed(1);
                                return `${label}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                }
            }
        });
    }

    // Gráfico de tendências
    const trendsCtx = document.getElementById('trendsChart')?.getContext('2d');
    if (trendsCtx) {
        charts.trends = new Chart(trendsCtx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [{
                    label: 'Tendência',
                data: [],
                borderColor: 'rgb(153, 102, 255)',
                    backgroundColor: 'rgba(153, 102, 255, 0.1)',
                    fill: true,
                tension: 0.1
            }]
        },
        options: {
                ...defaultChartOptions,
                maintainAspectRatio: false
            }
        });
    }
}

// Configuração dos event listeners
function setupEventListeners() {
    document.getElementById('wavySelect')?.addEventListener('change', updateDashboard);
    document.getElementById('sensorSelect')?.addEventListener('change', updateDashboard);
    document.getElementById('startDate')?.addEventListener('change', updateDashboard);
    document.getElementById('endDate')?.addEventListener('change', updateDashboard);
}

// Inicialização das atualizações em tempo real
function initializeRealTimeUpdates() {
    updateDashboard();
    updateInterval = setInterval(updateDashboard, UPDATE_FREQUENCY);
}

// Função principal de atualização do dashboard
async function updateDashboard() {
    try {
        const sensorType = document.getElementById('sensorSelect')?.value || '';
        const response = await fetch(`/Dashboard/GetRealTimeStats?sensorType=${encodeURIComponent(sensorType)}`);
        const stats = await response.json();

        if (stats.error) {
            console.error('Erro retornado pelo servidor:', stats.error);
            return;
        }

        // Se não houver dados filtrados, atualiza com dados em tempo real
        if (!lastFilteredData) {
            updateHeaderStats(stats);
            await updateCharts();
        }
    } catch (error) {
        console.error('Erro ao atualizar dashboard:', error);
    }
}

// Atualização das estatísticas do cabeçalho
function updateHeaderStats(stats) {
    document.getElementById('headerTotalDevices').textContent = stats.connectedDevices;
    
    document.getElementById('headerAvgValue').textContent = 
        stats.averageSensor !== null ? stats.averageSensor.toFixed(2) : '--';
    
    document.getElementById('headerMinMax').textContent = 
        (stats.minValue !== null && stats.maxValue !== null) 
            ? `${stats.minValue.toFixed(2)} / ${stats.maxValue.toFixed(2)}` 
            : '-- / --';
    
    document.getElementById('headerStdDev').textContent = 
        stats.stdDev !== null ? stats.stdDev.toFixed(2) : '--';
}

// Atualização dos gráficos
async function updateCharts() {
    try {
        const sensorType = document.getElementById('sensorSelect')?.value || '';
        
        // Atualiza distribuição de status
        await loadStatusDistribution();

        // Atualiza gráfico de status
        const statusResponse = await fetch('/Dashboard/GetStatus');
        const statusData = await statusResponse.json();
        
        const connected = statusData.filter(s => s.status === "Conectada").length;
        const disconnected = statusData.length - connected;
        
        if (charts.status) {
            charts.status.data.datasets[0].data = [connected, disconnected];
            charts.status.update();
        }

        // Atualiza dados em tempo real
        const now = new Date();
        const oneMinuteAgo = new Date(now.getTime() - 60000);
        
        const realtimeResponse = await fetch(`/Dashboard/GetData?startTime=${oneMinuteAgo.toISOString()}&endTime=${now.toISOString()}&sensorType=${encodeURIComponent(sensorType)}`);
        const realtimeData = await realtimeResponse.json();

        if (charts.realtime && realtimeData && realtimeData.length > 0) {
            // Pega o valor mais recente
            const latestValue = parseFloat(realtimeData[realtimeData.length - 1].value);
            
            if (!isNaN(latestValue)) {
                charts.realtime.data.labels.push(now.toLocaleTimeString());
                charts.realtime.data.datasets[0].data.push(latestValue);

                // Mantém apenas os últimos 20 pontos
                if (charts.realtime.data.labels.length > 20) {
                    charts.realtime.data.labels.shift();
                    charts.realtime.data.datasets[0].data.shift();
                }

                charts.realtime.update();
            }
        }

        // Atualiza tendências
        if (charts.trends) {
            const end = new Date();
            const start = new Date(end - 3600000); // última hora
            const trendsResponse = await fetch(`/Dashboard/GetData?startTime=${start.toISOString()}&endTime=${end.toISOString()}&sensorType=${encodeURIComponent(sensorType)}`);
            const trendsData = await trendsResponse.json();

            if (trendsData && trendsData.length > 0) {
                const validData = trendsData
                    .filter(d => !isNaN(parseFloat(d.value)))
                    .map(d => ({
                        time: new Date(d.timestamp).toLocaleTimeString(),
                        value: parseFloat(d.value)
                    }));

                if (validData.length > 0) {
                    charts.trends.data.labels = validData.map(d => d.time);
                    charts.trends.data.datasets[0].data = validData.map(d => d.value);
                    charts.trends.update();
                }
            }
        }
    } catch (error) {
        console.error('Erro ao atualizar gráficos:', error);
    }
}

// Função para parar as atualizações em tempo real
function stopRealTimeUpdates() {
    if (updateInterval) {
        clearInterval(updateInterval);
    }
}

// Limpar dados quando a página for fechada
window.addEventListener('beforeunload', stopRealTimeUpdates);

async function loadStatusSummary() {
    const res = await fetch('/Dashboard/GetStatusSummary');
    const data = await res.json();
    const labels = data.map(x => x.status);
    const counts = data.map(x => x.count);
    if (!statusChart) {
        const ctx = document.getElementById('statusChart').getContext('2d');
        statusChart = new Chart(ctx, {
            type: 'pie',
            data: { labels, datasets: [{ data: counts, backgroundColor: ['#10b981', '#ef4444', '#f59e42', '#6366f1'] }] },
            options: { responsive: true, plugins: { legend: { position: 'bottom' } } }
        });
    } else {
        statusChart.data.labels = labels;
        statusChart.data.datasets[0].data = counts;
        statusChart.update();
    }
    // Atualizar estatísticas
    document.getElementById('statusMostCommon').textContent = labels[counts.indexOf(Math.max(...counts))] || '--';
}

async function loadConfigSummary() {
    const res = await fetch('/Dashboard/GetConfigSummary');
    const data = await res.json();
    const labels = data.map(x => x.config);
    const counts = data.map(x => x.count);
    if (!configChart) {
        const ctx = document.getElementById('configChart').getContext('2d');
        configChart = new Chart(ctx, {
            type: 'bar',
            data: { labels, datasets: [{ data: counts, backgroundColor: '#6366f1' }] },
            options: { responsive: true, plugins: { legend: { display: false } } }
        });
    } else {
        configChart.data.labels = labels;
        configChart.data.datasets[0].data = counts;
        configChart.update();
    }
    document.getElementById('configMostCommon').textContent = labels[counts.indexOf(Math.max(...counts))] || '--';
}

async function loadStatusHistory(wavyId) {
    const res = await fetch(`/Dashboard/GetStatusHistory?wavyId=${wavyId}`);
    const data = await res.json();
    const table = document.getElementById('statusHistoryTable');
    table.innerHTML = data.map(x => `<tr><td>${new Date(x.timestamp).toLocaleString()}</td><td>${x.value}</td></tr>`).join('');
}

async function loadConfigHistory(wavyId) {
    const res = await fetch(`/Dashboard/GetConfigHistory?wavyId=${wavyId}`);
    const data = await res.json();
    const table = document.getElementById('configHistoryTable');
    table.innerHTML = data.map(x => `<tr><td>${new Date(x.timestamp).toLocaleString()}</td><td>${x.value}</td></tr>`).join('');
}

// Função para carregar e atualizar a distribuição de status
async function loadStatusDistribution() {
    try {
        const response = await fetch('/Dashboard/GetStatus');
        const statusData = await response.json();
        
        // Agrupa os status e conta as ocorrências
        const statusCounts = statusData.reduce((acc, curr) => {
            acc[curr.status] = (acc[curr.status] || 0) + 1;
            return acc;
        }, {});

        // Prepara os dados para o gráfico
        const labels = Object.keys(statusCounts);
        const data = Object.values(statusCounts);
        const backgroundColor = labels.map(status => statusColors[status] || statusColors.default);

        // Atualiza o gráfico
        if (charts.status) {
            charts.status.data.labels = labels;
            charts.status.data.datasets[0].data = data;
            charts.status.data.datasets[0].backgroundColor = backgroundColor;
            charts.status.update();

            // Atualiza o status mais comum
            const mostCommonStatus = labels.reduce((a, b) => 
                statusCounts[a] > statusCounts[b] ? a : b
            );
            const statusElement = document.getElementById('statusMostCommon');
            if (statusElement) {
                statusElement.textContent = `${mostCommonStatus} (${statusCounts[mostCommonStatus]} dispositivos)`;
                statusElement.className = `badge bg-${getStatusClass(mostCommonStatus)}`;
            }

            // Atualiza o timestamp da última atualização
            const lastUpdateElement = document.getElementById('statusLastUpdate');
            if (lastUpdateElement) {
                const now = new Date();
                lastUpdateElement.textContent = now.toLocaleString();
                
                // Adiciona efeito de atualização
                lastUpdateElement.classList.add('updating');
                setTimeout(() => {
                    lastUpdateElement.classList.remove('updating');
                }, 1000);
            }
        }
    } catch (error) {
        console.error('Erro ao carregar distribuição de status:', error);
    }
}

// Função auxiliar para determinar a classe do badge baseado no status
function getStatusClass(status) {
    switch (status.toLowerCase()) {
        case 'conectada':
            return 'success';
        case 'inativa':
            return 'danger';
        case 'erro':
            return 'warning';
        case 'manutenção':
            return 'info';
        default:
            return 'secondary';
    }
}

// Função para carregar dados filtrados
async function loadData() {
    try {
        const wavyId = document.getElementById('wavySelect')?.value || '';
        const sensorType = document.getElementById('sensorSelect')?.value || '';
        const startDate = document.getElementById('startDate')?.value || '';
        const endDate = document.getElementById('endDate')?.value || '';

        // Validar datas
        if (startDate && endDate && new Date(startDate) > new Date(endDate)) {
            alert('A data inicial não pode ser maior que a data final.');
            return;
        }

        const response = await fetch(`/Dashboard/GetData?wavyId=${encodeURIComponent(wavyId)}&dataType=${encodeURIComponent(sensorType)}&startTime=${encodeURIComponent(startDate)}&endTime=${encodeURIComponent(endDate)}`);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const data = await response.json();
        lastFilteredData = data; // Armazena os dados filtrados
        
        // Atualizar estatísticas
        updateFilteredStats(data);
        
        // Atualizar gráficos
        updateChartsWithFilteredData(data);
        
    } catch (error) {
        console.error('Erro ao carregar dados:', error);
        alert('Erro ao carregar dados. Por favor, tente novamente.');
    }
}

// Nova função para atualizar estatísticas com dados filtrados
function updateFilteredStats(data) {
    if (!data || data.length === 0) {
        document.getElementById('totalDevices').textContent = '0';
        document.getElementById('totalReadings').textContent = '0';
        document.getElementById('avgValue').textContent = '0';
        return;
    }

    const uniqueDevices = [...new Set(data.map(x => x.wavyId))];
    const validValues = data.filter(x => !isNaN(parseFloat(x.value))).map(x => parseFloat(x.value));
    const average = validValues.length > 0 ? validValues.reduce((a, b) => a + b, 0) / validValues.length : 0;

    document.getElementById('totalDevices').textContent = uniqueDevices.length;
    document.getElementById('totalReadings').textContent = data.length;
    document.getElementById('avgValue').textContent = average.toFixed(2);
}

// Função para carregar opções dos filtros
async function loadFilterOptions() {
    try {
        // Carregar WAVYs disponíveis
        const wavyResponse = await fetch('/Dashboard/GetWavyIds');
        if (!wavyResponse.ok) throw new Error('Erro ao carregar WAVYs');
        const wavyIds = await wavyResponse.json();
        
        const wavySelect = document.getElementById('wavySelect');
        if (wavySelect) {
            wavySelect.innerHTML = '<option value="">Todos os dispositivos</option>' +
                wavyIds.map(id => `<option value="${id}">${id}</option>`).join('');
        }

        // Carregar tipos de sensores disponíveis
        const sensorResponse = await fetch('/Dashboard/GetAllDataTypes');
        if (!sensorResponse.ok) throw new Error('Erro ao carregar tipos de sensores');
        const sensorTypes = await sensorResponse.json();
        
        const sensorSelect = document.getElementById('sensorSelect');
        if (sensorSelect) {
            sensorSelect.innerHTML = '<option value="">Todos os sensores</option>' +
                sensorTypes.map(type => `<option value="${type}">${type}</option>`).join('');
        }

        // Configurar datas padrão
        const now = new Date();
        const oneHourAgo = new Date(now - 3600000);
        
        const startDate = document.getElementById('startDate');
        const endDate = document.getElementById('endDate');
        
        if (startDate) startDate.value = oneHourAgo.toISOString().slice(0, 16);
        if (endDate) endDate.value = now.toISOString().slice(0, 16);
    } catch (error) {
        console.error('Erro ao carregar opções dos filtros:', error);
    }
}

// Função para atualizar gráficos com dados filtrados
function updateChartsWithFilteredData(data) {
    if (!data || data.length === 0) {
        // Limpar gráficos se não houver dados
        if (charts.trends) {
            charts.trends.data.labels = [];
            charts.trends.data.datasets[0].data = [];
            charts.trends.update();
        }
        return;
    }

    // Atualizar gráfico de tendências
    if (charts.trends) {
        const validData = data
            .filter(d => !isNaN(parseFloat(d.value)))
            .map(d => ({
                time: new Date(d.timestamp).toLocaleString(),
                value: parseFloat(d.value)
            }));

        if (validData.length > 0) {
            charts.trends.data.labels = validData.map(d => d.time);
            charts.trends.data.datasets[0].data = validData.map(d => d.value);
            charts.trends.update();
        }
    }

    // Atualizar tabela de dados
    const tbody = document.getElementById('dataTableBody');
    if (tbody) {
        if (!data || data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center">Nenhum registro encontrado</td></tr>';
        } else {
            tbody.innerHTML = data.map(item => `
                <tr>
                    <td>${new Date(item.timestamp).toLocaleString()}</td>
                    <td>${item.wavyId}</td>
                    <td>${item.dataType}</td>
                    <td>${item.value}</td>
                    <td>${item.status || 'N/A'}</td>
                    <td>${item.isAnomaly ? 'Sim' : 'Não'}</td>
                </tr>
            `).join('');
        }
    }

    // Atualizar gráfico de status se houver dados de status
    const statusData = data.filter(d => d.status);
    if (statusData.length > 0 && charts.status) {
        const statusCounts = statusData.reduce((acc, curr) => {
            acc[curr.status] = (acc[curr.status] || 0) + 1;
            return acc;
        }, {});

        charts.status.data.labels = Object.keys(statusCounts);
        charts.status.data.datasets[0].data = Object.values(statusCounts);
        charts.status.data.datasets[0].backgroundColor = charts.status.data.labels.map(
            status => statusColors[status] || statusColors.default
        );
        charts.status.update();
    }
} 