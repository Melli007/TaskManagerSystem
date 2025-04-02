using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class FileUploadOperation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check if any parameter is IFormFile
        var hasFileParam = context.MethodInfo.GetParameters()
            .Any(p => p.ParameterType == typeof(IFormFile));

        if (!hasFileParam) return;

        // Clear existing parameter definitions
        operation.Parameters.Clear();

        // For tasks or messages, you'll want to define multi-part form.
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["File"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            },
                            // You can add your other fields (like "SenderId", "RecipientId") or "tasksCreate"
                            // For a more advanced setup, you might do a check on the method name
                        },
                        Required = new HashSet<string> { "file" }
                    }
                }
            }
        };
    }
}
