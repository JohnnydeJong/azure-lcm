﻿

using System.Reflection;
using System.Text.Json;

namespace AzLcm.Shared.Storage
{
    public class WorkItemTemplateStorage(JsonSerializerOptions jsonSerializerOptions)
    {
        private async Task<string> GetTemplateTextAsync(string resourceName, CancellationToken stoppingToken)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);                
                string fileContents = await reader.ReadToEndAsync(stoppingToken);
                return fileContents;
            }
            throw new InvalidOperationException($"Resource {resourceName} not found");
        }

        public async Task<WorkItemTemplate?> GetFeedWorkItemTemplateAsync(CancellationToken stoppingToken)
        {
            var resourceName = $"{typeof(WorkItemTemplateStorage).Namespace}.FeedWorkItemTemplate.json";
            var templateText = await GetTemplateTextAsync(resourceName, stoppingToken);
            var template = JsonSerializer.Deserialize<WorkItemTemplate>(templateText, jsonSerializerOptions);
            return template;
        }

        public async Task<WorkItemTemplate?> GetPolicyWorkItemTemplateAsync(CancellationToken stoppingToken)
        {
            var resourceName = $"{typeof(WorkItemTemplateStorage).Namespace}.PolicyWorkItemTemplate.json";
            var templateText = await GetTemplateTextAsync(resourceName, stoppingToken);
            var template = JsonSerializer.Deserialize<WorkItemTemplate>(templateText, jsonSerializerOptions);
            return template;
        }
    }

    public class WorkItemTemplate
    {
        public string? ProjectId { get; set; }
        public string? Type { get; set; }
        public List<PatchFragment>? Fields { get; set; }
    }

    public class PatchFragment
    {
        public string? Op { get; set; }
        public string? Path { get; set; }
        public string? From { get; set; }
        public string? Value { get; set; }
    }
}