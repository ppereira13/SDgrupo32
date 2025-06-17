using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace Servidor
{
    public class HPCService
    {
        private readonly HttpClient _httpClient;
        private readonly string _hpcEndpoint;
        private readonly Dictionary<string, string> _jobStatus;

        public HPCService(string hpcEndpoint = "http://localhost:8080")
        {
            _httpClient = new HttpClient();
            _hpcEndpoint = hpcEndpoint;
            _jobStatus = new Dictionary<string, string>();
        }

        public async Task<string> SubmeterAnaliseHPC(AnaliseHPCRequest request)
        {
            try
            {
                // Preparar dados para envio ao cluster HPC
                var jobRequest = new
                {
                    request.WavyId,
                    request.TipoDado,
                    request.Dados,
                    request.TipoAnalise,
                    request.Parametros,
                    Timestamp = DateTime.UtcNow
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(jobRequest),
                    Encoding.UTF8,
                    "application/json");

                // Enviar job para o cluster HPC
                var response = await _httpClient.PostAsync($"{_hpcEndpoint}/submit", content);
                var jobId = await response.Content.ReadAsStringAsync();

                // Registrar status do job
                _jobStatus[jobId] = "Submetido";

                return jobId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao submeter análise HPC: {ex.Message}");
            }
        }

        public async Task<AnaliseHPCResultado> ObterResultadoAnalise(string jobId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_hpcEndpoint}/result/{jobId}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<AnaliseHPCResultado>(content);
                }
                else
                {
                    throw new Exception($"Erro ao obter resultado: {content}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao obter resultado da análise HPC: {ex.Message}");
            }
        }

        public async Task<string> ObterStatusJob(string jobId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_hpcEndpoint}/status/{jobId}");
                var status = await response.Content.ReadAsStringAsync();

                _jobStatus[jobId] = status;
                return status;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao obter status do job: {ex.Message}");
            }
        }

        public async Task CancelarJob(string jobId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_hpcEndpoint}/cancel/{jobId}");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Falha ao cancelar job");
                }
                _jobStatus[jobId] = "Cancelado";
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao cancelar job: {ex.Message}");
            }
        }

        public async Task<List<RecursoHPC>> ObterRecursosDisponiveis()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_hpcEndpoint}/resources");
                var content = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<List<RecursoHPC>>(content);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao obter recursos disponíveis: {ex.Message}");
            }
        }

        public async Task<AnaliseHPCResultado> ExecutarAnaliseCompleta(AnaliseHPCRequest request)
        {
            try
            {
                // 1. Verificar recursos disponíveis
                var recursos = await ObterRecursosDisponiveis();
                if (!recursos.Any(r => r.Disponivel))
                {
                    throw new Exception("Nenhum recurso HPC disponível no momento");
                }

                // 2. Submeter job
                var jobId = await SubmeterAnaliseHPC(request);

                // 3. Monitorar progresso
                string status;
                do
                {
                    await Task.Delay(1000); // Aguardar 1 segundo entre verificações
                    status = await ObterStatusJob(jobId);
                }
                while (status == "Em Execução" || status == "Submetido");

                // 4. Obter resultado
                if (status == "Concluído")
                {
                    return await ObterResultadoAnalise(jobId);
                }
                else
                {
                    throw new Exception($"Análise falhou com status: {status}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro na execução da análise HPC: {ex.Message}");
            }
        }
    }

    public class AnaliseHPCRequest
    {
        public string WavyId { get; set; }
        public string TipoDado { get; set; }
        public List<double> Dados { get; set; }
        public string TipoAnalise { get; set; }
        public Dictionary<string, object> Parametros { get; set; }
    }

    public class AnaliseHPCResultado
    {
        public string JobId { get; set; }
        public string WavyId { get; set; }
        public string TipoDado { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Resultados { get; set; }
        public List<string> Alertas { get; set; }
        public Dictionary<string, object> MetaDados { get; set; }
        public TimeSpan TempoProcessamento { get; set; }
        public string RecursoUtilizado { get; set; }
    }

    public class RecursoHPC
    {
        public string Id { get; set; }
        public string Nome { get; set; }
        public bool Disponivel { get; set; }
        public int NucleosCPU { get; set; }
        public int MemoriaGB { get; set; }
        public List<string> Capacidades { get; set; }
        public Dictionary<string, string> Metricas { get; set; }
    }
} 