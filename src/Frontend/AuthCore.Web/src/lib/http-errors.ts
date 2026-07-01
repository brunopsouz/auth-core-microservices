type ErrorPayload = {
  errors?: string[];
  Errors?: string[];
};

export async function readApiError(response: Response) {
  const fallbackMessage = "Não foi possível concluir a operação.";

  try {
    const payload = (await response.json()) as ErrorPayload;
    const errors = payload.errors ?? payload.Errors;

    if (Array.isArray(errors) && errors.length > 0) {
      return errors.join(" ");
    }
  } catch {
    return fallbackMessage;
  }

  return fallbackMessage;
}
