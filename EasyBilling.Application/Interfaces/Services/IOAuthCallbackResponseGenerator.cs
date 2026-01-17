namespace EasyBilling.Application.Interfaces.Services;

public interface IOAuthCallbackResponseGenerator
{
    string GenerateSuccessResponse();
    string GenerateErrorResponse(string errorMessage);
}
