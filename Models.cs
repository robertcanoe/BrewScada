using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BrewScada
{
    public class Ingrediente
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; } // Cambiamos a ObjectId

        public string Nombre { get; set; }
        public decimal Cantidad { get; set; }
        public decimal UmbralMinimo { get; set; }
    }

    public class Produccion
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string BatchName { get; set; }
        public string Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class Counter
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
    }
}