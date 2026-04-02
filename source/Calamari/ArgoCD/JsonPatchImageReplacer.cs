#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD
{
    public class JsonPatchImageReplacer : IContainerImageReplacer
    {
        private static class JsonProcessingConstants
        {
            public const int RegexTimeoutMs = 100;
            public const string ContainersFieldName = "containers";
            public const string InitContainersFieldName = "initContainers";
            public const string ImageFieldName = "image";
            public const string ValueFieldName = "value";

            public static readonly JsonSerializerOptions DefaultSerializerOptions = new()
            {
                PropertyNamingPolicy = null,
                WriteIndented = true
            };

            public static readonly JsonSerializerOptions CompactSerializerOptions = new()
            {
                PropertyNamingPolicy = null,
                WriteIndented = false
            };
        }

        readonly string jsonContent;
        readonly string defaultRegistry;
        readonly ILog log;
        readonly Regex imageReferencePattern;

        static readonly Regex DefaultImageReferencePattern = new Regex(
            @"""(?<image>[a-zA-Z0-9\.\-_/]+(?::[a-zA-Z0-9\.\-_]+)?(?:@sha256:[a-fA-F0-9]{64})?)""",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromMilliseconds(JsonProcessingConstants.RegexTimeoutMs));

        public JsonPatchImageReplacer(string jsonContent, string defaultRegistry, ILog log)
            : this(jsonContent, defaultRegistry, log, DefaultImageReferencePattern)
        {
        }

        JsonPatchImageReplacer(string jsonContent, string defaultRegistry, ILog log, Regex imagePattern)
        {
            this.jsonContent = jsonContent;
            this.defaultRegistry = defaultRegistry;
            this.log = log;
            this.imageReferencePattern = imagePattern;
        }

        ImageReplacementResult NoChangeResult => new ImageReplacementResult(jsonContent, new HashSet<string>(), new HashSet<string>());

        public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                log.Warn("JSON patch file content is empty or whitespace only.");
                return NoChangeResult;
            }

            try
            {
                JsonArray? patchArray;
                try
                {
                    patchArray = JsonSerializer.Deserialize<JsonArray>(jsonContent);
                }
                catch (JsonException)
                {
                    log.Warn("JSON patch file content is not valid JSON.");
                    return NoChangeResult;
                }

                if (patchArray == null)
                {
                    log.Warn("JSON patch file does not contain a valid JSON array.");
                    return NoChangeResult;
                }

                var replacementsMade = new HashSet<string>();
                var hasChanges = false;

                foreach (var patchOp in patchArray)
                {
                    if (patchOp is JsonObject patchObject)
                    {
                        var changes = ProcessPatchOperation(patchObject, imagesToUpdate);
                        replacementsMade.UnionWith(changes);
                        if (changes.Count > 0)
                        {
                            hasChanges = true;
                        }
                    }
                }

                if (!hasChanges)
                {
                    return NoChangeResult;
                }

                var options = jsonContent.Contains('\n')
                    ? JsonProcessingConstants.DefaultSerializerOptions
                    : JsonProcessingConstants.CompactSerializerOptions;

                var modifiedJson = JsonSerializer.Serialize(patchArray, options);
                return new ImageReplacementResult(modifiedJson, replacementsMade, new HashSet<string>());
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error processing JSON patch file: {0}", ex.Message);
                return NoChangeResult;
            }
        }

        HashSet<string> ProcessPatchOperation(JsonObject patchOperation, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var changes = new HashSet<string>();

            if (patchOperation.TryGetPropertyValue(JsonProcessingConstants.ValueFieldName, out var valueNode))
            {
                ProcessValueNode(valueNode, imagesToUpdate, changes);
            }

            return changes;
        }

        void ProcessValueNode(JsonNode? valueNode, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            switch (valueNode)
            {
                case JsonObject obj:
                    ProcessJsonObject(obj, imagesToUpdate, changes);
                    break;
                case JsonArray array:
                    ProcessJsonArray(array, imagesToUpdate, changes);
                    break;
                case JsonValue value:
                    ProcessJsonValue(value, imagesToUpdate, changes);
                    break;
            }
        }

        void ProcessJsonObject(JsonObject obj, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            if (obj.TryGetPropertyValue(JsonProcessingConstants.ImageFieldName, out var imageNode) && imageNode is JsonValue imageValue)
            {
                ProcessImageValue(imageValue, imagesToUpdate, changes);
            }

            if (obj.TryGetPropertyValue(JsonProcessingConstants.ContainersFieldName, out var containersNode) && containersNode is JsonArray containersArray)
            {
                ProcessJsonArray(containersArray, imagesToUpdate, changes);
            }

            if (obj.TryGetPropertyValue(JsonProcessingConstants.InitContainersFieldName, out var initContainersNode) && initContainersNode is JsonArray initContainersArray)
            {
                ProcessJsonArray(initContainersArray, imagesToUpdate, changes);
            }

            foreach (var kvp in obj.ToList())
            {
                ProcessValueNode(kvp.Value, imagesToUpdate, changes);
            }
        }

        void ProcessJsonArray(JsonArray array, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            foreach (var item in array)
            {
                ProcessValueNode(item, imagesToUpdate, changes);
            }
        }

        void ProcessJsonValue(JsonValue value, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            if (value.TryGetValue<string>(out var stringValue) && !string.IsNullOrEmpty(stringValue))
            {
                if (IsImageReference(stringValue))
                {
                    ProcessImageValue(value, imagesToUpdate, changes);
                }
            }
        }

        void ProcessImageValue(JsonValue imageValue, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            if (!imageValue.TryGetValue<string>(out var currentImageString) || string.IsNullOrEmpty(currentImageString))
                return;

            var matchedUpdate = FindMatchingImageUpdate(currentImageString, imagesToUpdate);
            if (matchedUpdate == null || matchedUpdate.Comparison.TagMatch)
                return;

            var newImageRef = matchedUpdate.Reference.WithTag(matchedUpdate.Reference.Tag);
            UpdateJsonImageValue(imageValue, newImageRef);

            changes.Add($"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}");
            log.Verbose($"Updated container image in JSON patch: {newImageRef}");
        }

        ImageReferenceMatch? FindMatchingImageUpdate(string currentImageString, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var currentImageRef = ContainerImageReference.FromReferenceString(currentImageString, defaultRegistry);
            return imagesToUpdate
                .Select(i => new ImageReferenceMatch(i.ContainerReference, i.ContainerReference.CompareWith(currentImageRef)))
                .FirstOrDefault(i => i.Comparison.MatchesImage());
        }

        void UpdateJsonImageValue(JsonValue imageValue, string newImageRef)
        {
            var parentArray = imageValue.Parent as JsonArray;
            var parentObject = imageValue.Parent as JsonObject;

            if (parentArray != null)
            {
                UpdateImageInJsonArray(parentArray, imageValue, newImageRef);
            }
            else if (parentObject != null)
            {
                UpdateImageInJsonObject(parentObject, imageValue, newImageRef);
            }
        }

        void UpdateImageInJsonArray(JsonArray parentArray, JsonValue imageValue, string newImageRef)
        {
            var index = parentArray.IndexOf(imageValue);
            if (index >= 0)
            {
                parentArray[index] = JsonValue.Create(newImageRef);
            }
        }

        void UpdateImageInJsonObject(JsonObject parentObject, JsonValue imageValue, string newImageRef)
        {
            var property = parentObject.FirstOrDefault(kvp => ReferenceEquals(kvp.Value, imageValue));
            if (property.Key != null)
            {
                parentObject[property.Key] = JsonValue.Create(newImageRef);
            }
        }

        static bool IsImageReference(string value)
        {
            return value.Contains(':') || value.Contains('/') || value.Contains('.');
        }

        record ImageReferenceMatch(ContainerImageReference Reference, ContainerImageComparison Comparison);
    }
}