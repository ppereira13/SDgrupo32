using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Servidor.Models;

namespace Servidor.Services
{
    public class MongoDBService
    {
        private readonly IMongoCollection<WavyData> _wavyDataCollection;
        private readonly IMongoDatabase _database;

        public MongoDBService(string connectionString)
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);
            settings.RetryWrites = true;
            settings.RetryReads = true;

            var mongoClient = new MongoClient(settings);
            _database = mongoClient.GetDatabase("WavyDB");
            _wavyDataCollection = _database.GetCollection<WavyData>("WavyData");
        }

        public async Task SaveDataAsync(WavyData data)
        {
            await _wavyDataCollection.InsertOneAsync(data);
        }

        public async Task<List<string>> GetWavyIdsAsync()
        {
            var filter = Builders<WavyData>.Filter.Empty;
            var wavyIds = await _wavyDataCollection.Distinct<string>("wavyId", filter).ToListAsync();
            return wavyIds;
        }

        public async Task<List<string>> GetDataTypesForWavyAsync(string wavyId)
        {
            var filter = Builders<WavyData>.Filter.Eq("wavyId", wavyId);
            var dataTypes = await _wavyDataCollection.Distinct<string>("dataType", filter).ToListAsync();
            return dataTypes;
        }

        public async Task<List<WavyData>> GetLatestDataForWavyAsync(string wavyId, int limit = 25)
        {
            var filter = Builders<WavyData>.Filter.Eq("wavyId", wavyId);
            var sort = Builders<WavyData>.Sort.Descending("timestamp");
            return await _wavyDataCollection.Find(filter).Sort(sort).Limit(limit).ToListAsync();
        }

        public async Task<WavyData?> GetLatestDataPointAsync(string wavyId, string dataType)
        {
            var filter = Builders<WavyData>.Filter.And(
                Builders<WavyData>.Filter.Eq("wavyId", wavyId),
                Builders<WavyData>.Filter.Eq("dataType", dataType)
            );
            var sort = Builders<WavyData>.Sort.Descending("timestamp");
            return await _wavyDataCollection.Find(filter).Sort(sort).FirstOrDefaultAsync();
        }

        public async Task<List<ChartDataPoint>> GetDataForChartAsync(string wavyId, string dataType)
        {
            var filter = Builders<WavyData>.Filter.And(
                Builders<WavyData>.Filter.Eq("wavyId", wavyId),
                Builders<WavyData>.Filter.Eq("dataType", dataType)
            );
            var sort = Builders<WavyData>.Sort.Ascending("timestamp");
            var data = await _wavyDataCollection.Find(filter)
                                              .Sort(sort)
                                              .Limit(1000)
                                              .ToListAsync();

            return data.Select(d => new ChartDataPoint
            {
                Timestamp = d.Timestamp,
                Value = double.Parse(d.Value)
            }).ToList();
        }

        public async Task<List<WavyData>> GetDataForExportAsync(string wavyId, string dataType, DateTime? startTime, DateTime? endTime)
        {
            var filterBuilder = Builders<WavyData>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq("wavyId", wavyId),
                filterBuilder.Eq("dataType", dataType)
            );

            if (startTime.HasValue)
                filter = filter & filterBuilder.Gte("timestamp", startTime.Value);
            if (endTime.HasValue)
                filter = filter & filterBuilder.Lte("timestamp", endTime.Value);

            return await _wavyDataCollection.Find(filter)
                                          .Sort(Builders<WavyData>.Sort.Ascending("timestamp"))
                                          .ToListAsync();
        }

        public async Task<AnalysisResult> AnalyzeDataAsync(string wavyId, string dataType, DateTime startTime, DateTime endTime, string analysisType)
        {
            var filter = Builders<WavyData>.Filter.And(
                Builders<WavyData>.Filter.Eq("wavyId", wavyId),
                Builders<WavyData>.Filter.Eq("dataType", dataType),
                Builders<WavyData>.Filter.Gte("timestamp", startTime),
                Builders<WavyData>.Filter.Lte("timestamp", endTime)
            );

            var data = await _wavyDataCollection.Find(filter)
                                              .Sort(Builders<WavyData>.Sort.Ascending("timestamp"))
                                              .ToListAsync();

            var values = data.Select(d => double.Parse(d.Value)).ToList();

            var result = new AnalysisResult
            {
                WavyId = wavyId,
                DataType = dataType,
                StartTime = startTime,
                EndTime = endTime,
                AnalysisType = analysisType,
                DataPoints = data.Count,
                Value = 0
            };

            if (values.Count > 0)
            {
                switch (analysisType.ToLower())
                {
                    case "media":
                        result.Value = values.Average();
                        break;
                    case "mediana":
                        result.Value = values.OrderBy(v => v).ElementAt(values.Count / 2);
                        break;
                    case "desvio":
                        var mean = values.Average();
                        result.Value = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));
                        break;
                    case "tendencia":
                        var xValues = Enumerable.Range(0, values.Count).Select(i => (double)i).ToList();
                        var xMean = xValues.Average();
                        var yMean = values.Average();
                        var numerator = xValues.Zip(values, (x, y) => (x - xMean) * (y - yMean)).Sum();
                        var denominator = xValues.Sum(x => Math.Pow(x - xMean, 2));
                        result.Value = numerator / denominator;
                        break;
                    default:
                        throw new ArgumentException("Tipo de análise não suportado");
                }
            }

            return result;
        }

        public async Task UpdateAnalysisResultAsync(string id, Dictionary<string, string> results)
        {
            var filter = Builders<WavyData>.Filter.Eq("_id", ObjectId.Parse(id));
            var update = Builders<WavyData>.Update.Set("analysisResults", results);
            await _wavyDataCollection.UpdateOneAsync(filter, update);
        }

        public async Task UpdateWavyStatusAsync(string wavyId, string status)
        {
            var data = new WavyData
            {
                WavyId = wavyId,
                DataType = "status",
                Value = status,
                Timestamp = DateTime.UtcNow
            };

            await SaveDataAsync(data);
        }

        public async Task<string> GetWavyStatusAsync(string wavyId)
        {
            try
            {
                var filter = Builders<WavyData>.Filter.And(
                    Builders<WavyData>.Filter.Eq("wavyId", wavyId),
                    Builders<WavyData>.Filter.Eq("dataType", "status")
                );
                var sort = Builders<WavyData>.Sort.Descending("timestamp");
                
                Console.WriteLine($"Buscando status mais recente para WAVY {wavyId}...");
                var lastStatus = await _wavyDataCollection.Find(filter)
                                                        .Sort(sort)
                                                        .FirstOrDefaultAsync();
                
                if (lastStatus != null)
                {
                    Console.WriteLine($"Status encontrado para WAVY {wavyId}: {lastStatus.Value} (Timestamp: {lastStatus.Timestamp})");
                    
                    // Se o valor for uma string, retorna diretamente
                    if (lastStatus.Value == "operacao" || 
                        lastStatus.Value == "associada" || 
                        lastStatus.Value == "desativada")
                    {
                        return lastStatus.Value;
                    }
                    
                    // Se for um valor numérico, interpreta como status de operação
                    if (lastStatus.Value == "1" || lastStatus.Value == "0")
                    {
                        // Busca o último status não-numérico
                        var textStatusFilter = Builders<WavyData>.Filter.And(
                            Builders<WavyData>.Filter.Eq("wavyId", wavyId),
                            Builders<WavyData>.Filter.Eq("dataType", "status"),
                            Builders<WavyData>.Filter.Or(
                                Builders<WavyData>.Filter.Eq("value", "operacao"),
                                Builders<WavyData>.Filter.Eq("value", "associada"),
                                Builders<WavyData>.Filter.Eq("value", "desativada")
                            )
                        );
                        
                        var lastTextStatus = await _wavyDataCollection.Find(textStatusFilter)
                                                                  .Sort(sort)
                                                                  .FirstOrDefaultAsync();
                        
                        if (lastTextStatus != null)
                        {
                            Console.WriteLine($"Último status textual encontrado: {lastTextStatus.Value}");
                            return lastTextStatus.Value;
                        }
                    }
                }
                
                Console.WriteLine($"Nenhum status válido encontrado para WAVY {wavyId}");
                return "desativada"; // Status padrão
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar status da WAVY {wavyId}: {ex.Message}");
                return "desativada";
            }
        }

        public async Task<DateTime?> GetLastDataTimeAsync(string wavyId)
        {
            try
            {
                Console.WriteLine($"Buscando último timestamp para WAVY {wavyId}...");
                
                var filter = Builders<WavyData>.Filter.And(
                    Builders<WavyData>.Filter.Eq("wavyId", wavyId),
                    Builders<WavyData>.Filter.Ne("dataType", "config") // Ignorar mensagens de configuração
                );
                var sort = Builders<WavyData>.Sort.Descending("timestamp");
                
                var lastData = await _wavyDataCollection.Find(filter)
                                                      .Sort(sort)
                                                      .FirstOrDefaultAsync();
                
                if (lastData != null)
                {
                    Console.WriteLine($"Último dado da WAVY {wavyId}: {lastData.DataType} = {lastData.Value} (Timestamp: {lastData.Timestamp})");
                    return lastData.Timestamp;
                }
                else
                {
                    Console.WriteLine($"Nenhum dado encontrado para WAVY {wavyId}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar último timestamp da WAVY {wavyId}: {ex.Message}");
                return null;
            }
        }
    }

    public class ChartDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class AnalysisResult
    {
        public string WavyId { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string AnalysisType { get; set; } = string.Empty;
        public int DataPoints { get; set; }
        public double Value { get; set; }
    }
} 