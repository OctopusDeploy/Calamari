using System;
using System.IO;
using System.Text.Json;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Calamari.Azure
{
    /// <remarks>
    /// Based on: https://github.com/Azure/azure-sdk-for-net/issues/33384#issuecomment-1428080542
    /// </remarks>
    public class SlotConfigNamesInvalidIdFilterPolicy : HttpPipelineSynchronousPolicy
    {
        public override void OnReceivedResponse(HttpMessage message)
        {
            if (!message.Request.Uri.Path.EndsWith("slotconfignames", StringComparison.OrdinalIgnoreCase) || 
                message.Response.ContentStream == null) // Change here to get the expected response
            {
                return;
            }

            using var reader = new StreamReader(message.Response.ContentStream);
            // rewrite the null id with the request path which is a valid id
            var content = reader.ReadToEnd().Replace("\"id\":null", $"\"id\":\"{message.Request.Uri.Path}\""); // Assign a valid value to the property id
            //rewrite the response as JSON
            var jsonDocument = JsonDocument.Parse(content);
            var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream);
            jsonDocument.WriteTo(writer);
            writer.Flush();
            //reset the content stream result
            message.Response.ContentStream = stream;
            message.Response.ContentStream.Position = 0;
        }
    }
}