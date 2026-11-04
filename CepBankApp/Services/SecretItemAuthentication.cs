namespace CepBankApp.Services;

public static class SecretItemAuthentication
{
    public static async Task<(bool Succeeded, string Message)> AuthenticateAsync()
    {
#if IOS
        using var context = new LocalAuthentication.LAContext();
        var policy = LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics;
        if (!context.CanEvaluatePolicy(policy, out _))
            return (false, "Face ID kullanılamıyor. Cihaz ayarlarından Face ID'yi etkinleştir.");
        if (context.BiometryType != LocalAuthentication.LABiometryType.FaceId)
            return (false, "Gizli ürünler için Face ID gerekir.");

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.EvaluatePolicy(policy, "Gizli ürünün ayrıntılarını göster", (approved, _) => completion.TrySetResult(approved));
        return await completion.Task
            ? (true, string.Empty)
            : (false, "Face ID doğrulaması tamamlanmadı.");
#else
        await Task.CompletedTask;
        return (false, "Face ID yalnızca desteklenen iPhone cihazlarında kullanılabilir.");
#endif
    }
}
