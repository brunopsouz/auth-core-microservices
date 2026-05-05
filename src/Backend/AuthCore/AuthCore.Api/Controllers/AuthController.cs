using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Application.Authentication.UseCases.ResendVerification;
using AuthCore.Application.Authentication.UseCases.VerifyEmail;
using AuthCore.Application.Common.Models.Responses;
using AuthCore.Application.Users.UseCases.RegisterUser;
using AuthCore.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsável pelas operações de autenticação.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : AuthControllerBase
{
    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="logger">Logger do fluxo de autenticação.</param>
    public AuthController(ILogger<AuthController> logger)
        : base(logger)
    {
    }

    #endregion

    /// <summary>
    /// Operação para registrar um usuário pendente de verificação.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pelo registro do usuário.</param>
    /// <param name="request">Dados da requisição de registro.</param>
    /// <returns>Resposta com os dados do usuário registrado.</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ResponseRegisteredUserJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResponseRegisteredUserJson>> Register(
        [FromServices] IRegisterUserUseCase useCase,
        [FromBody] RequestRegisterUserJson request)
    {
        try
        {
            var result = await useCase.Execute(new RegisterUserCommand
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Contact = request.Contact,
                Password = request.Password,
                ConfirmPassword = request.ConfirmPassword
            });

            return Created(string.Empty, new ResponseRegisteredUserJson
            {
                UserIdentifier = result.UserIdentifier,
                FullName = result.FullName,
                Email = result.Email
            });
        }
        catch (Exception exception) when (TryMapKnownException(exception, out var actionResult))
        {
            return actionResult;
        }
    }

    /// <summary>
    /// Operação para validar o código OTP de verificação de e-mail.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pela validação do e-mail.</param>
    /// <param name="request">Dados da requisição de validação.</param>
    /// <returns>Resposta sem conteúdo após a confirmação do e-mail.</returns>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> VerifyEmail(
        [FromServices] IVerifyEmailUseCase useCase,
        [FromBody] RequestVerifyEmailJson request)
    {
        try
        {
            await useCase.Execute(new VerifyEmailCommand
            {
                Email = request.Email,
                Code = request.Code
            });

            return NoContent();
        }
        catch (Exception exception) when (exception is DomainException or NotFoundException)
        {
            return BadRequest(CreateErrorResponse("Não foi possível validar o código de verificação informado."));
        }
        catch (Exception exception) when (TryMapKnownException(exception, out var actionResult))
        {
            return actionResult;
        }
    }

    /// <summary>
    /// Operação para reenviar a verificação de e-mail pendente.
    /// </summary>
    /// <param name="useCase">Caso de uso responsável pelo reenvio.</param>
    /// <param name="request">Dados da requisição de reenvio.</param>
    /// <returns>Resposta sem conteúdo após o reenvio.</returns>
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ResendVerification(
        [FromServices] IResendVerificationUseCase useCase,
        [FromBody] RequestResendVerificationJson request)
    {
        try
        {
            await useCase.Execute(new ResendVerificationCommand
            {
                Email = request.Email
            });

            return NoContent();
        }
        catch (Exception exception) when (exception is NotFoundException or ForbiddenException)
        {
            return NoContent();
        }
        catch (Exception exception) when (TryMapKnownException(exception, out var actionResult))
        {
            return actionResult;
        }
    }
}
