﻿


using AzLcm.Shared.Cognition.Models;
using AzLcm.Shared.PageScrapping;
using AzLcm.Shared.Storage;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.Json;

namespace AzLcm.Shared.Cognition
{
    public class CognitiveService(
        JsonSerializerOptions jsonSerializerOptions,
        ILogger<CognitiveService> logger,
        DaemonConfig daemonConfig,
        OpenAIClient openAIClient)
    {
        private ChatCompletionsOptions GetChatCompletionsOptions(float temperature = (float)1) => new()
        {
            DeploymentName = daemonConfig.AzureOpenAIGPTDeploymentId,
            ChoiceCount = 1,            
            MaxTokens = 4000,
            FrequencyPenalty = (float)0,
            PresencePenalty = (float)0,
            Temperature = (float)temperature
        };

        public async Task<Verdict?> AnalyzeV2Async(
            SyndicationItem feedItem,  string promptTemplate, CancellationToken stoppingToken)
        {
            ArgumentNullException.ThrowIfNull(nameof(feedItem));

            var thread = GetChatCompletionsOptions((float)0.7);

            thread.Messages.Add(new ChatRequestSystemMessage(promptTemplate));

            var tags = feedItem.Categories.Select(c => c.Name).ToList();

            var feedDetails = new StringBuilder();
            feedDetails.AppendLine($"<Update info BEGIN>");
            feedDetails.AppendLine($"Title: {feedItem.Title.Text}");
            feedDetails.AppendLine($"Summary: {feedItem.Summary.Text}");
            feedDetails.AppendLine($"Tags: {string.Join(", ", tags)}");
            feedDetails.AppendLine($"</Update info END>");
            thread.Messages.Add(new ChatRequestUserMessage(feedDetails.ToString()));

            try
            {
                var response = await openAIClient.GetChatCompletionsAsync(thread, stoppingToken);
                var rawContent = response.Value.Choices[0].Message.Content;
                var verdict = Verdict.FromJson(rawContent, logger, jsonSerializerOptions);
                return verdict;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, message: "");
            }
            return null;
        }

        public async Task<AreaPathMapResponse?> MapServiceToAreaPathAsync(
            string serviceName,
            AreaPathServiceMapConfig areaPathServiceMapConfig, CancellationToken stoppingToken)
        {
            ArgumentNullException.ThrowIfNull(nameof(areaPathServiceMapConfig));

            var thread = GetChatCompletionsOptions((float)1);
            thread.Messages.Add(new ChatRequestSystemMessage(
"""
You have a map of services to area paths. Your task is to map the service name to the area path.
"""));
            
            var mapInfoBuilder = new StringBuilder();
            foreach(var mapItem in areaPathServiceMapConfig.Map)
            {
                mapInfoBuilder.AppendLine($"[");
                mapInfoBuilder.AppendLine($" Service: {string.Join(", ", mapItem.Services)}");
                mapInfoBuilder.AppendLine($" AreaPath: {mapItem.RouteToAreaPath}");
                mapInfoBuilder.AppendLine($"] {Environment.NewLine}");
            }
            thread.Messages.Add(new ChatRequestSystemMessage(mapInfoBuilder.ToString()));

            thread.Messages.Add(new ChatRequestSystemMessage(
"""
IMPORTANT: You MUST response with JSON object with following schema:

export type MapResponse {
    areaPath: string;
}

Example1: If user provide a service name "Azure SQL" and it matches to an area path '/area-path/demo'
Response should be:
```
    { areaPath: "/area-path/demo", matched: true }
```

Example2: If user provide a service name "Azure X-Service" and there is no match found
Response should be:
```
    { areaPath: "", matched: false }
```
"""));
            thread.Messages.Add(new ChatRequestSystemMessage("The response MUST contain ONLY valid JSON object like examples, no additional explanation or text!!"));

            thread.Messages.Add(new ChatRequestUserMessage($"Service Name: {serviceName}"));

            var rawContent = string.Empty;
            try
            {
                var response = await openAIClient.GetChatCompletionsAsync(thread, stoppingToken);
                rawContent = response.Value.Choices[0].Message.Content;
                var mapResponse = AreaPathMapResponse.FromJson(rawContent, jsonSerializerOptions);

                var validAreaPaths = areaPathServiceMapConfig.Map.Select(m => m.RouteToAreaPath).ToList();
                validAreaPaths.Add(areaPathServiceMapConfig.DefaultAreaPath);

                if (mapResponse != null)
                {
                    if (!validAreaPaths.Contains($"{mapResponse.AreaPath}"))
                    {
                        return new AreaPathMapResponse { AreaPath = string.Empty };
                    }
                    else 
                    {
                        return mapResponse;
                    }                    
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, message: $"Raw: {rawContent}");
            }
            return null;
        }
    }
}

