using AuthCore.Application.UseCases.Authentication.Login;
using AuthCore.Application.UseCases.Authentication.LoginSession;
using AuthCore.Application.UseCases.Authentication.LogoutAllSessions;
using AuthCore.Application.UseCases.Authentication.LogoutCurrentSession;
using AuthCore.Application.UseCases.Authentication.LogoutSession;
using AuthCore.Application.UseCases.Authentication.GetUserSessions;
using AuthCore.Application.UseCases.Authentication.RefreshSession;
using AuthCore.Application.UseCases.Authentication.ResendVerification;
using AuthCore.Application.UseCases.Authentication.RevokeUserSession;
using AuthCore.Application.UseCases.Authentication.VerifyEmail;
using AuthCore.Application.UseCases.Users.ChangePassword;
using AuthCore.Application.UseCases.Users.DeleteUser;
using AuthCore.Application.UseCases.Users.GetUserProfile;
using AuthCore.Application.UseCases.Users.RegisterUser;
using AuthCore.Application.UseCases.Users.UpdateUser;
using Microsoft.Extensions.DependencyInjection;

namespace AuthCore.Application;

/// <summary>
/// Define operações para registrar dependências da aplicação.
/// </summary>
public static class ApplicationDependencyInjection
{
    /// <summary>
    /// Operação para adicionar os serviços da aplicação.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <returns>Coleção de serviços atualizada.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ILoginUseCase, LoginUseCase>();
        services.AddScoped<ILoginSessionUseCase, LoginSessionUseCase>();
        services.AddScoped<ILogoutCurrentSessionUseCase, LogoutCurrentSessionUseCase>();
        services.AddScoped<ILogoutSessionUseCase, LogoutSessionUseCase>();
        services.AddScoped<IGetUserSessionsUseCase, GetUserSessionsUseCase>();
        services.AddScoped<IRevokeUserSessionUseCase, RevokeUserSessionUseCase>();
        services.AddScoped<ILogoutAllSessionsUseCase, LogoutAllSessionsUseCase>();
        services.AddScoped<IRefreshSessionUseCase, RefreshSessionUseCase>();
        services.AddScoped<IVerifyEmailUseCase, VerifyEmailUseCase>();
        services.AddScoped<IResendVerificationUseCase, ResendVerificationUseCase>();
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<IGetUserProfileUseCase, GetUserProfileUseCase>();
        services.AddScoped<IUpdateUserUseCase, UpdateUserUseCase>();
        services.AddScoped<IChangePasswordUseCase, ChangePasswordUseCase>();
        services.AddScoped<IDeleteUserUseCase, DeleteUserUseCase>();

        return services;
    }
}
