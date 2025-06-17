using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Agregador
{
    public class DataPreProcessor : IDisposable
    {
        private readonly ConfigLoader _configLoader;
        private readonly Dictionary<string, Queue<string>> _dataBuffer;
        private readonly Dictionary<string, Timer> _dataSendTimers;
        private readonly object _dataBufferLock = new object();
        private readonly RPCPreProcessor _rpcPreProcessor;
        private readonly MessageBroker _messageBroker = new MessageBroker();

        // Configuração padrão do servidor
        private const string DEFAULT_SERVER_IP = "127.0.0.1";
        private const int DEFAULT_SERVER_PORT = 6000;

        public DataPreProcessor(ConfigLoader configLoader)
        {
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _dataBuffer = new Dictionary<string, Queue<string>>();
            _dataSendTimers = new Dictionary<string, Timer>();
            _rpcPreProcessor = new RPCPreProcessor();
        }

        // Espacamento
        protected void LogProcessingInfo(string info)
        {
            if (string.IsNullOrEmpty(info))
                return;

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(info);
            Console.WriteLine();
        }

        // Processa dados recebidos de uma WAVY
        public async Task<bool> ProcessData(string wavyId, string dataType, string value)
        {
            if (string.IsNullOrEmpty(wavyId))
                throw new ArgumentNullException(nameof(wavyId));
            if (string.IsNullOrEmpty(dataType))
                throw new ArgumentNullException(nameof(dataType));
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));

            try
            {
                if (!_configLoader.WavyConfigs.TryGetValue(wavyId, out WavyConfig? wavyConfig) ||
                    wavyConfig == null ||
                    wavyConfig.Status != "operacao")
                {
                    LogProcessingInfo($"WAVY {wavyId} não está em estado operacional ou não registrada");
                    return false;
                }

                if (!wavyConfig.DataTypes.Contains(dataType))
                {
                    LogProcessingInfo($"Tipo de dados {dataType} não suportado pela WAVY {wavyId}");
                    return false;
                }

                if (!_configLoader.DataProcessingConfigs.TryGetValue(dataType, out List<DataProcessingConfig>? configs) ||
                    configs == null)
                {
                    LogProcessingInfo($"Não há configurações de processamento para o tipo de dados {dataType}");
                    return false;
                }

                DataProcessingConfig? config = configs.Find(c => c.WavyId == wavyId) ?? 
                                            configs.Find(c => c.WavyId == "*");

                if (config == null)
                {
                    LogProcessingInfo($"Não há configuração de processamento específica ou padrão para WAVY {wavyId}");
                    return false;
                }

                try
                {
                    string processedValue = await _rpcPreProcessor.PreProcessarDadosAsync(wavyId, dataType, value);
                    if (!string.IsNullOrEmpty(processedValue))
                    {
                        LogProcessingInfo($"Dados pré-processados via RPC: {processedValue}");

                        string bufferKey = $"{wavyId}_{dataType}";
                        lock (_dataBufferLock)
                        {
                            if (!_dataBuffer.ContainsKey(bufferKey))
                            {
                                _dataBuffer[bufferKey] = new Queue<string>();
                            }
                            _dataBuffer[bufferKey].Enqueue(processedValue);
                        }
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    LogProcessingInfo($"Erro no pré-processamento RPC: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogProcessingInfo($"Erro no processamento de dados: {ex.Message}");
                return false;
            }
        }

        // Aplica pré-processamento aos dados
        private string ApplyPreProcessing(string value, string preProcessingType)
        {
            // Implementar diferentes tipos de pré-processamento
            switch (preProcessingType.ToLower())
            {
                case "normalize":
                    // Exemplo simples de normalização
                    if (double.TryParse(value, out double numValue))
                    {
                        // Normalização simples entre 0 e 1
                        double normalizedValue = Math.Max(0, Math.Min(1, numValue / 100.0));
                        return normalizedValue.ToString("F4");
                    }
                    break;

                case "average":
                    // Exemplo: valor médio seria implementado melhor com um buffer real
                    return value; // Simplificado

                case "compress":
                    // Compressão simples aqui seria apenas uma ilustração
                    return value; // Simplificado

                default:
                    // Sem pré-processamento
                    return value;
            }

            return value;
        }

        // Determina intervalo de envio baseado na configuração de volume
        private int DetermineSendInterval(string volumeConfig)
        {
            switch (volumeConfig.ToLower())
            {
                case "high":
                    return 1000; // 1 segundo
                case "medium":
                    return 5000; // 5 segundos
                case "low":
                    return 10000; // 10 segundos
                default:
                    return 5000; // Padrão: 5 segundos
            }
        }

        // Callback do timer para envio de dados acumulados
        private void SendDataCallback(object state)
        {
            string bufferKey = (string)state;

            lock (_dataBufferLock)
            {
                if (_dataBuffer.ContainsKey(bufferKey) && _dataBuffer[bufferKey].Count > 0)
                {
                    string[] keyParts = bufferKey.Split('_');
                    if (keyParts.Length >= 2)
                    {
                        string wavyId = keyParts[0];
                        string dataType = keyParts[1];

                        // Construir mensagem com todos os dados acumulados
                        var dataCount = _dataBuffer[bufferKey].Count;
                        var dataValues = new List<string>();

                        // Extrair até 100 valores do buffer para envio (para evitar mensagens muito grandes)
                        for (int i = 0; i < Math.Min(dataCount, 100); i++)
                        {
                            dataValues.Add(_dataBuffer[bufferKey].Dequeue());
                        }

                        string batchData = string.Join(";", dataValues);

                        // Enviar dados em lote para o Servidor via RabbitMQ
                        _messageBroker.PublishMessage(wavyId, dataType, batchData);
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (var timer in _dataSendTimers.Values)
            {
                timer.Dispose();
            }
            _dataSendTimers.Clear();
            _dataBuffer.Clear();
            _rpcPreProcessor.Dispose();
            _messageBroker.Dispose();
        }
    }
}