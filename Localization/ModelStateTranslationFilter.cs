using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RothaShop.Localization;

/// <summary>
/// Rewrites model-state error text into the active language after the action has
/// run and before the view is rendered. Model binding messages (data annotations)
/// and Identity messages are compiled in English, so this is the only place they
/// can be translated without duplicating every rule.
/// </summary>
public sealed class ModelStateTranslationFilter : IAsyncActionFilter
{
    private readonly ITranslator _translator;

    public ModelStateTranslationFilter(ITranslator translator)
    {
        _translator = translator;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await next();

        if (context.ModelState.Count == 0)
        {
            return;
        }

        foreach (var entry in context.ModelState.Values)
        {
            Translate(entry);
        }
    }

    private void Translate(ModelStateEntry entry)
    {
        if (entry.Errors.Count == 0)
        {
            return;
        }

        List<ModelError>? replacement = null;

        foreach (var error in entry.Errors)
        {
            var translated = ValidationMessageMap.Translate(error.ErrorMessage, _translator);

            if (string.Equals(translated, error.ErrorMessage, StringComparison.Ordinal))
            {
                replacement?.Add(error);
                continue;
            }

            replacement ??= entry.Errors.TakeWhile(e => !ReferenceEquals(e, error)).ToList();
            replacement.Add(error.Exception is null
                ? new ModelError(translated)
                : new ModelError(error.Exception, translated));
        }

        if (replacement is null)
        {
            return;
        }

        entry.Errors.Clear();

        foreach (var error in replacement)
        {
            entry.Errors.Add(error);
        }
    }
}
