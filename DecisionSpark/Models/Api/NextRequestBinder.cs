using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace DecisionSpark.Models.Api;

public class NextRequestBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
   {
throw new ArgumentNullException(nameof(bindingContext));
        }

        var request = bindingContext.HttpContext.Request;
        
        if (request.Body == null)
        {
       Console.WriteLine("[NextRequestBinder] Request body is null");
       bindingContext.Result = ModelBindingResult.Failed();
        return;
      }

        try
        {
       using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
          
   Console.WriteLine($"[NextRequestBinder] Raw body: {body}");

   var options = new JsonSerializerOptions
 {
      PropertyNameCaseInsensitive = true
    };

  var nextRequest = JsonSerializer.Deserialize<NextRequest>(body, options);
      
            Console.WriteLine($"[NextRequestBinder] Deserialized UserInput: '{nextRequest?.UserInput ?? "NULL"}'");

    bindingContext.Result = ModelBindingResult.Success(nextRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NextRequestBinder] Error: {ex.Message}");
    bindingContext.Result = ModelBindingResult.Failed();
        }
    }
}
