using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PreProcessamentoRPC
{
    public class RateUniformizer
    {
        private readonly Dictionary<string, Queue<DataPoint>> _dataBuffers;
        private readonly Dictionary<string, DateTime> _lastProcessedTime;
        public readonly Dictionary<string, TimeSpan> _sensorRates;

        public RateUniformizer()
        {
            _dataBuffers = new Dictionary<string, Queue<DataPoint>>();
            _lastProcessedTime = new Dictionary<string, DateTime>();
            _sensorRates = new Dictionary<string, TimeSpan>();
        }

        public void ConfigureSensorRate(string sensorId, TimeSpan rate)
        {
            _sensorRates[sensorId] = rate;
            if (!_dataBuffers.ContainsKey(sensorId))
            {
                _dataBuffers[sensorId] = new Queue<DataPoint>();
            }
        }

        public void AddData(string sensorId, double value, DateTime timestamp)
        {
            if (!_dataBuffers.ContainsKey(sensorId))
            {
                _dataBuffers[sensorId] = new Queue<DataPoint>();
            }

            _dataBuffers[sensorId].Enqueue(new DataPoint { Value = value, Timestamp = timestamp });
        }

        public List<UniformData> UniformizeSensorData(TimeSpan targetRate)
        {
            var result = new List<UniformData>();
            var now = DateTime.UtcNow;

            foreach (var sensorId in _dataBuffers.Keys)
            {
                if (!_lastProcessedTime.ContainsKey(sensorId))
                {
                    _lastProcessedTime[sensorId] = now - targetRate;
                }

                var buffer = _dataBuffers[sensorId];
                var sensorRate = _sensorRates.GetValueOrDefault(sensorId, targetRate);

                while (buffer.Count >= 2)
                {
                    var nextProcessTime = _lastProcessedTime[sensorId] + targetRate;
                    if (nextProcessTime > now) break;

                    var data1 = buffer.Peek();
                    var data2 = buffer.Skip(1).First();

                    if (nextProcessTime >= data1.Timestamp && nextProcessTime <= data2.Timestamp)
                    {
                        // Interpolação linear
                        var interpolatedValue = InterpolateValue(
                            data1.Timestamp, data1.Value,
                            data2.Timestamp, data2.Value,
                            nextProcessTime);

                        result.Add(new UniformData
                        {
                            SensorId = sensorId,
                            Value = interpolatedValue,
                            Timestamp = nextProcessTime,
                            OriginalRate = sensorRate,
                            UniformRate = targetRate
                        });

                        _lastProcessedTime[sensorId] = nextProcessTime;
                        buffer.Dequeue(); // Remove o ponto mais antigo
                    }
                    else if (nextProcessTime > data2.Timestamp)
                    {
                        buffer.Dequeue(); // Descarta dados muito antigos
                    }
                    else
                    {
                        break; // Aguarda mais dados
                    }
                }
            }

            return result;
        }

        private double InterpolateValue(DateTime t1, double v1, DateTime t2, double v2, DateTime target)
        {
            var totalTime = (t2 - t1).TotalMilliseconds;
            var targetTime = (target - t1).TotalMilliseconds;
            var ratio = targetTime / totalTime;
            return v1 + (v2 - v1) * ratio;
        }

        public void ClearOldData(TimeSpan threshold)
        {
            var cutoffTime = DateTime.UtcNow - threshold;

            foreach (var sensorId in _dataBuffers.Keys)
            {
                var buffer = _dataBuffers[sensorId];
                while (buffer.Count > 0 && buffer.Peek().Timestamp < cutoffTime)
                {
                    buffer.Dequeue();
                }
            }
        }

        public async Task<UniformData> ProcessarDado(FormatConverter.DadoPadronizado dado)
        {
            // Adiciona o dado ao buffer
            AddData(dado.TipoDado, double.Parse(dado.Valor), dado.Timestamp);

            // Uniformiza os dados com a taxa configurada para este tipo de sensor
            var taxaPadrao = _sensorRates.GetValueOrDefault(dado.TipoDado, TimeSpan.FromSeconds(1));
            var dadosUniformes = UniformizeSensorData(taxaPadrao);

            // Retorna o último dado uniformizado
            return dadosUniformes.LastOrDefault() ?? new UniformData
            {
                SensorId = dado.TipoDado,
                Value = double.Parse(dado.Valor),
                Timestamp = dado.Timestamp,
                OriginalRate = taxaPadrao,
                UniformRate = taxaPadrao
            };
        }

        public Dictionary<string, double> ObterConfiguracoes()
        {
            return _sensorRates.ToDictionary(
                x => x.Key,
                x => x.Value.TotalMilliseconds
            );
        }

        public void AtualizarConfiguracao(string tipoSensor, double taxaMs)
        {
            _sensorRates[tipoSensor] = TimeSpan.FromMilliseconds(taxaMs);
        }

        public class DataPoint
        {
            public double Value { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class UniformData
        {
            public required string SensorId { get; set; }
            public double Value { get; set; }
            public DateTime Timestamp { get; set; }
            public TimeSpan OriginalRate { get; set; }
            public TimeSpan UniformRate { get; set; }
        }
    }
} 