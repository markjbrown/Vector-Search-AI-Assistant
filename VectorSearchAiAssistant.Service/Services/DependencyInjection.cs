﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VectorSearchAiAssistant.Common.Interfaces;
using VectorSearchAiAssistant.Common.Models.Configuration;
using VectorSearchAiAssistant.Common.Services;
using VectorSearchAiAssistant.SemanticKernel.Models;
using VectorSearchAiAssistant.Service.Interfaces;
using VectorSearchAiAssistant.Service.MemorySource;
using VectorSearchAiAssistant.Service.Models.ConfigurationOptions;
using VectorSearchAiAssistant.Service.Services;
using VectorSearchAiAssistant.Service.Services.Text;

namespace VectorSearchAiAssistant
{
    /// <summary>
    /// General purpose dependency injection extensions.
    /// </summary>
    public static partial class DependencyInjection
    {
        /// <summary>
        /// Registers the <see cref="ICosmosDBService"/> implementation with the dependency injection container."/>
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddCosmosDBService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<CosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI:CosmosDB"));
            builder.Services.AddSingleton<ICosmosDBService, CosmosDBService>();
        }

        /// <summary>
        /// Registers the <see cref="IAISearchService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddAISearchService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<AISearchSettings>()
                .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI:AISearch"));
            builder.Services.AddSingleton<IAISearchService, AISearchService>();
        }

        /// <summary>
        /// Registers the <see cref="IRAGService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddSemanticKernelRAGService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<SemanticKernelRAGServiceSettings>()
                .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI"));
            builder.Services.AddSingleton<IRAGService, SemanticKernelRAGService>();
        }

        /// <summary>
        /// Registers the <see cref="IChatService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddChatService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddSingleton<IChatService, ChatService>();
        }

        /// <summary>
        /// Registers the <see cref="ICosmosDBVectorStoreService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddCosmosDBVectorStoreService (this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<CosmosDBVectorStoreServiceSettings>()
                .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI:CosmosDBVectorStore"));
            builder.Services.AddSingleton<ICosmosDBVectorStoreService, CosmosDBVectorStoreService>();
        }

        /// <summary>
        /// Registers the <see cref="ISystemPromptService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddPromptService(this IHostApplicationBuilder builder)
        {
            // System prompt service backed by an Azure blob storage account
            builder.Services.AddOptions<DurableSystemPromptServiceSettings>()
                .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI:DurableSystemPrompt"));
            builder.Services.AddSingleton<ISystemPromptService, DurableSystemPromptService>();
        }

        /// <summary>
        /// Registers the <see cref="IMemorySource"/> implementations with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddMemorySourceServices(this IHostApplicationBuilder builder)
        {
            //builder.Services.AddOptions<AzureAISearchMemorySourceSettings>()
            //    .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI:AISearchMemorySource"));
            //builder.Services.AddTransient<IMemorySource, AzureAISearchMemorySource>();

            builder.Services.AddOptions<BlobStorageMemorySourceSettings>()
                .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI:BlobStorageMemorySource"));
            builder.Services.AddTransient<IMemorySource, BlobStorageMemorySource>();
        }

        /// <summary>
        /// Registers the <see cref="ITextSplittingService"/> implementations with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddTextSplittingServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddSingleton<ITokenizerService, MicrosoftBPETokenizerService>();
            builder.Services.ActivateSingleton<ITokenizerService>();

            builder.Services.AddOptions<TokenTextSplitterServiceSettings>()
                .Bind(builder.Configuration.GetSection("MSCosmosDBOpenAI:TextSplitter"));
            builder.Services.AddSingleton<ITextSplitterService, TokenTextSplitterService>();
        }

        /// <summary>
        /// Registers the <see cref="IItemTransformerFactory"/> implementation with the dependency injection container."/>
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddItemTransformerFactory(this IHostApplicationBuilder builder) =>
            builder.Services.AddSingleton<IItemTransformerFactory, ItemTransformerFactory>();
    }
}
