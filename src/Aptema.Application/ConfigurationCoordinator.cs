using Aptema.Core;

namespace Aptema.Application;

public sealed class ConfigurationCoordinator(ISettingsStore settingsStore)
{
    public async ValueTask<OperationResult<UserSettings>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return OperationResult<UserSettings>.Succeeded(await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResult<UserSettings>.Failure(OperationStatus.Unavailable, "SettingsUnavailable");
        }
        catch (Exception)
        {
            return OperationResult<UserSettings>.Failure(OperationStatus.Failed, "SettingsLoadFailed");
        }
    }

    public async ValueTask<OperationResult<UserSettings>> SaveAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            return OperationResult<UserSettings>.Succeeded(settings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResult<UserSettings>.Failure(OperationStatus.Unavailable, "SettingsUnavailable");
        }
        catch (Exception)
        {
            return OperationResult<UserSettings>.Failure(OperationStatus.Failed, "SettingsSaveFailed");
        }
    }
}
