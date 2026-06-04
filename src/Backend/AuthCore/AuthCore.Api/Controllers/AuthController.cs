using AuthCore.Api.Contracts.Requests;
using AuthCore.Api.Contracts.Responses;
using AuthCore.Application.UseCases.Authentication.ResendVerification;
using AuthCore.Application.UseCases.Authentication.VerifyEmail;
using AuthCore.Application.UseCases.Users.RegisterUser;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.Api.Controllers;

/// <summary>
/// Representa controller responsavel pelas operacoes de autenticacao.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Operacao para registrar um usuario pendente de verificacao.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pelo registro do usuario.</param>
    /// <param name="request">Dados da requisicao de registro.</param>
    /// <returns>Resposta com os dados do usuario registrado.</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ResponseRegisteredUserJson), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResponseRegisteredUserJson>> Register(
        [FromServices] IRegisterUserUseCase useCase,
        [FromBody] RequestRegisterUserJson request)
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

    /// <summary>
    /// Operacao para validar o codigo OTP de verificacao de e-mail.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pela validacao do e-mail.</param>
    /// <param name="request">Dados da requisicao de validacao.</param>
    /// <returns>Resposta sem conteudo apos a confirmacao do e-mail.</returns>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> VerifyEmail(
        [FromServices] IVerifyEmailUseCase useCase,
        [FromBody] RequestVerifyEmailJson request)
    {
        await useCase.Execute(new VerifyEmailCommand
        {
            Email = request.Email,
            Code = request.Code
        });

        return NoContent();
    }

    /// <summary>
    /// Operacao para reenviar a verificacao de e-mail pendente.
    /// </summary>
    /// <param name="useCase">Caso de uso responsavel pelo reenvio.</param>
    /// <param name="request">Dados da requisicao de reenvio.</param>
    /// <returns>Resposta sem conteudo apos o reenvio.</returns>
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ResendVerification(
        [FromServices] IResendVerificationUseCase useCase,
        [FromBody] RequestResendVerificationJson request)
    {
        await useCase.Execute(new ResendVerificationCommand
        {
            Email = request.Email
        });

        return NoContent();
    }
}
