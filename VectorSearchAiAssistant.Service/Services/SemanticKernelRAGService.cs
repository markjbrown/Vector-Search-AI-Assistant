﻿using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System.Text.RegularExpressions;
using VectorSearchAiAssistant.Common.Interfaces;
using VectorSearchAiAssistant.Common.Models;
using VectorSearchAiAssistant.Common.Models.BusinessDomain;
using VectorSearchAiAssistant.Common.Models.Chat;
using VectorSearchAiAssistant.SemanticKernel.Connectors.AzureCosmosDBNoSql;
using VectorSearchAiAssistant.SemanticKernel.Plugins.Core;
using VectorSearchAiAssistant.SemanticKernel.Plugins.Memory;
using VectorSearchAiAssistant.Service.Interfaces;
using VectorSearchAiAssistant.Service.Models.ConfigurationOptions;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0050, SKEXP0060

namespace VectorSearchAiAssistant.Service.Services;

public class SemanticKernelRAGService : IRAGService
{
    readonly SemanticKernelRAGServiceSettings _settings;
    readonly Kernel _semanticKernel;
    readonly IEnumerable<IMemorySource> _memorySources;
    readonly ILogger<SemanticKernelRAGService> _logger;
    readonly ISystemPromptService _systemPromptService;
    readonly ICosmosDBVectorStoreService _cosmosDBVectorStoreService;
    readonly VectorMemoryStore _longTermMemory;
    readonly VectorMemoryStore _shortTermMemory;
    readonly VectorMemoryStore _semanticMemory;

    readonly IItemTransformerFactory _itemTransformerFactory;

    readonly string _shortTermCollectionName = "short-term";
    readonly string _semanticMemoryCollectionName = "semantic-memory";

    bool _serviceInitialized = false;
    bool _shortTermMemoryInitialized = false;
    bool _semanticMemoryInitialized = false;

    string _prompt = string.Empty;

    public bool IsInitialized => _serviceInitialized;

    public SemanticKernelRAGService(
        IItemTransformerFactory itemTransformerFactory,
        ISystemPromptService systemPromptService,
        IEnumerable<IMemorySource> memorySources,
        ICosmosDBVectorStoreService cosmosDBVectorStoreService,
        IOptions<SemanticKernelRAGServiceSettings> options,
        ILogger<SemanticKernelRAGService> logger,
        ILoggerFactory loggerFactory)
    {
        _itemTransformerFactory = itemTransformerFactory;
        _systemPromptService = systemPromptService;
        _cosmosDBVectorStoreService = cosmosDBVectorStoreService;
        _memorySources = memorySources;
        _settings = options.Value;
        _logger = logger;

        _logger.LogInformation("Initializing the Semantic Kernel RAG service...");

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);

        builder.AddAzureOpenAITextEmbeddingGeneration(
            _settings.OpenAI.EmbeddingsDeployment,
            _settings.OpenAI.Endpoint,
            _settings.OpenAI.Key);

        builder.AddAzureOpenAIChatCompletion(
            _settings.OpenAI.CompletionsDeployment,
            _settings.OpenAI.Endpoint,
            _settings.OpenAI.Key);

        _semanticKernel = builder.Build();

        // The long-term memory uses an Azure Cosmos DB NoSQL memory store
        _longTermMemory = new VectorMemoryStore(
            _settings.CosmosDBVectorStore.MainIndexName,
            new AzureCosmosDBNoSqlMemoryStore(_cosmosDBVectorStoreService),
            _semanticKernel.Services.GetRequiredService<ITextEmbeddingGenerationService>()!,
            loggerFactory.CreateLogger<VectorMemoryStore>());

        _shortTermMemory = new VectorMemoryStore(
            _shortTermCollectionName,
            new VolatileMemoryStore(),
            _semanticKernel.Services.GetRequiredService<ITextEmbeddingGenerationService>()!,
            loggerFactory.CreateLogger<VectorMemoryStore>());

        _semanticMemory = new VectorMemoryStore(
            _semanticMemoryCollectionName,
            new VolatileMemoryStore(),
            _semanticKernel.Services.GetRequiredService<ITextEmbeddingGenerationService>()!,
            loggerFactory.CreateLogger<VectorMemoryStore>());

