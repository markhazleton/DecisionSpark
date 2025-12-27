using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DecisionSpark.Swagger;

/// <summary>
/// Schema filter to handle object and List<object> types in Swagger documentation.
/// </summary>
public class ObjectTypeSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Handle List<object> properties
        if (context.Type.IsGenericType && context.Type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = context.Type.GetGenericArguments()[0];
            if (elementType == typeof(object))
            {
                schema.Type = "array";
                schema.Items = new OpenApiSchema
                {
                    Type = "object",
                    AdditionalPropertiesAllowed = true,
                    Description = "Dynamic object - can contain any JSON structure"
                };
            }
        }

        // Handle Dictionary<string, object> properties
        if (context.Type.IsGenericType && context.Type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var valueType = context.Type.GetGenericArguments()[1];
            if (valueType == typeof(object))
            {
                schema.Type = "object";
                schema.AdditionalPropertiesAllowed = true;
                schema.AdditionalProperties = new OpenApiSchema
                {
                    Type = "object",
                    Description = "Dynamic value - can be any JSON type"
                };
            }
        }
    }
}
