using Microsoft.Extensions.Options;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Blockchain;

public sealed class BlockchainOptionsValidator : IValidateOptions<BlockchainOptions>
{
    public ValidateOptionsResult Validate(string? name, BlockchainOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("BlockchainOptions cannot be null.");
        }

        if (!BlockchainOptions.TryCreateBaseUri(options.BaseUrl, out _, out var uriError))
        {
            return ValidateOptionsResult.Fail(uriError!);
        }

        if (options.TimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("TimeoutSeconds must be greater than zero.");
        }

        if (options.MaxRetries < 0)
        {
            return ValidateOptionsResult.Fail("MaxRetries must be non-negative.");
        }

        return ValidateOptionsResult.Success;
    }
}
