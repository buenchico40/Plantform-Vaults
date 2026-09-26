using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PlatformVault.Api.Infrastructure;

/// <summary>
/// Enumerados en parámetros de consulta y de ruta con los valores del contrato OpenAPI (`JsonStringEnumMemberName`,
/// por ejemplo `Crítico` o `Expirado`), igual que en el cuerpo JSON. También acepta el nombre interno del miembro.
/// Sin esto, el enlace de modelos de ASP.NET Core solo reconoce el nombre en inglés.
/// </summary>
public sealed class ContractEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type.IsEnum && context.BindingInfo.BindingSource is { } source
               && (source == BindingSource.Query || source == BindingSource.Path)
            ? new ContractEnumModelBinder(type)
            : null;
    }
}

public sealed class ContractEnumModelBinder(Type enumType) : IModelBinder
{
    private readonly Dictionary<string, object> _values = BuildMap(enumType);

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
            return Task.CompletedTask;
        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);

        var text = result.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text))
            return Task.CompletedTask;
        if (_values.TryGetValue(text, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                $"Valor no válido: '{text}'. Valores admitidos: {string.Join(", ", _values.Keys.Where(k => k.Any(char.IsLetter)).Distinct(StringComparer.OrdinalIgnoreCase))}.");
        }
        return Task.CompletedTask;
    }

    private static Dictionary<string, object> BuildMap(Type type)
    {
        var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = field.GetValue(null)!;
            var contractName = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name;
            if (contractName is not null)
                map[contractName] = value;
            map.TryAdd(field.Name, value);
        }
        return map;
    }
}
