using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace PreProcessamentoRPC
{
    public class AnaliseService
    {
        private const string ANALISE_QUEUE = "analise_rpc_queue";
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly Dictionary<string, List<double>> _dadosHistoricos;

        public AnaliseService()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _dadosHistoricos = new Dictionary<string, List<double>>();

            _channel.QueueDeclare(queue: ANALISE_QUEUE,
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            _channel.QueuePurge(ANALISE_QUEUE);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var response = string.Empty;
                var body = ea.Body.ToArray();
                var props = ea.BasicProperties;
                var replyProps = _channel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;

                try
                {
                    var message = Encoding.UTF8.GetString(body);
                    var request = JsonSerializer.Deserialize<AnaliseRequest>(message);
                    response = await ProcessarAnalise(request);
                }
                catch (Exception ex)
                {
                    response = JsonSerializer.Serialize(new { erro = ex.Message });
                }
                finally
                {
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    _channel.BasicPublish(exchange: "",
                                        routingKey: props.ReplyTo,
                                        basicProperties: replyProps,
                                        body: responseBytes);
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
            };

            _channel.BasicConsume(queue: ANALISE_QUEUE,
                                autoAck: false,
                                consumer: consumer);

            Console.WriteLine("Serviço de Análise iniciado. Aguardando requisições...");
        }

        private async Task<string> ProcessarAnalise(AnaliseRequest request)
        {
            var resultado = new AnaliseResultado();
            var chave = $"{request.WavyId}_{request.TipoDado}";

            // Atualizar dados históricos
            if (!_dadosHistoricos.ContainsKey(chave))
            {
                _dadosHistoricos[chave] = new List<double>();
            }

            if (double.TryParse(request.Valor, out double valorNumerico))
            {
                _dadosHistoricos[chave].Add(valorNumerico);
            }

            var dados = _dadosHistoricos[chave];

            // Análise estatística básica
            resultado.Media = dados.Average();
            resultado.Mediana = CalcularMediana(dados);
            resultado.DesvioPadrao = CalcularDesvioPadrao(dados);
            resultado.Tendencia = DetectarTendencia(dados);
            resultado.Anomalias = DetectarAnomalias(dados);

            // Análise avançada assíncrona (simulando processamento HPC)
            if (request.RequerAnaliseAvancada)
            {
                resultado.AnaliseAvancada = await ExecutarAnaliseAvancadaHPC(dados);
            }

            return JsonSerializer.Serialize(resultado);
        }

        private double CalcularMediana(List<double> dados)
        {
            var ordenados = dados.OrderBy(d => d).ToList();
            int meio = ordenados.Count / 2;
            
            if (ordenados.Count % 2 == 0)
                return (ordenados[meio - 1] + ordenados[meio]) / 2;
            
            return ordenados[meio];
        }

        private double CalcularDesvioPadrao(List<double> dados)
        {
            double media = dados.Average();
            double somaDiferencasQuadrado = dados.Sum(d => Math.Pow(d - media, 2));
            return Math.Sqrt(somaDiferencasQuadrado / (dados.Count - 1));
        }

        private string DetectarTendencia(List<double> dados)
        {
            if (dados.Count < 3) return "Dados insuficientes";

            int tendenciaPositiva = 0;
            int tendenciaNegativa = 0;

            for (int i = 1; i < dados.Count; i++)
            {
                if (dados[i] > dados[i - 1]) tendenciaPositiva++;
                else if (dados[i] < dados[i - 1]) tendenciaNegativa++;
            }

            if (tendenciaPositiva > dados.Count * 0.6) return "Crescente";
            if (tendenciaNegativa > dados.Count * 0.6) return "Decrescente";
            return "Estável";
        }

        private List<double> DetectarAnomalias(List<double> dados)
        {
            if (dados.Count < 4) return new List<double>();

            var media = dados.Average();
            var desvioPadrao = CalcularDesvioPadrao(dados);
            var limite = desvioPadrao * 2; // 2 desvios padrão

            return dados.Where(d => Math.Abs(d - media) > limite).ToList();
        }

        private async Task<Dictionary<string, object>> ExecutarAnaliseAvancadaHPC(List<double> dados)
        {
            // Simulação de processamento HPC
            await Task.Delay(100); // Simula processamento distribuído

            return new Dictionary<string, object>
            {
                { "correlacaoDimensional", CalcularCorrelacaoDimensional(dados) },
                { "entropia", CalcularEntropia(dados) },
                { "componentesPrincipais", AnalisarComponentesPrincipais(dados) }
            };
        }

        private double CalcularCorrelacaoDimensional(List<double> dados)
        {
            // Implementação simplificada do algoritmo de correlação dimensional
            return dados.Count > 0 ? dados.Average() / dados.Max() : 0;
        }

        private double CalcularEntropia(List<double> dados)
        {
            if (!dados.Any()) return 0;

            var min = dados.Min();
            var max = dados.Max();
            var range = max - min;
            var bins = 10;
            var histogram = new int[bins];
            
            foreach (var valor in dados)
            {
                var bin = (int)((valor - min) / range * (bins - 1));
                histogram[bin]++;
            }

            double entropia = 0;
            var total = dados.Count;
            
            for (int i = 0; i < bins; i++)
            {
                if (histogram[i] > 0)
                {
                    var p = (double)histogram[i] / total;
                    entropia -= p * Math.Log(p);
                }
            }

            return entropia;
        }

        private List<double> AnalisarComponentesPrincipais(List<double> dados)
        {
            // Implementação simplificada de PCA
            var normalizedData = dados.Select(d => (d - dados.Average()) / CalcularDesvioPadrao(dados)).ToList();
            return normalizedData.Take(3).ToList(); // Retorna os 3 primeiros componentes
        }
    }
} 