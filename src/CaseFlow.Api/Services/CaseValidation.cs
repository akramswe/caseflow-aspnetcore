using System.ComponentModel.DataAnnotations;
using CaseFlow.Api.Contracts;

namespace CaseFlow.Api.Services;

public static class CaseValidation
{
    public static string? ValidateCreate(CreateCaseRequest request)
    {
        if (!Between(request.Title, 5, 120)) return "Title must contain 5–120 characters.";
        if (!Between(request.Description, 10, 2000)) return "Description must contain 10–2000 characters.";
        if (!Between(request.CustomerName, 2, 100)) return "Customer name must contain 2–100 characters.";
        if (string.IsNullOrWhiteSpace(request.CustomerEmail)
            || request.CustomerEmail.Trim().Length > 254
            || !new EmailAddressAttribute().IsValid(request.CustomerEmail.Trim()))
            return "Enter a valid customer email address.";
        if (request.Priority is null || !Enum.IsDefined(request.Priority.Value))
            return "Select a valid case priority.";
        if (request.AssignedTo is not null && request.AssignedTo.Trim().Length > 80)
            return "Assignee cannot exceed 80 characters.";
        return null;
    }

    public static bool Between(string? value, int min, int max) =>
        value is not null && value.Trim().Length >= min && value.Trim().Length <= max;

    public static string ActorOrDefault(string? actor) =>
        string.IsNullOrWhiteSpace(actor) ? "Demo User" : actor.Trim();

    public static bool ValidActor(string? actor) =>
        actor is null || actor.Trim().Length <= 80;
}
