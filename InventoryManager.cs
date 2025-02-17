using System;
using System.Collections.Generic;
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
            _ingredientesCollection.InsertOne(ingrediente);
        }

        public void UpdateIngrediente(Ingrediente ingrediente)
        {
            var filter = Builders<Ingrediente>.Filter.Eq(i => i.Id, ingrediente.Id);
            _ingredientesCollection.ReplaceOne(filter, ingrediente);
        }

        public void DeleteIngrediente(string id)
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