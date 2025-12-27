using Microsoft.Extensions.Options;

namespace DecisionSpark.Core.Models.Configuration;

/// <summary>
/// Validates DecisionSpecsOptions configuration at startup.
/// </summary>
public class DecisionSpecsOptionsValidator : IValidateOptions<DecisionSpecsOptions>
{
    public ValidateOptionsResult Validate(string? name, DecisionSpecsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            return ValidateOptionsResult.Fail("DecisionSpecs:RootPath is required.");
        }

        if (options.SoftDeleteRetentionDays < 0)
        {
            return ValidateOptionsResult.Fail("DecisionSpecs:SoftDeleteRetentionDays must be >= 0.");
        }

        if (string.IsNullOrWhiteSpace(options.IndexFileName))
        {
            return ValidateOptionsResult.Fail("DecisionSpecs:IndexFileName is required.");
        }

        // Ensure RootPath exists and is writable
        try
        {
            var directory = new DirectoryInfo(options.RootPath);
            if (!directory.Exists)
            {
                directory.Create();
            }

            // Test write permission
            var testFile = Path.Combine(options.RootPath, $".write-test-{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail($"DecisionSpecs:RootPath '{options.RootPath}' is not writable: {ex.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}
