using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Servidor.Models
{
    public class WavyData
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("wavyId")]
        public string WavyId { get; set; } = string.Empty;

        [BsonElement("dataType")]
        public string DataType { get; set; } = string.Empty;

        [BsonElement("value")]
        public string Value { get; set; } = string.Empty;

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }

        [BsonElement("processedValue")]
        public string ProcessedValue { get; set; } = string.Empty;

        [BsonElement("analysisResults")]
        public Dictionary<string, string> AnalysisResults { get; set; }

        public WavyData()
        {
            AnalysisResults = new Dictionary<string, string>();
        }
    }
} 