using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WavyDashboard.Models
{
    [BsonIgnoreExtraElements]
    public class WavyDataPoint
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }

        [BsonElement("value")]
        public string Value { get; set; }

        [BsonElement("wavyId")]
        public string WavyId { get; set; }

        [BsonElement("dataType")]
        public string DataType { get; set; }

        public WavyDataPoint()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }
    }
} 