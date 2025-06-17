using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using System.Collections.Generic;
using System.ServiceModel;
using Servidor;
using System.Text.Json.Serialization;

namespace PreProcessamentoRPC
{
    public class PreProcessamentoService : IPreProcessamentoService, IDisposable
    {
        private bool _disposed = false;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly FormatConverter _formatConverter;
        private readonly RateUniformizer _rateUniformizer;
        private readonly AnaliseService _analiseService;
        private readonly HPCService _hpcService;
        private const string QUEUE_NAME = "preprocessamento_rpc_queue";

        public PreProcessamentoService()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _formatConverter = new FormatConverter();
            _rateUniformizer = new RateUniformizer();
            _analiseService = new AnaliseService();
            _hpcService = new HPCService();

            _channel.QueueDeclare(queue: QUEUE_NAME,
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            _channel.QueuePurge(QUEUE_NAME);

            _channel.BasicQos(0, 1, false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                string response = string.Empty;
                var body = ea.Body.ToArray();
                var props = ea.BasicProperties;
                var replyProps = _channel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;

                try
                {
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine($"Recebida requisição: {message}");
                    
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    var request = JsonSerializer.Deserialize<PreProcessamentoRequest>(message, options);
                    response = await ProcessarDados(request);
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

            _channel.BasicConsume(queue: QUEUE_NAME,
                                autoAck: false,
                                consumer: consumer);

            Console.WriteLine("Serviço de Pré-processamento iniciado. Aguardando requisições...");

            // Configurar taxas de amostragem padrão para diferentes tipos de sensores
            ConfigurarTaxasAmostragem();
        }

        private void ConfigurarTaxasAmostragem()
        {
            // Configurar taxas de amostragem padrão para diferentes tipos de sensores
            _rateUniformizer.ConfigureSensorRate("acel", TimeSpan.FromMilliseconds(100));  // 10Hz
            _rateUniformizer.ConfigureSensorRate("gyro", TimeSpan.FromMilliseconds(100));  // 10Hz
            _rateUniformizer.ConfigureSensorRate("status", TimeSpan.FromSeconds(1));       // 1Hz
            _rateUniformizer.ConfigureSensorRate("hidrofone", TimeSpan.FromMilliseconds(50)); // 20Hz
            _rateUniformizer.ConfigureSensorRate("transdutor", TimeSpan.FromMilliseconds(200)); // 5Hz
            _rateUniformizer.ConfigureSensorRate("camera", TimeSpan.FromSeconds(1));       // 1Hz
        }

        private async Task<string> ProcessarDados(PreProcessamentoRequest request)
        {
            try
            {
                // 1. Converter formato dos dados se necessário
                string dadosConvertidos = request.Valor;
                if (request.FormatoOrigem != FormatConverter.DataFormat.JSON)
                {
                    dadosConvertidos = _formatConverter.ConvertData(
                        request.Valor,
                        request.FormatoOrigem,
                        FormatConverter.DataFormat.JSON
                    );
                }

                // 2. Extrair valor numérico do JSON
                var dadosJson = JsonSerializer.Deserialize<DadosSensor>(dadosConvertidos);
                
                // 3. Uniformizar taxa de amostragem
                _rateUniformizer.AddData(
                    $"{request.WavyId}_{request.TipoDado}",
                    dadosJson.Valor,
                    dadosJson.Timestamp
                );

                var dadosUniformizados = _rateUniformizer.UniformizeSensorData(TimeSpan.FromSeconds(1));

                // 4. Preparar resposta
                var taxas = _rateUniformizer.ObterConfiguracoes();
                var resposta = new PreProcessamentoResposta
                {
                    WavyId = request.WavyId,
                    TipoDado = request.TipoDado,
                    DadosProcessados = dadosUniformizados,
                    Timestamp = DateTime.UtcNow,
                    MetaDados = new MetaDados
                    {
                        FormatoOriginal = request.FormatoOrigem.ToString(),
                        TaxaAmostragermOriginal = taxas[request.TipoDado],
                        TaxaAmostragermUniforme = 1000, // 1 segundo
                        ProcessamentoTimestamp = DateTime.UtcNow
                    }
                };

                // 5. Limpar dados antigos periodicamente
                _rateUniformizer.ClearOldData(TimeSpan.FromMinutes(5));

                return JsonSerializer.Serialize(resposta);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro no pré-processamento: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _channel?.Close();
                        _connection?.Close();
                        _channel?.Dispose();
                        _connection?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao fechar conexões: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        ~PreProcessamentoService()
        {
            Dispose(false);
        }

        public async Task<string> ConverterFormato(string dados, string formatoOrigem, string formatoDestino)
        {
            try
            {
                return await Task.Run(() =>
                {
                    // Converter para o formato padronizado
                    var dadoPadronizadoJson = _formatConverter.ConverterParaPadrao(dados, formatoOrigem);
                    var dadoPadronizado = JsonSerializer.Deserialize<FormatConverter.DadoPadronizado>(dadoPadronizadoJson);

                    // Validar o dado
                    _formatConverter.ValidarDado(dadoPadronizadoJson);

                    // Converter para o formato de destino
                    return _formatConverter.ConverterParaFormato(dadoPadronizado, formatoDestino);
                });
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro na conversão de formato: {ex.Message}");
            }
        }

        public async Task<string> UniformizarDados(string dados, string formatoOrigem)
        {
            try
            {
                return await Task.Run(async () =>
                {
                    // Converter para o formato padronizado
                    var dadoPadronizadoJson = _formatConverter.ConverterParaPadrao(dados, formatoOrigem);
                    var dadoPadronizado = JsonSerializer.Deserialize<FormatConverter.DadoPadronizado>(dadoPadronizadoJson);

                    // Validar o dado
                    _formatConverter.ValidarDado(dadoPadronizadoJson);

                    // Uniformizar a taxa de amostragem
                    var dadoUniformizado = await _rateUniformizer.ProcessarDado(dadoPadronizado);

                    // Converter de volta para JSON (formato padrão de retorno)
                    return JsonSerializer.Serialize(dadoUniformizado);
                });
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro na uniformização de dados: {ex.Message}");
            }
        }

        public async Task<string> ProcessarDadosCompleto(string dados, string formatoOrigem, string formatoDestino)
        {
            try
            {
                return await Task.Run(async () =>
                {
                    // 1. Converter para o formato padronizado
                    var dadoPadronizadoJson = _formatConverter.ConverterParaPadrao(dados, formatoOrigem);
                    var dadoPadronizado = JsonSerializer.Deserialize<FormatConverter.DadoPadronizado>(dadoPadronizadoJson);

                    // 2. Validar o dado
                    _formatConverter.ValidarDado(dadoPadronizadoJson);

                    // 3. Uniformizar a taxa de amostragem
                    var dadoUniformizado = await _rateUniformizer.ProcessarDado(dadoPadronizado);

                    // 4. Criar requisição de análise
                    var request = new AnaliseRequest
                    {
                        WavyId = dadoUniformizado.SensorId,
                        TipoDado = dadoUniformizado.SensorId.Split('_')[1],
                        Valor = dadoUniformizado.Value.ToString(),
                        Timestamp = dadoUniformizado.Timestamp
                    };

                    // 5. Realizar análise básica
                    var analiseResultado = new AnaliseResultado
                    {
                        WavyId = request.WavyId,
                        TipoDado = request.TipoDado,
                        Timestamp = DateTime.UtcNow
                    };

                    // Converter resultado uniformizado para o formato de destino
                    return _formatConverter.ConverterParaFormato(new FormatConverter.DadoPadronizado
                    {
                        WavyId = dadoUniformizado.SensorId,
                        TipoDado = dadoUniformizado.SensorId.Split('_')[1],
                        Valor = dadoUniformizado.Value.ToString(),
                        Timestamp = dadoUniformizado.Timestamp,
                        MetaDados = new Dictionary<string, object>
                        {
                            ["originalRate"] = dadoUniformizado.OriginalRate.TotalMilliseconds,
                            ["uniformRate"] = dadoUniformizado.UniformRate.TotalMilliseconds
                        }
                    }, formatoDestino);
                });
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro no processamento completo: {ex.Message}");
            }
        }

        public async Task<Dictionary<string, double>> ObterTaxasAmostragem()
        {
            try
            {
                // Obter o dicionário de taxas de amostragem (sensorId -> taxa em ms)
                return _rateUniformizer.ObterConfiguracoes();
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro ao obter taxas de amostragem: {ex.Message}");
            }
        }

        public async Task<bool> AtualizarTaxaAmostragem(string tipoSensor, double novaTaxa)
        {
            try
            {
                _rateUniformizer.AtualizarConfiguracao(tipoSensor, novaTaxa);
                return true;
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro ao atualizar taxa de amostragem: {ex.Message}");
            }
        }

        public async Task<string> AnalisarDados(string dados, string tipoAnalise)
        {
            try
            {
                // Converter dados para o formato padronizado
                var dadoPadronizadoJson = _formatConverter.ConverterParaPadrao(dados, "json");
                var dadoPadronizado = JsonSerializer.Deserialize<FormatConverter.DadoPadronizado>(dadoPadronizadoJson);

                // Criar requisição de análise
                var request = new AnaliseRequest
                {
                    WavyId = dadoPadronizado.WavyId,
                    TipoDado = dadoPadronizado.TipoDado,
                    Valor = dadoPadronizado.Valor,
                    Timestamp = dadoPadronizado.Timestamp
                };

                // Realizar análise e retornar resultado em JSON
                var resultado = await Task.Run(() => JsonSerializer.Serialize(request));
                return resultado;
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro na análise de dados: {ex.Message}");
            }
        }

        public async Task<string> SubmeterAnaliseHPC(string dados, string tipoAnalise, Dictionary<string, object> parametros)
        {
            try
            {
                // Converter dados para o formato padronizado
                var dadoPadronizadoJson = _formatConverter.ConverterParaPadrao(dados, "json");
                var dadoPadronizado = JsonSerializer.Deserialize<FormatConverter.DadoPadronizado>(dadoPadronizadoJson);

                // Criar requisição HPC
                var request = new AnaliseHPCRequest
                {
                    WavyId = dadoPadronizado.WavyId,
                    TipoDado = dadoPadronizado.TipoDado,
                    Dados = new List<double> { double.Parse(dadoPadronizado.Valor) },
                    TipoAnalise = tipoAnalise,
                    Parametros = parametros
                };

                // Submeter análise ao HPC
                var jobId = await _hpcService.SubmeterAnaliseHPC(request);
                return jobId;
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro ao submeter análise HPC: {ex.Message}");
            }
        }

        public async Task<string> ObterStatusAnaliseHPC(string jobId)
        {
            try
            {
                var status = await _hpcService.ObterStatusJob(jobId);
                return status;
            }
            catch (Exception ex)
            {
                throw new FaultException($"Erro ao obter status da análise HPC: {ex.Message}");
            }
        }
    }

    public class DadosSensor
    {
        public double Valor { get; set; }
        public DateTime Timestamp { get; set; }
    }
} 