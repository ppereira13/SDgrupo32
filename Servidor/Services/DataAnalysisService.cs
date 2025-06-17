using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Servidor.Models;

namespace Servidor.Services
{
    public class DataAnalysisService
    {
        private readonly MongoDBService _mongoService;

        public DataAnalysisService(MongoDBService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task<Dictionary<string, string>> AnalyzeData(WavyAnalysisRequest request)
        {
            var filter = new Dictionary<string, object>
            {
                { "WavyId", request.WavyId },
                { "DataType", request.DataType },
                { "StartTime", request.StartTime },
                { "EndTime", request.EndTime }
            };

            var data = await _mongoService.GetDataForExportAsync(
                request.WavyId,
                request.DataType,
                request.StartTime,
                request.EndTime
            );

            if (data == null || !data.Any())
            {
                return new Dictionary<string, string>
                {
                    { "erro", "Nenhum dado encontrado para o período especificado" }
                };
            }

            var values = data.Select(d => double.Parse(d.Value)).ToList();
            var results = new Dictionary<string, string>();

            switch (request.AnalysisType.ToLower())
            {
                case "media":
                    {
                        double media = values.Average();
                        results.Add("média", media.ToString("F2"));
                    }
                    break;

                case "mediana":
                    {
                        var sortedValues = values.OrderBy(v => v).ToList();
                        double median;
                        int count = sortedValues.Count;
                        
                        if (count % 2 == 0)
                        {
                            median = (sortedValues[(count / 2) - 1] + sortedValues[count / 2]) / 2.0;
                        }
                        else
                        {
                            median = sortedValues[count / 2];
                        }
                        
                        results.Add("mediana", median.ToString("F2"));
                    }
                    break;

                case "desvio":
                    {
                        double mean = values.Average();
                        double sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
                        double stdDev = Math.Sqrt(sumOfSquares / (values.Count - 1));
                        results.Add("desvio_padrao", stdDev.ToString("F2"));
                    }
                    break;

                case "tendencia":
                    {
                        int n = values.Count;
                        double[] timestamps = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
                        double sumX = timestamps.Sum();
                        double sumY = values.Sum();
                        double sumXY = timestamps.Zip(values, (x, y) => x * y).Sum();
                        double sumX2 = timestamps.Sum(x => x * x);

                        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
                        string trend = slope > 0 ? "crescente" : slope < 0 ? "decrescente" : "estável";
                        
                        results.Add("tendencia", trend);
                        results.Add("variacao_por_amostra", slope.ToString("F4"));
                    }
                    break;

                default:
                    results.Add("erro", "Tipo de análise não suportado");
                    break;
            }

            // Adicionar metadados da análise
            results.Add("total_amostras", values.Count.ToString());
            results.Add("periodo_inicio", request.StartTime.ToString("dd/MM/yyyy HH:mm:ss"));
            results.Add("periodo_fim", request.EndTime.ToString("dd/MM/yyyy HH:mm:ss"));

            return results;
        }
    }
} 