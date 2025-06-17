using System;
using System.Collections.Generic;
using System.Xml;
using System.Text.Json;
using System.Linq;
using System.IO;
using System.Text;

namespace PreProcessamentoRPC
{
    public class FormatConverter
    {
        public enum DataFormat
        {
            Text,
            CSV,
            XML,
            JSON
        }

        public class DadoPadronizado
        {
            public string WavyId { get; set; }
            public string TipoDado { get; set; }
            public string Valor { get; set; }
            public DateTime Timestamp { get; set; }
            public Dictionary<string, object> MetaDados { get; set; }
        }

        public string ConverterParaPadrao(string dados, string formatoOrigem)
        {
            var formato = Enum.Parse<DataFormat>(formatoOrigem, true);
            var dadosDict = ParseToIntermediate(dados, formato);
            
            var dadoPadronizado = new DadoPadronizado
            {
                WavyId = dadosDict.GetValueOrDefault("wavyId", "").ToString(),
                TipoDado = dadosDict.GetValueOrDefault("tipoDado", "").ToString(),
                Valor = dadosDict.GetValueOrDefault("valor", "").ToString(),
                Timestamp = DateTime.Parse(dadosDict.GetValueOrDefault("timestamp", DateTime.UtcNow.ToString()).ToString()),
                MetaDados = new Dictionary<string, object>()
            };

            // Adiciona quaisquer campos extras como metadados
            foreach (var item in dadosDict)
            {
                if (!new[] { "wavyId", "tipoDado", "valor", "timestamp" }.Contains(item.Key))
                {
                    dadoPadronizado.MetaDados[item.Key] = item.Value;
                }
            }

            return JsonSerializer.Serialize(dadoPadronizado);
        }

        public void ValidarDado(string dadoJson)
        {
            try
            {
                var dado = JsonSerializer.Deserialize<DadoPadronizado>(dadoJson);
                
                if (string.IsNullOrEmpty(dado.WavyId))
                    throw new ArgumentException("WavyId é obrigatório");
                
                if (string.IsNullOrEmpty(dado.TipoDado))
                    throw new ArgumentException("TipoDado é obrigatório");
                
                if (string.IsNullOrEmpty(dado.Valor))
                    throw new ArgumentException("Valor é obrigatório");
                
                if (dado.Timestamp == default)
                    throw new ArgumentException("Timestamp é obrigatório");
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Formato JSON inválido: {ex.Message}");
            }
        }

        public string ConverterParaFormato(DadoPadronizado dado, string formatoDestino)
        {
            var formato = Enum.Parse<DataFormat>(formatoDestino, true);
            
            var dadosDict = new Dictionary<string, object>
            {
                ["wavyId"] = dado.WavyId,
                ["tipoDado"] = dado.TipoDado,
                ["valor"] = dado.Valor,
                ["timestamp"] = dado.Timestamp
            };

            // Adiciona metadados
            if (dado.MetaDados != null)
            {
                foreach (var meta in dado.MetaDados)
                {
                    dadosDict[meta.Key] = meta.Value;
                }
            }

            return ConvertFromIntermediate(dadosDict, formato);
        }

        public string ConvertData(string input, DataFormat sourceFormat, DataFormat targetFormat)
        {
            // Primeiro converte para o formato intermediário (Dictionary)
            var data = ParseToIntermediate(input, sourceFormat);
            
            // Depois converte do formato intermediário para o formato alvo
            return ConvertFromIntermediate(data, targetFormat);
        }

        private Dictionary<string, object> ParseToIntermediate(string input, DataFormat format)
        {
            try
            {
                switch (format)
                {
                    case DataFormat.Text:
                        return ParseTextFormat(input);
                    case DataFormat.CSV:
                        return ParseCSVFormat(input);
                    case DataFormat.XML:
                        return ParseXMLFormat(input);
                    case DataFormat.JSON:
                        return ParseJSONFormat(input);
                    default:
                        throw new ArgumentException("Formato não suportado");
                }
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro ao converter do formato {format}: {ex.Message}");
            }
        }

        private string ConvertFromIntermediate(Dictionary<string, object> data, DataFormat targetFormat)
        {
            try
            {
                switch (targetFormat)
                {
                    case DataFormat.Text:
                        return ConvertToText(data);
                    case DataFormat.CSV:
                        return ConvertToCSV(data);
                    case DataFormat.XML:
                        return ConvertToXML(data);
                    case DataFormat.JSON:
                        return ConvertToJSON(data);
                    default:
                        throw new ArgumentException("Formato alvo não suportado");
                }
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro ao converter para o formato {targetFormat}: {ex.Message}");
            }
        }

        private Dictionary<string, object> ParseTextFormat(string input)
        {
            var result = new Dictionary<string, object>();
            var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    result[parts[0].Trim()] = parts[1].Trim();
                }
            }

            return result;
        }

        private Dictionary<string, object> ParseCSVFormat(string input)
        {
            var result = new Dictionary<string, object>();
            var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2) throw new FormatException("CSV inválido: necessário cabeçalho e dados");

            var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
            var values = lines[1].Split(',').Select(v => v.Trim()).ToArray();

            for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
            {
                result[headers[i]] = values[i];
            }

            return result;
        }

        private Dictionary<string, object> ParseXMLFormat(string input)
        {
            var result = new Dictionary<string, object>();
            var doc = new XmlDocument();
            doc.LoadXml(input);

            var root = doc.DocumentElement;
            if (root != null)
            {
                foreach (XmlNode node in root.ChildNodes)
                {
                    if (node.NodeType == XmlNodeType.Element)
                    {
                        result[node.Name] = node.InnerText;
                    }
                }
            }

            return result;
        }

        private Dictionary<string, object> ParseJSONFormat(string input)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(input);
        }

        private string ConvertToText(Dictionary<string, object> data)
        {
            var sb = new StringBuilder();
            foreach (var item in data)
            {
                sb.AppendLine($"{item.Key}={item.Value}");
            }
            return sb.ToString();
        }

        private string ConvertToCSV(Dictionary<string, object> data)
        {
            var sb = new StringBuilder();
            
            // Cabeçalho
            sb.AppendLine(string.Join(",", data.Keys));
            
            // Valores
            sb.AppendLine(string.Join(",", data.Values));
            
            return sb.ToString();
        }

        private string ConvertToXML(Dictionary<string, object> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<data>");
            
            foreach (var item in data)
            {
                sb.AppendLine($"  <{item.Key}>{item.Value}</{item.Key}>");
            }
            
            sb.AppendLine("</data>");
            return sb.ToString();
        }

        private string ConvertToJSON(Dictionary<string, object> data)
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }
    }
} 