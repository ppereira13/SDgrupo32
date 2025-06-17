using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace Servidor
{
    public static class ListExtensions
    {
        public static double Mode(this List<double> list)
        {
            return list.GroupBy(x => x)
                      .OrderByDescending(g => g.Count())
                      .ThenBy(g => g.Key)
                      .First()
                      .Key;
        }
    }

    public class AnaliseService : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly Dictionary<string, List<DadoSensor>> _dadosHistoricos;
        private const string ANALISE_QUEUE = "analise_servidor_queue";

        public AnaliseService()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _dadosHistoricos = new Dictionary<string, List<DadoSensor>>();

            _channel.QueueDeclare(queue: ANALISE_QUEUE,
                                durable: true,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    var request = JsonSerializer.Deserialize<AnaliseRequest>(message);
                    if (request == null)
                    {
                        Console.WriteLine("Erro: AnaliseRequest desserializado como nulo.");
                        return; // Sair se a requisição for nula
                    }
                    var resultado = await RealizarAnalise(request);
                    
                    // Armazenar resultado para consulta posterior
                    ArmazenarResultado(request.WavyId, request.TipoDado, resultado);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar análise: {ex.Message}");
                }
            };

            _channel.BasicConsume(queue: ANALISE_QUEUE,
                                autoAck: true,
                                consumer: consumer);
        }

        private async Task<AnaliseResultado> RealizarAnalise(AnaliseRequest request)
        {
            var chave = $"{request.WavyId}_{request.TipoDado}";
            
            if (!_dadosHistoricos.ContainsKey(chave))
            {
                _dadosHistoricos[chave] = new List<DadoSensor>();
            }

            // Adicionar novo dado ao histórico
            _dadosHistoricos[chave].Add(new DadoSensor
            {
                Valor = request.Valor,
                Timestamp = request.Timestamp
            });

            // Manter apenas os últimos 1000 dados
            if (_dadosHistoricos[chave].Count > 1000)
            {
                _dadosHistoricos[chave].RemoveAt(0);
            }

            var dados = _dadosHistoricos[chave]
                .Select(d => double.Parse(d.Valor))
                .ToList();

            var resultado = new AnaliseResultado
            {
                WavyId = request.WavyId,
                TipoDado = request.TipoDado,
                Timestamp = DateTime.UtcNow,
                AnaliseBasica = await RealizarAnaliseBasica(dados),
                AnaliseAvancada = await RealizarAnaliseAvancada(dados),
                Padroes = await DetectarPadroes(dados),
                Alertas = await GerarAlertas(dados)
            };

            return resultado;
        }

        private async Task<AnaliseBasica> RealizarAnaliseBasica(List<double> dados)
        {
            return await Task.Run(() => new AnaliseBasica
            {
                Media = dados.Average(),
                Mediana = dados.Median(),
                DesvioPadrao = dados.StandardDeviation(),
                Minimo = dados.Min(),
                Maximo = dados.Max(),
                Variancia = dados.Variance(),
                Moda = dados.Mode()
            });
        }

        private async Task<AnaliseAvancada> RealizarAnaliseAvancada(List<double> dados)
        {
            return await Task.Run(() =>
            {
                var janela = Math.Min(dados.Count, 100);
                var dadosRecentes = dados.Skip(dados.Count - janela).ToList();

                return new AnaliseAvancada
                {
                    Correlacao = CalcularCorrelacaoSerial(dadosRecentes),
                    TendenciaLinear = CalcularTendenciaLinear(dadosRecentes),
                    Sazonalidade = DetectarSazonalidade(dadosRecentes),
                    Periodicidade = CalcularPeriodicidade(dadosRecentes),
                    Entropia = CalcularEntropia(dadosRecentes)
                };
            });
        }

        private async Task<List<Padrao>> DetectarPadroes(List<double> dados)
        {
            return await Task.Run(() =>
            {
                var padroes = new List<Padrao>();
                var janela = Math.Min(dados.Count, 100);
                var dadosRecentes = dados.Skip(dados.Count - janela).ToList();

                // Detectar tendências
                var tendencia = CalcularTendenciaLinear(dadosRecentes);
                if (Math.Abs(tendencia) > 0.1)
                {
                    padroes.Add(new Padrao
                    {
                        Tipo = "Tendência",
                        Descricao = tendencia > 0 ? "Crescente" : "Decrescente",
                        Confianca = Math.Abs(tendencia)
                    });
                }

                // Detectar ciclos
                var periodicidade = CalcularPeriodicidade(dadosRecentes);
                if (periodicidade > 0)
                {
                    padroes.Add(new Padrao
                    {
                        Tipo = "Ciclo",
                        Descricao = $"Período de {periodicidade} amostras",
                        Confianca = 0.8
                    });
                }

                // Detectar outliers
                var media = dadosRecentes.Average();
                var desvio = dadosRecentes.StandardDeviation();
                var outliers = dadosRecentes
                    .Where(d => Math.Abs(d - media) > 2 * desvio)
                    .Count();

                if (outliers > 0)
                {
                    padroes.Add(new Padrao
                    {
                        Tipo = "Anomalia",
                        Descricao = $"Detectados {outliers} valores anômalos",
                        Confianca = 0.9
                    });
                }

                return padroes;
            });
        }

        private async Task<List<Alerta>> GerarAlertas(List<double> dados)
        {
            return await Task.Run(() =>
            {
                var alertas = new List<Alerta>();
                var janela = Math.Min(dados.Count, 100);
                var dadosRecentes = dados.Skip(dados.Count - janela).ToList();

                // Alerta de valor extremo
                var media = dadosRecentes.Average();
                var desvio = dadosRecentes.StandardDeviation();
                var ultimoValor = dadosRecentes.Last();

                if (Math.Abs(ultimoValor - media) > 3 * desvio)
                {
                    alertas.Add(new Alerta
                    {
                        Tipo = "Valor Extremo",
                        Mensagem = $"Valor atual ({ultimoValor:F2}) está muito distante da média ({media:F2})",
                        Severidade = "Alta",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Alerta de tendência rápida
                var tendencia = CalcularTendenciaLinear(dadosRecentes);
                if (Math.Abs(tendencia) > 0.3)
                {
                    alertas.Add(new Alerta
                    {
                        Tipo = "Tendência Acentuada",
                        Mensagem = $"Detectada tendência {(tendencia > 0 ? "crescente" : "decrescente")} acentuada",
                        Severidade = "Média",
                        Timestamp = DateTime.UtcNow
                    });
                }

                return alertas;
            });
        }

        private double CalcularCorrelacaoSerial(List<double> dados)
        {
            if (dados.Count < 2) return 0;

            var dados1 = dados.Take(dados.Count - 1).ToList();
            var dados2 = dados.Skip(1).ToList();

            return Correlation.Pearson(dados1, dados2);
        }

        private double CalcularTendenciaLinear(List<double> dados)
        {
            if (dados.Count < 2) return 0;

            var x = Enumerable.Range(0, dados.Count).Select(i => (double)i).ToList();
            return Correlation.Pearson(x, dados);
        }

        private bool DetectarSazonalidade(List<double> dados)
        {
            if (dados.Count < 4) return false;

            // Implementação simplificada - detecta padrões repetitivos
            var correlacoes = new List<double>();
            for (int lag = 1; lag <= dados.Count / 4; lag++)
            {
                var dados1 = dados.Take(dados.Count - lag).ToList();
                var dados2 = dados.Skip(lag).ToList();
                correlacoes.Add(Correlation.Pearson(dados1, dados2));
            }

            return correlacoes.Any(c => c > 0.7);
        }

        private int CalcularPeriodicidade(List<double> dados)
        {
            if (dados.Count < 4) return 0;

            // Implementação simplificada - procura por padrões repetitivos
            for (int periodo = 2; periodo <= dados.Count / 2; periodo++)
            {
                var similar = true;
                for (int i = 0; i < dados.Count - periodo; i++)
                {
                    if (Math.Abs(dados[i] - dados[i + periodo]) > dados.StandardDeviation())
                    {
                        similar = false;
                        break;
                    }
                }
                if (similar) return periodo;
            }

            return 0;
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

        private void ArmazenarResultado(string wavyId, string tipoDado, AnaliseResultado resultado)
        {
            // Aqui você implementaria a lógica para armazenar os resultados
            // Por exemplo, em um banco de dados ou sistema de arquivos
            Console.WriteLine($"Análise concluída para {wavyId} - {tipoDado}");
            Console.WriteLine(JsonSerializer.Serialize(resultado, new JsonSerializerOptions { WriteIndented = true }));
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }

    public class DadoSensor
    {
        public string Valor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class AnaliseRequest
    {
        public string WavyId { get; set; }
        public string TipoDado { get; set; }
        public string Valor { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AnaliseResultado
    {
        public string WavyId { get; set; }
        public string TipoDado { get; set; }
        public DateTime Timestamp { get; set; }
        public AnaliseBasica AnaliseBasica { get; set; }
        public AnaliseAvancada AnaliseAvancada { get; set; }
        public List<Padrao> Padroes { get; set; }
        public List<Alerta> Alertas { get; set; }
    }

    public class AnaliseBasica
    {
        public double Media { get; set; }
        public double Mediana { get; set; }
        public double DesvioPadrao { get; set; }
        public double Minimo { get; set; }
        public double Maximo { get; set; }
        public double Variancia { get; set; }
        public double Moda { get; set; }
    }

    public class AnaliseAvancada
    {
        public double Correlacao { get; set; }
        public double TendenciaLinear { get; set; }
        public bool Sazonalidade { get; set; }
        public int Periodicidade { get; set; }
        public double Entropia { get; set; }
    }

    public class Padrao
    {
        public string Tipo { get; set; }
        public string Descricao { get; set; }
        public double Confianca { get; set; }
    }

    public class Alerta
    {
        public string Tipo { get; set; }
        public string Mensagem { get; set; }
        public string Severidade { get; set; }
        public DateTime Timestamp { get; set; }
    }
} 