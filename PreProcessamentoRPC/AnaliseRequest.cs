using System;

namespace PreProcessamentoRPC
{
    public class AnaliseRequest
    {
        public string WavyId { get; set; }
        public string TipoDado { get; set; }
        public string Valor { get; set; }
        public DateTime Timestamp { get; set; }
        public string TipoAnalise { get; set; }
        public Dictionary<string, object> Parametros { get; set; }
        public bool RequerAnaliseAvancada { get; set; }
    }
} 