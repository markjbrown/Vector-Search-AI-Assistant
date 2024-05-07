﻿using Newtonsoft.Json.Linq;

namespace VectorSearchAiAssistant.Common.Interfaces
{
    public interface IItemTransformer
    {
        object TypedValue { get; }

        string EmbeddingId { get; }

        string Name { get; }

        JObject ObjectToEmbed { get; }

        string TextToEmbed { get; }
    }
}
