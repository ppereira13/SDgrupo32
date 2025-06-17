using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WavyDashboard.Services;
using WavyDashboard.Models;
using System.Collections.Generic;
using System.Linq;

namespace WavyDashboard.Controllers
{
    public class DashboardController : Controller
    {
        private readonly WavyAnalyticsService _analyticsService;

        public DashboardController(WavyAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                Console.WriteLine("Iniciando carregamento do Dashboard...");
                
                var dbStatus = await _analyticsService.CheckDatabaseConnection();
                ViewBag.DatabaseStatus = dbStatus;
                
                if (!dbStatus)
                {
                    Console.WriteLine("Falha na conexão com o banco de dados");
                    return View(new List<WavyStatus>());
                }

                Console.WriteLine("Obtendo status das WAVYs...");
                var statuses = await _analyticsService.GetWavyStatusesAsync();
                Console.WriteLine($"WAVYs encontradas: {statuses.Count}");
                
                foreach (var status in statuses)
                {
                    Console.WriteLine($"WAVY {status.Id}: {status.Status} (Última atualização: {status.LastUpdate})");
                }

                return View(statuses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no carregamento do Dashboard: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                ViewBag.DatabaseStatus = false;
                ViewBag.Error = ex.Message;
                return View(new List<WavyStatus>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckConnection()
        {
            try
            {
                var result = await _analyticsService.CheckDatabaseConnection();
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao verificar conexão: {ex.Message}");
                return Json(new { success = false, error = ex.Message });
            }
        }

        public async Task<IActionResult> WavyDetails(string wavyId)
        {
            var dataTypes = await _analyticsService.GetAvailableDataTypesAsync(wavyId);
            ViewBag.WavyId = wavyId;
            return View(dataTypes);
        }

        [HttpGet]
        public async Task<IActionResult> GetData(string wavyId, string dataType, DateTime? startTime, DateTime? endTime)
        {
            var start = startTime ?? DateTime.Now.AddHours(-1);
            var end = endTime ?? DateTime.Now;
            var data = await _analyticsService.GetDataPointsFlexibleAsync(wavyId, dataType, start, end);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAnalysis(string wavyId, string dataType, DateTime? startTime, DateTime? endTime)
        {
            if (string.IsNullOrEmpty(wavyId) || string.IsNullOrEmpty(dataType))
                return BadRequest();

            var start = startTime ?? DateTime.Now.AddHours(-1);
            var end = endTime ?? DateTime.Now;

            var analysis = await _analyticsService.AnalyzeDataAsync(wavyId, dataType, start, end);
            return Json(analysis);
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            var statuses = await _analyticsService.GetWavyStatusesAsync();
            return Json(statuses);
        }

        [HttpGet]
        public async Task<IActionResult> GetWavyIds()
        {
            var ids = await _analyticsService.GetAvailableWavyIdsAsync();
            return Json(ids);
        }

        [HttpGet]
        public async Task<IActionResult> GetDataTypes(string wavyId)
        {
            if (string.IsNullOrEmpty(wavyId))
                return BadRequest();

            var dataTypes = await _analyticsService.GetAvailableDataTypesAsync(wavyId);
            return Json(dataTypes);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDataTypes()
        {
            var dataTypes = await _analyticsService.GetAllAvailableDataTypesAsync();
            return Json(dataTypes);
        }

        [HttpGet]
        public async Task<IActionResult> GetStatusSummary(DateTime? startTime, DateTime? endTime)
        {
            var result = await _analyticsService.GetStatusCountsAsync(startTime, endTime);
            return Json(result.Select(x => new { status = x.Value, count = x.Count }));
        }

        [HttpGet]
        public async Task<IActionResult> GetConfigSummary(DateTime? startTime, DateTime? endTime)
        {
            var result = await _analyticsService.GetConfigCountsAsync(startTime, endTime);
            return Json(result.Select(x => new { config = x.Value, count = x.Count }));
        }

        [HttpGet]
        public async Task<IActionResult> GetStatusHistory(string wavyId, int limit = 100)
        {
            var result = await _analyticsService.GetStatusHistoryAsync(wavyId, limit);
            return Json(result.Select(x => new { timestamp = x.Timestamp, value = x.Value }));
        }

        [HttpGet]
        public async Task<IActionResult> GetConfigHistory(string wavyId, int limit = 100)
        {
            var result = await _analyticsService.GetConfigHistoryAsync(wavyId, limit);
            return Json(result.Select(x => new { timestamp = x.Timestamp, value = x.Value }));
        }

        [HttpGet]
        public async Task<IActionResult> GetRealTimeStats(string? sensorType = null)
        {
            try
            {
                var stats = await _analyticsService.GetRealTimeStatsAsync(sensorType);
                return Json(stats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter estatísticas em tempo real: {ex.Message}");
                return Json(new { error = ex.Message });
            }
        }
    }
} 