using System;

namespace Servidor.Models
{
    public class WavyAnalysisRequest
    {
        public string WavyId { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string AnalysisType { get; set; } = string.Empty;
    }
} 