using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using WavyDashboard.Models;

namespace WavyDashboard.Services
{
    public class WavyAnalyticsService
    {
        private readonly IMongoCollection<WavyDataPoint> _dataCollection;
        private readonly IMongoDatabase _database;
        private readonly MongoClient _client;

        public WavyAnalyticsService(string connectionString)
        {
            try
            {
                Console.WriteLine($"Tentando conectar ao MongoDB com string de conexão: {connectionString}");
                _client = new MongoClient(connectionString);
                _database = _client.GetDatabase("WavyDB");
                _dataCollection = _database.GetCollection<WavyDataPoint>("WavyData");
                Console.WriteLine("Conexão com MongoDB estabelecida com sucesso!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao conectar com MongoDB: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<bool> CheckDatabaseConnection()
        {
            try
            {
                await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
                var collections = await _database.ListCollectionNamesAsync();
                var collectionList = await collections.ToListAsync();
                
                Console.WriteLine($"Conexão OK. Collections encontradas: {string.Join(", ", collectionList)}");
                
                // Verificar se existem documentos
                var count = await _dataCollection.CountDocumentsAsync(FilterDefinition<WavyDataPoint>.Empty);
                Console.WriteLine($"Total de documentos na coleção: {count}");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao verificar conexão: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<List<WavyDataPoint>> GetDataPointsAsync(string wavyId, string dataType, DateTime startTime, DateTime endTime)
        {
            var filter = Builders<WavyDataPoint>.Filter.And(
                Builders<WavyDataPoint>.Filter.Eq(x => x.WavyId, wavyId),
                Builders<WavyDataPoint>.Filter.Eq(x => x.DataType, dataType),
                Builders<WavyDataPoint>.Filter.Gte(x => x.Timestamp, startTime),
                Builders<WavyDataPoint>.Filter.Lte(x => x.Timestamp, endTime)
            );

            return await _dataCollection.Find(filter)
                .SortBy(x => x.Timestamp)
                .ToListAsync();
        }

        public async Task<List<WavyStatus>> GetWavyStatusesAsync()
        {
            var statuses = new List<WavyStatus>();
            var wavyIds = await GetAvailableWavyIdsAsync();
            var now = DateTime.UtcNow;
            
            foreach (var id in wavyIds)
            {
                try
                {
                    var filter = Builders<WavyDataPoint>.Filter.Eq(x => x.WavyId, id);
                    var lastData = await _dataCollection.Find(filter)
                        .SortByDescending(x => x.Timestamp)
                        .FirstOrDefaultAsync();

                    if (lastData != null)
                    {
                        // Considera um WAVY ativo se teve dados nos últimos 5 minutos
                        var timeDiff = (now - lastData.Timestamp.ToUniversalTime()).TotalMinutes;
                        var isActive = timeDiff <= 5;
                        
                        Console.WriteLine($"[DEBUG] WAVY {id}: Última atualização há {timeDiff:F2} minutos, Status: {(isActive ? "Conectada" : "Inativa")}");
                        
                        statuses.Add(new WavyStatus
                        {
                            Id = id,
                            Status = isActive ? "Conectada" : "Inativa",
                            LastUpdate = lastData.Timestamp
                        });
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] WAVY {id}: Nenhum dado encontrado");
                        statuses.Add(new WavyStatus
                        {
                            Id = id,
                            Status = "Inativa",
                            LastUpdate = DateTime.MinValue
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Erro ao processar status do WAVY {id}: {ex.Message}");
                }
            }

            return statuses;
        }

        public async Task<List<string>> GetAvailableWavyIdsAsync()
        {
            try
            {
                var filter = Builders<WavyDataPoint>.Filter.Empty;
                var wavyIds = await _dataCollection.Distinct<string>("wavyId", filter).ToListAsync();
                return wavyIds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter WAVYs disponíveis: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<WavyAnalysis> AnalyzeDataAsync(string wavyId, string dataType, DateTime startTime, DateTime endTime)
        {
            var data = await GetDataPointsAsync(wavyId, dataType, startTime, endTime);
            
            if (!data.Any())
                return null;

            var values = data.Where(x => double.TryParse(x.Value, out _)).Select(x => double.Parse(x.Value)).ToList();

            if (!values.Any())
                return null;

            values.Sort();

            var analysis = new WavyAnalysis
            {
                WavyId = wavyId,
                DataType = dataType,
                StartTime = startTime,
                EndTime = endTime,
                Mean = values.Average(),
                Median = values.Count % 2 == 0 
                    ? (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2 
                    : values[values.Count / 2],
                StdDev = CalculateStdDev(values),
                Min = values.First(),
                Max = values.Last()
            };

            // Análise de tendência
            var trend = CalculateTrend(values);
            analysis.Trend = trend.slope;
            analysis.Correlation = trend.correlation;

            // Análise de sazonalidade
            var seasonality = DetectSeasonality(values);
            analysis.SeasonalityStrength = seasonality.strength;
            analysis.SeasonalityPeriod = seasonality.period;

            // Detecção de anomalias
            var anomalies = DetectAnomalies(values);
            analysis.AnomalyCount = anomalies.count;
            analysis.AnomalySeverity = anomalies.severity;

            // Qualidade dos dados
            var quality = CalculateDataQuality(values);
            analysis.DataQuality = quality.score;
            analysis.DataConsistency = quality.consistency;

            // Estatísticas adicionais
            analysis.Statistics["Variância"] = CalculateVariance(values);
            analysis.Statistics["Amplitude"] = analysis.Max - analysis.Min;
            analysis.Statistics["Contagem"] = values.Count;
            analysis.Statistics["Tendência"] = analysis.Trend;
            analysis.Statistics["Correlação"] = analysis.Correlation;
            analysis.Statistics["Sazonalidade"] = analysis.SeasonalityStrength;
            analysis.Statistics["Período"] = analysis.SeasonalityPeriod;
            analysis.Statistics["Anomalias"] = analysis.AnomalyCount;
            analysis.Statistics["Severidade"] = analysis.AnomalySeverity;
            analysis.Statistics["Qualidade"] = analysis.DataQuality;
            analysis.Statistics["Consistência"] = analysis.DataConsistency;

            return analysis;
        }

        private (double slope, double correlation) CalculateTrend(List<double> values)
        {
            var n = values.Count;
            var xMean = (n - 1) / 2.0;
            var yMean = values.Average();
            
            var numerator = 0.0;
            var denominator = 0.0;
            
            for (var i = 0; i < n; i++)
            {
                numerator += (i - xMean) * (values[i] - yMean);
                denominator += Math.Pow(i - xMean, 2);
            }
            
            var slope = numerator / denominator;
            var correlation = numerator / (Math.Sqrt(denominator) * Math.Sqrt(values.Sum(x => Math.Pow(x - yMean, 2))));
            
            return (slope, Math.Abs(correlation));
        }

        private (double strength, int period) DetectSeasonality(List<double> values)
        {
            if (values.Count < 24) // Mínimo de 24 pontos para detectar sazonalidade
                return (0, 0);

            var n = values.Count;
            var maxPeriod = Math.Min(n / 2, 24); // Máximo de 24 períodos
            var bestPeriod = 1;
            var maxCorrelation = 0.0;

            for (var period = 2; period <= maxPeriod; period++)
            {
                var correlation = 0.0;
                var count = 0;

                for (var i = 0; i < n - period; i++)
                {
                    correlation += values[i] * values[i + period];
                    count++;
                }

                correlation /= count;
                if (correlation > maxCorrelation)
                {
                    maxCorrelation = correlation;
                    bestPeriod = period;
                }
            }

            return (maxCorrelation, bestPeriod);
        }

        private (int count, double severity) DetectAnomalies(List<double> values)
        {
            var mean = values.Average();
            var stdDev = CalculateStdDev(values);
            var upperThreshold = mean + 2 * stdDev;
            var lowerThreshold = mean - 2 * stdDev;
            
            var anomalies = values.Where(v => v > upperThreshold || v < lowerThreshold).ToList();
            var severity = anomalies.Any() 
                ? anomalies.Average(v => Math.Abs(v - mean) / stdDev)
                : 0;
            
            return (anomalies.Count, severity);
        }

        private (double score, double consistency) CalculateDataQuality(List<double> values)
        {
            var stdDev = CalculateStdDev(values);
            var range = values.Max() - values.Min();
            var consistency = 1 - (stdDev / range);
            
            // Pontuação baseada em vários fatores
            var score = 0.0;
            
            // Consistência dos dados
            score += consistency * 0.4;
            
            // Quantidade de dados
            var dataDensity = values.Count / (values.Max() - values.Min());
            score += Math.Min(dataDensity / 100, 1) * 0.3;
            
            // Ausência de anomalias
            var anomalies = DetectAnomalies(values);
            score += (1 - Math.Min(anomalies.count / (double)values.Count, 1)) * 0.3;
            
            return (score, consistency);
        }

        private double CalculateVariance(List<double> values)
        {
            var mean = values.Average();
            return values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
        }

        private double CalculateStdDev(List<double> values)
        {
            return Math.Sqrt(CalculateVariance(values));
        }

        public async Task<List<string>> GetAvailableDataTypesAsync(string wavyId)
        {
            var filter = Builders<WavyDataPoint>.Filter.Eq(x => x.WavyId, wavyId);
            return await _dataCollection.Distinct<string>("dataType", filter).ToListAsync();
        }

        public async Task<List<string>> GetAllAvailableDataTypesAsync()
        {
            try
            {
                var filter = Builders<WavyDataPoint>.Filter.Empty;
                return await _dataCollection.Distinct<string>("dataType", filter).ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter tipos de dados disponíveis: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<List<WavyDataPoint>> GetDataPointsFlexibleAsync(string wavyId, string dataType, DateTime startTime, DateTime endTime)
        {
            try
            {
                var filterBuilder = Builders<WavyDataPoint>.Filter;
                var filters = new List<FilterDefinition<WavyDataPoint>>
                {
                    filterBuilder.Gte(x => x.Timestamp, startTime),
                    filterBuilder.Lte(x => x.Timestamp, endTime)
                };

                if (!string.IsNullOrEmpty(wavyId) && wavyId != "Todos os dispositivos")
                {
                    filters.Add(filterBuilder.Eq(x => x.WavyId, wavyId));
                }

                if (!string.IsNullOrEmpty(dataType) && dataType != "Todos os sensores")
                {
                    filters.Add(filterBuilder.Eq(x => x.DataType, dataType));
                }

                var combinedFilter = filters.Any() ? filterBuilder.And(filters) : FilterDefinition<WavyDataPoint>.Empty;

                var data = await _dataCollection.Find(combinedFilter)
                                                .SortBy(x => x.Timestamp)
                                                .ToListAsync();
                
                Console.WriteLine($"GetDataPointsFlexibleAsync: Encontrados {data.Count} pontos de dados para WavyId: {wavyId}, DataType: {dataType}, StartTime: {startTime}, EndTime: {endTime}");
                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro em GetDataPointsFlexibleAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return new List<WavyDataPoint>();
            }
        }

        // NOVOS MÉTODOS PARA STATUS E CONFIG
        public async Task<List<(string Value, int Count)>> GetStatusCountsAsync(DateTime? start = null, DateTime? end = null)
        {
            var filterBuilder = Builders<WavyDataPoint>.Filter;
            var filters = new List<FilterDefinition<WavyDataPoint>>
            {
                filterBuilder.Eq(x => x.DataType, "status")
            };
            if (start.HasValue) filters.Add(filterBuilder.Gte(x => x.Timestamp, start.Value));
            if (end.HasValue) filters.Add(filterBuilder.Lte(x => x.Timestamp, end.Value));
            var filter = filterBuilder.And(filters);
            var data = await _dataCollection.Find(filter).ToListAsync();
            return data.GroupBy(d => d.Value).Select(g => (g.Key, g.Count())).ToList();
        }

        public async Task<List<(string Value, int Count)>> GetConfigCountsAsync(DateTime? start = null, DateTime? end = null)
        {
            var filterBuilder = Builders<WavyDataPoint>.Filter;
            var filters = new List<FilterDefinition<WavyDataPoint>>
            {
                filterBuilder.Eq(x => x.DataType, "config")
            };
            if (start.HasValue) filters.Add(filterBuilder.Gte(x => x.Timestamp, start.Value));
            if (end.HasValue) filters.Add(filterBuilder.Lte(x => x.Timestamp, end.Value));
            var filter = filterBuilder.And(filters);
            var data = await _dataCollection.Find(filter).ToListAsync();
            return data.GroupBy(d => d.Value).Select(g => (g.Key, g.Count())).ToList();
        }

        public async Task<List<(DateTime Timestamp, string Value)>> GetStatusHistoryAsync(string wavyId, int limit = 100)
        {
            var filter = Builders<WavyDataPoint>.Filter.And(
                Builders<WavyDataPoint>.Filter.Eq(x => x.WavyId, wavyId),
                Builders<WavyDataPoint>.Filter.Eq(x => x.DataType, "status")
            );
            var data = await _dataCollection.Find(filter).SortByDescending(x => x.Timestamp).Limit(limit).ToListAsync();
            return data.Select(d => (d.Timestamp, d.Value)).OrderBy(d => d.Timestamp).ToList();
        }

        public async Task<List<(DateTime Timestamp, string Value)>> GetConfigHistoryAsync(string wavyId, int limit = 100)
        {
            var filter = Builders<WavyDataPoint>.Filter.And(
                Builders<WavyDataPoint>.Filter.Eq(x => x.WavyId, wavyId),
                Builders<WavyDataPoint>.Filter.Eq(x => x.DataType, "config")
            );
            var data = await _dataCollection.Find(filter).SortByDescending(x => x.Timestamp).Limit(limit).ToListAsync();
            return data.Select(d => (d.Timestamp, d.Value)).OrderBy(d => d.Timestamp).ToList();
        }

        public async Task<RealTimeStats> GetRealTimeStatsAsync(string? sensorType = null)
        {
            try
            {
                var now = DateTime.UtcNow;
                var startTime = now.AddSeconds(-60); // últimos 60 segundos

                var filter = Builders<WavyDataPoint>.Filter.And(
                    Builders<WavyDataPoint>.Filter.Gte(x => x.Timestamp, startTime),
                    Builders<WavyDataPoint>.Filter.Lte(x => x.Timestamp, now)
                );

                if (!string.IsNullOrEmpty(sensorType))
                {
                    filter = filter & Builders<WavyDataPoint>.Filter.Eq(x => x.DataType, sensorType);
                }

                var data = await _dataCollection.Find(filter).ToListAsync();
                var values = data.Where(x => double.TryParse(x.Value, out _))
                                .Select(x => double.Parse(x.Value))
                                .ToList();

                if (!values.Any())
                {
                    return new RealTimeStats
                    {
                        ConnectedDevices = await GetConnectedDevicesCountAsync(),
                        AverageSensor = null,
                        MinValue = null,
                        MaxValue = null,
                        StdDev = null
                    };
                }

                return new RealTimeStats
                {
                    ConnectedDevices = await GetConnectedDevicesCountAsync(),
                    AverageSensor = values.Average(),
                    MinValue = values.Min(),
                    MaxValue = values.Max(),
                    StdDev = CalculateStdDev(values)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter estatísticas em tempo real: {ex.Message}");
                throw;
            }
        }

        private async Task<int> GetConnectedDevicesCountAsync()
        {
            var now = DateTime.UtcNow;
            var fiveMinutesAgo = now.AddMinutes(-5);

            var filter = Builders<WavyDataPoint>.Filter.Gte(x => x.Timestamp, fiveMinutesAgo);
            var distinctWavyIds = await _dataCollection.Distinct<string>("WavyId", filter).ToListAsync();
            return distinctWavyIds.Count;
        }

        public class RealTimeStats
        {
            public int ConnectedDevices { get; set; }
            public double? AverageSensor { get; set; }
            public double? MinValue { get; set; }
            public double? MaxValue { get; set; }
            public double? StdDev { get; set; }
        }
    }

    public class WavyStatus
    {
        public string Id { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? LastUpdate { get; set; }
    }
} 