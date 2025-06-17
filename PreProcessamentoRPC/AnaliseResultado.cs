using System;
using System.Collections.Generic;

namespace PreProcessamentoRPC
{
    public class AnaliseResultado
    {
        public string WavyId { get; set; }
        public string TipoDado { get; set; }
        public DateTime Timestamp { get; set; }
        public string Resultado { get; set; }
        public Dictionary<string, object> MetaDados { get; set; }
        public string Status { get; set; }
        public double Media { get; set; }
        public double Mediana { get; set; }
        public double DesvioPadrao { get; set; }
        public string Tendencia { get; set; }
        public List<double> Anomalias { get; set; }
        public Dictionary<string, object> AnaliseAvancada { get; set; }
    }
} 