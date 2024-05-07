﻿using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using VectorSearchAiAssistant.Common.Exceptions;
using VectorSearchAiAssistant.Common.Extensions;
using VectorSearchAiAssistant.Common.Interfaces;
using VectorSearchAiAssistant.Common.Models;
using VectorSearchAiAssistant.Common.Models.BusinessDomain;
using VectorSearchAiAssistant.Common.Text;

namespace VectorSearchAiAssistant.Common.Services
{
    public class ModelRegistryItemTransformer : IItemTransformer
    {
        private readonly JObject? _jObjectItem;
        private readonly ModelRegistryEntry _typeMetadata;
        private readonly object _typedItem;
        private readonly string _itemName;
        private readonly JObject? _objectToEmbed;
        private readonly string? _textToEmbed;
        private readonly string? _embeddingId;

        private readonly bool _isEmbeddedEntity;

        private static readonly SHA1 _hash = SHA1.Create();

        public ModelRegistryItemTransformer(object item)
        {
            if (item is JObject jObject)
            {
                _typeMetadata = ModelRegistry.IdentifyType(jObject)
                    ?? throw new ItemTransformerException($"The Model Registry could not identify the type {item.GetType()}.");

                _jObjectItem = jObject;
                _typedItem = _jObjectItem.ToObject(_typeMetadata.Type!)!;
            }
            else
            {
                _typeMetadata = ModelRegistry.IdentifyType(item)
                    ?? throw new ItemTransformerException($"The Model Registry could not identify the type {item.GetType()}.");
                _typedItem = item;
            }

            _itemName = string.Join(" ", _typedItem.GetPropertyValues(_typeMetadata.NamingProperties!));

            if (_typedItem is EmbeddedEntity entity)
            {
                _isEmbeddedEntity = true;
                entity.entityType__ = _typeMetadata.Type!.Name;

                var transformedItem = EmbeddingUtility.Transform(_typedItem);
                _objectToEmbed = transformedItem.ObjectToEmbed;
                _textToEmbed = transformedItem.TextToEmbed;

                _embeddingId = (_jObjectItem != null && _jObjectItem.ContainsKey("id"))
                    ? _jObjectItem.Value<string>("id")
                    : GetHash(_textToEmbed);
            }
        }

        public string EmbeddingId =>
            _isEmbeddedEntity
                ? _embeddingId!
                : throw new ItemTransformerException("Only EmbeddedEntity objects can have an embedding identifier.");

        public string Name =>
            _itemName;

        public object TypedValue =>
            _typedItem;

        public JObject ObjectToEmbed =>
            _isEmbeddedEntity
                ? _objectToEmbed!
                : throw new ItemTransformerException("Only EmbeddedEntity objects can be embedded.");

        public string TextToEmbed =>
            _isEmbeddedEntity
                ? _textToEmbed!
                : throw new ItemTransformerException("Only EmbeddedEntity objects can be embedded.");

        private static string GetHash(string s)
        {
            return Convert.ToBase64String(
                _hash.ComputeHash(
                    Encoding.UTF8.GetBytes(s)));
        }
    }
}