        Task.Run(Initialize);
    }

    private async Task Initialize()
    {
        await _longTermMemory.EnsureMemoryStoreCollectionExists(_settings.CosmosDBVectorStore.MainIndexName);
        await _longTermMemory.EnsureMemoryStoreCollectionExists(_settings.CosmosDBVectorStore.SemanticCacheIndexName);

        _prompt = await _systemPromptService.GetPrompt(_settings.OpenAI.ChatCompletionPromptName);

        var kmContextPlugin = new KnowledgeManagementContextPlugin(
            _longTermMemory,
            _shortTermMemory,
            _prompt,
            _settings.AISearch,
            _settings.OpenAI,
            _logger);

        _semanticKernel.ImportPluginFromObject(kmContextPlugin);

        _serviceInitialized = true;

        _logger.LogInformation("Semantic Kernel RAG service initialized.");
    }

    private async Task EnsureShortTermMemory()
    {
        try
        {
            if (_shortTermMemoryInitialized)
                return;

            // The memories collection in the short term memory store must be created explicitly
            await _shortTermMemory.MemoryStore.CreateCollectionAsync(_shortTermCollectionName);

            // Get current short term memories. Short term memories are generated or loaded at runtime and kept in SK's volatile memory.
            //The memories (data) here were generated from ACSMemorySourceConfig.json in blob storage that was used to execute faceted queries in Cog Search to iterate through
            //each product category stored and count up the number of products in each category. The query also counts all the products for the entire company.
            //The content here has embeddings generated on it so it can be used in a vector query by the user

            // TODO: Explore the option of moving static memories loaded from blob storage into the long-term memory (e.g., the Azure Cognitive Search index).
            // For now, the static memories are re-loaded each time together with the analytical short-term memories originating from Azure Cognitive Search faceted queries.
            var shortTermMemories = new List<string>();
            foreach (var memorySource in _memorySources)
            {
                shortTermMemories.AddRange(await memorySource.GetMemories());
            }

            foreach (var itemTransformer in shortTermMemories
                .Select(m => _itemTransformerFactory.CreateItemTransformer(new ShortTermMemory
                {
                    entityType__ = nameof(ShortTermMemory),
                    memory__ = m
                })))
            {
                await _shortTermMemory.AddMemory(itemTransformer);
            }

            _shortTermMemoryInitialized = true;
            _logger.LogInformation("Semantic Kernel RAG service short-term memory initialized.");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The Semantic Kernel RAG service short-term memory failed to initialize.");
        }
    }

    private async Task EnsureSemanticMemory()
    {
        try
        {
            if (_semanticMemoryInitialized)
                return;

            // The memories collection in the short term memory store must be created explicitly
            await _semanticMemory.MemoryStore.CreateCollectionAsync(_semanticMemoryCollectionName);

            // TODO: Load the semantic memory from Cosmos DB.

            _semanticMemoryInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The semantic memory failed to initialize.");
        }
    }

    public async Task<(string Completion, string UserPrompt, int UserPromptTokens, int ResponseTokens, float[]? UserPromptEmbedding)> GetResponse(string userPrompt, List<Message> messageHistory)
    {
        // First, get the user prompt embedding since we will need to use it in multiple places
        await EnsureSemanticMemory();
        var userPromptEmbedding = await _semanticKernel.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingAsync(userPrompt);

        // Check if a very similar question has been asked in the recent past
        var semanticMemoryHits = await _semanticMemory
            .GetNearestMatches(userPromptEmbedding, 1, 0.95f)
            .ToListAsync()
            .ConfigureAwait(false);

        if (semanticMemoryHits.Count > 0)
        {
            var cachedResponse = semanticMemoryHits[0].Metadata.AdditionalMetadata;
            return (cachedResponse, userPrompt, 0, 0, userPromptEmbedding.ToArray());
        }

        // The semantic memory was not able to provide a cached answer, so we start the normal response flow

        await EnsureShortTermMemory();

        // Use observability features to capture the fully rendered prompt.
        var promptFilter = new DefaultPromptFilter();
        _semanticKernel.PromptFilters.Add(promptFilter);
        
        var arguments = new KernelArguments()
        {
            ["userPrompt"] = userPrompt,
            ["messageHistory"] = messageHistory
        };

        var result = await _semanticKernel.InvokePromptAsync(_prompt, arguments);

        var completion = result.GetValue<string>()!;
        var completionUsage = (result.Metadata!["Usage"] as CompletionsUsage)!;

        // Add the completion to the semantic memory
        await _semanticMemory.AddMemory(userPrompt, userPromptEmbedding, completion);

        return new(completion, promptFilter.RenderedPrompt, completionUsage!.PromptTokens, completionUsage.CompletionTokens, userPromptEmbedding.ToArray());
    }

    public async Task<string> Summarize(string sessionId, string userPrompt)
    {
        var summarizerPlugin = new TextSummaryPlugin(
            await _systemPromptService.GetPrompt(_settings.OpenAI.ShortSummaryPromptName),
            500,
            _semanticKernel);

        var updatedContext = await summarizerPlugin.SummarizeTextAsync(
            userPrompt);

        //Remove all non-alpha numeric characters (Turbo has a habit of putting things in quotes even when you tell it not to)
        var summary = Regex.Replace(updatedContext, @"[^a-zA-Z0-9.\s]", "");

        return summary;
    }

    public async Task AddMemory(IItemTransformer itemTransformer)
    {
        await _longTermMemory.AddMemory(itemTransformer);
    }

    public async Task RemoveMemory(object item)
    {
        await _longTermMemory.RemoveMemory(item);
    }
}
