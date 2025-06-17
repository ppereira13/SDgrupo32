using System;
using System.Collections.Generic;

namespace PreProcessamentoRPC
{
    public class PreProcessamentoResposta
    {
        public required string WavyId { get; set; }
        public required string TipoDado { get; set; }
        public required List<RateUniformizer.UniformData> DadosProcessados { get; set; }
        public required DateTime Timestamp { get; set; }
        public required MetaDados MetaDados { get; set; }
    }

    public class MetaDados
    {
        public required string FormatoOriginal { get; set; }
        public required double TaxaAmostragermOriginal { get; set; }
        public required double TaxaAmostragermUniforme { get; set; }
        public required DateTime ProcessamentoTimestamp { get; set; }
    }
} 