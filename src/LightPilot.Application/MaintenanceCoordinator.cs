namespace LightPilot.Application;

public sealed class MaintenanceCoordinator(IConfigurationTransferService transferService)
{
    public async ValueTask<OperationResult<ConfigurationImportPreview>> PreviewImportAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<ConfigurationImportPreview>.Failure(OperationStatus.ValidationFailure, "ImportPathRequired");
        }

        try
        {
            var preview = await transferService.PreviewImportAsync(path, cancellationToken).ConfigureAwait(false);
            return preview.IsValid
                ? OperationResult<ConfigurationImportPreview>.Succeeded(preview)
                : OperationResult<ConfigurationImportPreview>.Failure(OperationStatus.ValidationFailure, "InvalidConfiguration");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResult<ConfigurationImportPreview>.Failure(OperationStatus.Unavailable, "ConfigurationFileUnavailable");
        }
        catch (Exception)
        {
            return OperationResult<ConfigurationImportPreview>.Failure(OperationStatus.Failed, "ConfigurationPreviewFailed");
        }
    }
}
