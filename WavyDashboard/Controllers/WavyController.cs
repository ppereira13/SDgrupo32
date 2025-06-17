using Microsoft.AspNetCore.Mvc;
using WavyDashboard.Services;
using System;
using System.Threading.Tasks;

namespace WavyDashboard.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WavyController : ControllerBase
    {
        private readonly WavyAnalyticsService _analyticsService;

        public WavyController(WavyAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("realtime")]
        public async Task<IActionResult> GetRealtimeData()
        {
            try
            {
                var statuses = await _analyticsService.GetWavyStatusesAsync();
                var connectedWavys = statuses.Where(s => s.Status == "Conectada").ToList();

                var latestData = connectedWavys.FirstOrDefault();
                var currentValue = latestData != null ? 25.0 : 0.0; // Valor de exemplo

                return Ok(new
                {
                    connectedWavys = connectedWavys.Select(w => new
                    {
                        id = w.Id,
                        lastUpdate = w.LastUpdate
                    }),
                    currentValue
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("historical")]
        public async Task<IActionResult> GetHistoricalData([FromQuery] string type, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                var wavyId = "wavy1"; // ID de exemplo
                var data = await _analyticsService.GetDataPointsAsync(wavyId, type, startDate, endDate);

                var labels = data.Select(d => d.Timestamp.ToString("dd/MM HH:mm")).ToList();
                var values = data.Select(d => double.Parse(d.Value)).ToList();

                return Ok(new { labels, values });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
} 