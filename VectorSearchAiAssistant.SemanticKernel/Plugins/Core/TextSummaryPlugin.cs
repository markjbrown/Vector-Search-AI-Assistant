﻿using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace VectorSearchAiAssistant.SemanticKernel.Plugins.Core
{
    public class TextSummaryPlugin
    {
        private readonly KernelFunction _summarizeConversation;
        private readonly Kernel _kernel;

        public TextSummaryPlugin(
            string promptTemplate,
            int maxTokens,
            Kernel kernel)
        {
            _kernel = kernel;
            _summarizeConversation = kernel.CreateFunctionFromPrompt(
                promptTemplate,
                functionName: nameof(TextSummaryPlugin),
                description: "Given a text, summarize the text.",
                executionSettings: new OpenAIPromptExecutionSettings
                {
                    MaxTokens = maxTokens,
                    Temperature = 0.1,
                    TopP = 0.5
                });
        }

        [KernelFunction]
        public async Task<string> SummarizeTextAsync(
            string text)
        {
            var result = await _kernel.InvokeAsync<string>(_summarizeConversation, new()
            {
                { "text", text }
            });
            return result ?? string.Empty;
        }
    }
}