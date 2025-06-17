using System;

namespace PreProcessamentoRPC
{
    public class PreProcessamentoRequest
    {
        public required string WavyId { get; set; }
        public required string TipoDado { get; set; }
        public required string Valor { get; set; }
        public required FormatConverter.DataFormat FormatoOrigem { get; set; }
        public bool RequerAnaliseAvancada { get; set; }
    }
} 