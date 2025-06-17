using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Servidor
{
    public class RateUniformizer
    {
        private readonly Dictionary<string, SensorConfig> _sensorConfigs;
        private readonly Dictionary<string, List<DadoSensor>> _bufferDados;
        private readonly object _lockBuffer = new object();
        private readonly Timer _limpezaTimer;
        private const int TAMANHO_MAXIMO_BUFFER = 1000;
        private const int INTERVALO_LIMPEZA_MS = 300000; // 5 minutos

        public RateUniformizer()
        {
            _sensorConfigs = new Dictionary<string, SensorConfig>
            {
                { "acel", new SensorConfig { TaxaAmostragem = 10, IntervaloPadrao = 100 } },    // 10Hz
                { "gyro", new SensorConfig { TaxaAmostragem = 10, IntervaloPadrao = 100 } },    // 10Hz
                { "hidrofone", new SensorConfig { TaxaAmostragem = 20, IntervaloPadrao = 50 } }, // 20Hz
                { "transdutor", new SensorConfig { TaxaAmostragem = 5, IntervaloPadrao = 200 } }, // 5Hz
                { "status", new SensorConfig { TaxaAmostragem = 1, IntervaloPadrao = 1000 } },   // 1Hz
                { "camera", new SensorConfig { TaxaAmostragem = 1, IntervaloPadrao = 1000 } }    // 1Hz
            };

            _bufferDados = new Dictionary<string, List<DadoSensor>>();
            _limpezaTimer = new Timer(LimparDadosAntigos, null, INTERVALO_LIMPEZA_MS, INTERVALO_LIMPEZA_MS);
        }

        public async Task<DadoSensor> ProcessarDado(DadoPadronizado dado)
        {
            if (!_sensorConfigs.ContainsKey(dado.TipoDado.ToLower()))
                throw new ArgumentException($"Tipo de sensor não configurado: {dado.TipoDado}");

            var dadoSensor = new DadoSensor
            {
                WavyId = dado.WavyId,
                TipoDado = dado.TipoDado,
                Valor = double.Parse(dado.Valor),
                Timestamp = dado.Timestamp,
                MetaDados = dado.MetaDados
            };

            await AdicionarAoBuffer(dadoSensor);
            return await UniformizarTaxa(dadoSensor);
        }

        private async Task AdicionarAoBuffer(DadoSensor dado)
        {
            var chave = $"{dado.WavyId}_{dado.TipoDado}";

            await Task.Run(() =>
            {
                lock (_lockBuffer)
                {
                    if (!_bufferDados.ContainsKey(chave))
                        _bufferDados[chave] = new List<DadoSensor>();

                    _bufferDados[chave].Add(dado);

                    // Manter o tamanho do buffer limitado
                    if (_bufferDados[chave].Count > TAMANHO_MAXIMO_BUFFER)
                    {
                        _bufferDados[chave].RemoveAt(0);
                    }

                    // Ordenar por timestamp
                    _bufferDados[chave] = _bufferDados[chave]
                        .OrderBy(d => d.Timestamp)
                        .ToList();
                }
            });
        }

        private async Task<DadoSensor> UniformizarTaxa(DadoSensor dado)
        {
            var config = _sensorConfigs[dado.TipoDado.ToLower()];
            var chave = $"{dado.WavyId}_{dado.TipoDado}";

            return await Task.Run(() =>
            {
                lock (_lockBuffer)
                {
                    if (!_bufferDados.ContainsKey(chave) || _bufferDados[chave].Count < 2)
                        return dado;

                    var dados = _bufferDados[chave];
                    var ultimoDado = dados.Last();
                    var penultimoDado = dados[dados.Count - 2];

                    // Verificar se é necessário uniformizar
                    var intervaloReal = (ultimoDado.Timestamp - penultimoDado.Timestamp).TotalMilliseconds;
                    if (Math.Abs(intervaloReal - config.IntervaloPadrao) <= config.IntervaloPadrao * 0.1)
                        return dado;

                    // Realizar interpolação linear
                    var fatorInterpolacao = config.IntervaloPadrao / intervaloReal;
                    var valorInterpolado = InterpolacaoLinear(
                        penultimoDado.Valor,
                        ultimoDado.Valor,
                        fatorInterpolacao);

                    return new DadoSensor
                    {
                        WavyId = dado.WavyId,
                        TipoDado = dado.TipoDado,
                        Valor = valorInterpolado,
                        Timestamp = penultimoDado.Timestamp.AddMilliseconds(config.IntervaloPadrao),
                        MetaDados = new Dictionary<string, string>(dado.MetaDados ?? new Dictionary<string, string>())
                        {
                            { "interpolado", "true" },
                            { "taxa_original", intervaloReal.ToString("F2") }
                        }
                    };
                }
            });
        }

        private double InterpolacaoLinear(double valor1, double valor2, double fator)
        {
            return valor1 + (valor2 - valor1) * fator;
        }

        private void LimparDadosAntigos(object? state)
        {
            var limiteIdade = DateTime.UtcNow.AddHours(-1); // Manter dados de até 1 hora

            lock (_lockBuffer)
            {
                foreach (var chave in _bufferDados.Keys.ToList())
                {
                    _bufferDados[chave] = _bufferDados[chave]
                        .Where(d => d.Timestamp >= limiteIdade)
                        .ToList();
                }
            }
        }

        public Dictionary<string, SensorConfig> ObterConfiguracoes()
        {
            return new Dictionary<string, SensorConfig>(_sensorConfigs);
        }

        public void AtualizarConfiguracao(string tipoSensor, double novaTaxa)
        {
            if (!_sensorConfigs.ContainsKey(tipoSensor.ToLower()))
                throw new ArgumentException($"Tipo de sensor não encontrado: {tipoSensor}");

            _sensorConfigs[tipoSensor.ToLower()].TaxaAmostragem = novaTaxa;
            _sensorConfigs[tipoSensor.ToLower()].IntervaloPadrao = (int)(1000 / novaTaxa);
        }

        public class SensorConfig
        {
            public double TaxaAmostragem { get; set; }
            public int IntervaloPadrao { get; set; }
        }

        public class DadoSensor
        {
            public string WavyId { get; set; } = "";
            public string TipoDado { get; set; } = "";
            public double Valor { get; set; }
            public DateTime Timestamp { get; set; }
            public Dictionary<string, string>? MetaDados { get; set; }
        }
    }
} 