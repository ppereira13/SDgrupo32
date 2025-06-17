using System;
using System.Collections.Generic;

namespace WavyDashboard.Models
{
    public class WavyAnalysis
    {
        public string WavyId { get; set; }
        public string DataType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Mean { get; set; }
        public double Median { get; set; }
        public double StdDev { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Trend { get; set; }
        public double Correlation { get; set; }
        public double SeasonalityStrength { get; set; }
        public int SeasonalityPeriod { get; set; }
        public int AnomalyCount { get; set; }
        public double AnomalySeverity { get; set; }
        public double DataQuality { get; set; }
        public double DataConsistency { get; set; }
        public Dictionary<string, double> Statistics { get; set; } = new Dictionary<string, double>();
    }
} 