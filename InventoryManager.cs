using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BrewScada
{
    public class InventoryManager
    {
        private IMongoCollection<Ingrediente> _ingredientesCollection;

        public InventoryManager(IMongoCollection<Ingrediente> ingredientesCollection)
        {
            _ingredientesCollection = ingredientesCollection;
        }

        public List<Ingrediente> GetAllIngredientes()
        {
            return _ingredientesCollection.Find(_ => true).ToList();
        }

        public void AddIngrediente(Ingrediente ingrediente)
        {
            // Si el Id es nulo, dejamos que MongoDB lo genere automáticamente
            if (ingrediente.Id == ObjectId.Empty)
            {
                ingrediente.Id = ObjectId.GenerateNewId(); // Generamos un nuevo ObjectId único
            }

            // Verificamos si ya existe un ingrediente con el mismo nombre
            var existingIngredient = _ingredientesCollection.Find(i => i.Nombre.ToLower() == ingrediente.Nombre.ToLower()).FirstOrDefault();
            if (existingIngredient == null)
            {
                _ingredientesCollection.InsertOne(ingrediente);
            }
            else
            {
                // Si ya existe, actualizamos la cantidad en lugar de insertar un duplicado
                UpdateIngredienteQuantity(ingrediente.Nombre, ingrediente.Cantidad);
            }
        }

        public void UpdateIngrediente(Ingrediente ingrediente)
        {
            var filter = Builders<Ingrediente>.Filter.Eq(i => i.Id, ingrediente.Id);
            _ingredientesCollection.ReplaceOne(filter, ingrediente);
        }

        public void UpdateIngredienteQuantity(string nombre, decimal nuevaCantidad)
        {
            var filter = Builders<Ingrediente>.Filter.Eq(i => i.Nombre.ToLower(), nombre.ToLower());
            var update = Builders<Ingrediente>.Update.Set(i => i.Cantidad, nuevaCantidad);
            _ingredientesCollection.UpdateOne(filter, update);
        }

        public void DeleteIngrediente(ObjectId id)
        {
            var filter = Builders<Ingrediente>.Filter.Eq(i => i.Id, id);
            _ingredientesCollection.DeleteOne(filter);
        }

        public void CheckInventoryLevels()
        {
            var ingredientes = GetAllIngredientes();
            foreach (var ingrediente in ingredientes)
            {
                if (ingrediente.Cantidad < ingrediente.UmbralMinimo)
                {
                    NotifyLowInventory(ingrediente);
                }
            }
        }

        private void NotifyLowInventory(Ingrediente ingrediente)
        {
            // Implementar notificación (correo electrónico, mensaje, etc.)
            Console.WriteLine($"Alerta: El inventario del ingrediente {ingrediente.Nombre} está por debajo del umbral mínimo.");
        }
    }
}