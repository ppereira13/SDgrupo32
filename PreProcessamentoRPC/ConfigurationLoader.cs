using System;
using System.IO;
using System.Text.Json;

namespace PreProcessamentoRPC
{
    public class ConfigurationLoader
    {
        private static readonly Lazy<ConfigurationLoader> _instance = 
            new Lazy<ConfigurationLoader>(() => new ConfigurationLoader());
        
        private JsonDocument _config;

        public static ConfigurationLoader Instance => _instance.Value;

        private ConfigurationLoader()
        {
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                string jsonString = File.ReadAllText(configPath);
                _config = JsonDocument.Parse(jsonString);
                
                Logger.Instance.Info("Configurações carregadas com sucesso");
            }
            catch (Exception ex)
            {
                Logger.Instance.Critical("Erro ao carregar configurações", ex);
                throw;
            }
        }

        public T GetValue<T>(string section, string key, T defaultValue = default)
        {
            try
            {
                var element = _config.RootElement.GetProperty(section).GetProperty(key);
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                Logger.Instance.Warning($"Configuração não encontrada: {section}.{key}. Usando valor padrão.");
                return defaultValue;
            }
        }

        public string GetConnectionString()
        {
            var rabbitConfig = _config.RootElement.GetProperty("RabbitMQ");
            return $"amqp://{rabbitConfig.GetProperty("UserName").GetString()}:" +
                   $"{rabbitConfig.GetProperty("Password").GetString()}@" +
                   $"{rabbitConfig.GetProperty("HostName").GetString()}:" +
                   $"{rabbitConfig.GetProperty("Port").GetInt32()}/" +
                   $"{rabbitConfig.GetProperty("VirtualHost").GetString()}";
        }

        public int GetSensorRate(string sensorType)
        {
            try
            {
                return _config.RootElement
                    .GetProperty("SensorRates")
                    .GetProperty(sensorType)
                    .GetInt32();
            }
            catch
            {
                Logger.Instance.Warning($"Taxa de amostragem não encontrada para sensor: {sensorType}. Usando valor padrão (1000ms).");
                return 1000;
            }
        }

        public CircuitBreakerConfig GetCircuitBreakerConfig()
        {
            return new CircuitBreakerConfig
            {
                FailureThreshold = GetValue<int>("CircuitBreaker", "FailureThreshold", 3),
                ResetTimeoutSeconds = GetValue<int>("CircuitBreaker", "ResetTimeoutSeconds", 60)
            };
        }

        public DataProcessingConfig GetDataProcessingConfig()
        {
            return new DataProcessingConfig
            {
                BufferSize = GetValue<int>("DataProcessing", "BufferSize", 1000),
                ProcessingInterval = GetValue<int>("DataProcessing", "ProcessingInterval", 1000),
                MaxRetryAttempts = GetValue<int>("DataProcessing", "MaxRetryAttempts", 3),
                RetryDelayMs = GetValue<int>("DataProcessing", "RetryDelayMs", 1000)
            };
        }
    }

    public class CircuitBreakerConfig
    {
        public int FailureThreshold { get; set; }
        public int ResetTimeoutSeconds { get; set; }
    }

    public class DataProcessingConfig
    {
        public int BufferSize { get; set; }
        public int ProcessingInterval { get; set; }
        public int MaxRetryAttempts { get; set; }
        public int RetryDelayMs { get; set; }
    }
} 