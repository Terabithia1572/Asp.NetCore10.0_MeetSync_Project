using Microsoft.AspNetCore.Mvc.Filters;

namespace Asp.NetCore10._0_MeetSync_Project.Filters;

public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid input value." : e.ErrorMessage).ToArray()
                );

            throw new MeetSync.Domain.Exceptions.ValidationException(errors);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
