using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Globalization;

namespace Servidor
{
    public class FormatConverter
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public FormatConverter()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public DadoPadronizado ConverterParaPadrao(string dados, string formatoOrigem)
        {
            try
            {
                return formatoOrigem.ToLower() switch
                {
                    "json" => ConverterJsonParaPadrao(dados),
                    "xml" => ConverterXmlParaPadrao(dados),
                    "csv" => ConverterCsvParaPadrao(dados),
                    "texto" => ConverterTextoParaPadrao(dados),
                    _ => throw new FormatException($"Formato não suportado: {formatoOrigem}")
                };
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro na conversão de {formatoOrigem}: {ex.Message}");
            }
        }

        public string ConverterParaFormato(DadoPadronizado dado, string formatoDestino)
        {
            try
            {
                return formatoDestino.ToLower() switch
                {
                    "json" => ConverterParaJson(dado),
                    "xml" => ConverterParaXml(dado),
                    "csv" => ConverterParaCsv(dado),
                    "texto" => ConverterParaTexto(dado),
                    _ => throw new FormatException($"Formato não suportado: {formatoDestino}")
                };
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro na conversão para {formatoDestino}: {ex.Message}");
            }
        }

        private DadoPadronizado ConverterJsonParaPadrao(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<DadoPadronizado>(json, _jsonOptions)
                    ?? throw new FormatException("JSON inválido ou vazio");
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Erro ao converter JSON: {ex.Message}");
            }
        }

        private DadoPadronizado ConverterXmlParaPadrao(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var root = doc.Root ?? throw new FormatException("XML inválido ou vazio");

                return new DadoPadronizado
                {
                    WavyId = root.Element("WavyId")?.Value ?? "",
                    TipoDado = root.Element("TipoDado")?.Value ?? "",
                    Valor = root.Element("Valor")?.Value ?? "",
                    Timestamp = DateTime.Parse(root.Element("Timestamp")?.Value ?? DateTime.UtcNow.ToString()),
                    MetaDados = root.Element("MetaDados")?
                        .Elements()
                        .ToDictionary(e => e.Name.LocalName, e => e.Value)
                };
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro ao converter XML: {ex.Message}");
            }
        }

        private DadoPadronizado ConverterCsvParaPadrao(string csv)
        {
            try
            {
                var linhas = csv.Split('\n');
                if (linhas.Length < 2)
                    throw new FormatException("CSV inválido: deve conter cabeçalho e dados");

                var cabecalho = linhas[0].Split(',');
                var valores = linhas[1].Split(',');

                if (cabecalho.Length != valores.Length)
                    throw new FormatException("CSV inválido: número de colunas inconsistente");

                var metaDados = new Dictionary<string, string>();
                for (int i = 4; i < cabecalho.Length; i++)
                {
                    metaDados[cabecalho[i].Trim()] = valores[i].Trim();
                }

                return new DadoPadronizado
                {
                    WavyId = valores[0].Trim(),
                    TipoDado = valores[1].Trim(),
                    Valor = valores[2].Trim(),
                    Timestamp = DateTime.Parse(valores[3].Trim()),
                    MetaDados = metaDados
                };
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro ao converter CSV: {ex.Message}");
            }
        }

        private DadoPadronizado ConverterTextoParaPadrao(string texto)
        {
            try
            {
                var partes = texto.Split('|');
                if (partes.Length < 4)
                    throw new FormatException("Texto inválido: formato esperado 'WavyId|TipoDado|Valor|Timestamp|[MetaDados]'");

                var metaDados = new Dictionary<string, string>();
                if (partes.Length > 4)
                {
                    var metaPartes = partes[4].Split(';');
                    foreach (var meta in metaPartes)
                    {
                        var keyValue = meta.Split('=');
                        if (keyValue.Length == 2)
                            metaDados[keyValue[0].Trim()] = keyValue[1].Trim();
                    }
                }

                return new DadoPadronizado
                {
                    WavyId = partes[0].Trim(),
                    TipoDado = partes[1].Trim(),
                    Valor = partes[2].Trim(),
                    Timestamp = DateTime.Parse(partes[3].Trim()),
                    MetaDados = metaDados
                };
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro ao converter texto: {ex.Message}");
            }
        }

        private string ConverterParaJson(DadoPadronizado dado)
        {
            return JsonSerializer.Serialize(dado, _jsonOptions);
        }

        private string ConverterParaXml(DadoPadronizado dado)
        {
            var doc = new XDocument(
                new XElement("Dado",
                    new XElement("WavyId", dado.WavyId),
                    new XElement("TipoDado", dado.TipoDado),
                    new XElement("Valor", dado.Valor),
                    new XElement("Timestamp", dado.Timestamp.ToString("o")),
                    new XElement("MetaDados",
                        dado.MetaDados?.Select(kv =>
                            new XElement(kv.Key, kv.Value))
                    )
                )
            );

            return doc.ToString();
        }

        private string ConverterParaCsv(DadoPadronizado dado)
        {
            var sb = new StringBuilder();

            // Cabeçalho
            sb.Append("WavyId,TipoDado,Valor,Timestamp");
            if (dado.MetaDados?.Any() == true)
            {
                foreach (var key in dado.MetaDados.Keys)
                {
                    sb.Append($",{key}");
                }
            }
            sb.AppendLine();

            // Valores
            sb.Append($"{dado.WavyId},{dado.TipoDado},{dado.Valor},{dado.Timestamp:o}");
            if (dado.MetaDados?.Any() == true)
            {
                foreach (var value in dado.MetaDados.Values)
                {
                    sb.Append($",{value}");
                }
            }

            return sb.ToString();
        }

        private string ConverterParaTexto(DadoPadronizado dado)
        {
            var texto = $"{dado.WavyId}|{dado.TipoDado}|{dado.Valor}|{dado.Timestamp:o}";
            
            if (dado.MetaDados?.Any() == true)
            {
                var metaDados = string.Join(";", 
                    dado.MetaDados.Select(kv => $"{kv.Key}={kv.Value}"));
                texto += $"|{metaDados}";
            }

            return texto;
        }

        public bool ValidarDado(DadoPadronizado dado)
        {
            if (string.IsNullOrWhiteSpace(dado.WavyId))
                throw new ValidationException("WavyId é obrigatório");

            if (string.IsNullOrWhiteSpace(dado.TipoDado))
                throw new ValidationException("TipoDado é obrigatório");

            if (string.IsNullOrWhiteSpace(dado.Valor))
                throw new ValidationException("Valor é obrigatório");

            // Validar formato do valor baseado no tipo de dado
            switch (dado.TipoDado.ToLower())
            {
                case "acel":
                case "gyro":
                    if (!double.TryParse(dado.Valor, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        throw new ValidationException($"Valor inválido para {dado.TipoDado}");
                    break;

                case "status":
                case "camera":
                    if (!int.TryParse(dado.Valor, out var val) || (val != 0 && val != 1))
                        throw new ValidationException($"Valor inválido para {dado.TipoDado}");
                    break;

                case "hidrofone":
                case "transdutor":
                    if (!int.TryParse(dado.Valor, out _))
                        throw new ValidationException($"Valor inválido para {dado.TipoDado}");
                    break;
            }

            return true;
        }
    }

    public class DadoPadronizado
    {
        public string WavyId { get; set; } = "";
        public string TipoDado { get; set; } = "";
        public string Valor { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string>? MetaDados { get; set; }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }
} 