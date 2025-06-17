using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Servidor.Services;
using System.Linq;
using CsvHelper;
using System.IO;
using System.Globalization;
using Servidor.Models;

namespace Servidor.Controllers
{
    public class HomeController : Controller
    {
        private readonly MongoDBService _mongoService;

        public HomeController(MongoDBService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var wavyIds = await _mongoService.GetWavyIdsAsync();
                Console.WriteLine($"WAVYs encontradas: {string.Join(", ", wavyIds)}");
                
                var wavysAtivas = new List<string>();
                
                foreach (var wavyId in wavyIds)
                {
                    var status = await _mongoService.GetWavyStatusAsync(wavyId);
                    Console.WriteLine($"Status da WAVY {wavyId}: {status}");
                    
                    if (status == "operacao")
                    {
                        var lastDataTime = await _mongoService.GetLastDataTimeAsync(wavyId);
                        var isActive = lastDataTime.HasValue && 
                                     (DateTime.UtcNow - lastDataTime.Value).TotalMinutes <= 5;
                        
                        if (isActive)
                        {
                            wavysAtivas.Add(wavyId);
                            Console.WriteLine($"WAVY {wavyId} está ativa (último dado: {lastDataTime})");
                        }
                        else
                        {
                            Console.WriteLine($"WAVY {wavyId} está inativa (último dado: {lastDataTime})");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"WAVY {wavyId} não está em operação (status: {status})");
                    }
                }

                ViewBag.WavysAtivas = wavysAtivas;
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar página inicial: {ex.Message}");
                ViewBag.WavysAtivas = new List<string>();
                return View();
            }
        }

        public async Task<IActionResult> WavyDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            ViewBag.WavyId = id;
            ViewBag.DataTypes = await _mongoService.GetDataTypesForWavyAsync(id);
            ViewBag.LatestData = await _mongoService.GetLatestDataForWavyAsync(id, 25);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetChartData(string wavyId, string dataType)
        {
            if (string.IsNullOrEmpty(wavyId) || string.IsNullOrEmpty(dataType))
            {
                return Json(new List<ChartDataPoint>());
            }

            var data = await _mongoService.GetDataForChartAsync(wavyId, dataType);
            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> RequestAnalysis([FromBody] AnalysisRequest request)
        {
            try
            {
                if (request == null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                var result = await _mongoService.AnalyzeDataAsync(
                    request.WavyId,
                    request.DataType,
                    request.StartTime,
                    request.EndTime,
                    request.AnalysisType
                );
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportData(string wavyId, string dataType, DateTime? startTime, DateTime? endTime)
        {
            if (string.IsNullOrEmpty(wavyId) || string.IsNullOrEmpty(dataType))
            {
                return BadRequest("WavyId e DataType são obrigatórios");
            }

            var data = await _mongoService.GetDataForExportAsync(wavyId, dataType, startTime, endTime);
            
            using (var memoryStream = new MemoryStream())
            using (var writer = new StreamWriter(memoryStream))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(data);
                writer.Flush();
                return File(memoryStream.ToArray(), "text/csv", $"{wavyId}_{dataType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRealTimeData(string wavyId, string dataType)
        {
            if (string.IsNullOrEmpty(wavyId) || string.IsNullOrEmpty(dataType))
            {
                return Json(null);
            }

            var data = await _mongoService.GetLatestDataPointAsync(wavyId, dataType);
            return Json(data);
        }
    }

    public class AnalysisRequest
    {
        public string WavyId { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string AnalysisType { get; set; } = string.Empty;
    }

    public class WavyStatus
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? LastDataTime { get; set; }
    }
} 