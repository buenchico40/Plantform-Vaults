using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PlatformVault.Api.Infrastructure;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Api.Tests;

public sealed class ContractEnumModelBinderTests
{
    private static async Task<ModelBindingContext> BindAsync(Type type, string query)
    {
        var context = new DefaultModelBindingContext
        {
            ModelName = "value",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query,
                new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["value"] = query }),
                CultureInfo.InvariantCulture),
        };
        await new ContractEnumModelBinder(type).BindModelAsync(context);
        return context;
    }

    [Theory]
    [InlineData(typeof(Criticality), "Crítico", Criticality.Critical)]
    [InlineData(typeof(Criticality), "Critical", Criticality.Critical)]
    [InlineData(typeof(Criticality), "bajo", Criticality.Low)]
    public async Task Accepts_contract_values_and_internal_names(Type type, string text, object expected)
    {
        var context = await BindAsync(type, text);
        Assert.True(context.Result.IsModelSet);
        Assert.Equal(expected, context.Result.Model);
    }

    [Fact]
    public async Task Contract_values_for_expiration_and_state()
    {
        Assert.Equal(ExpirationStatus.Expired, (await BindAsync(typeof(ExpirationStatus), "Expirado")).Result.Model);
        Assert.Equal(ExpirationStatus.ExpiringSoon, (await BindAsync(typeof(ExpirationStatus), "PróximoAVencer")).Result.Model);
        Assert.Equal(LifecycleState.Active, (await BindAsync(typeof(LifecycleState), "Activo")).Result.Model);
        Assert.Equal(DeploymentEnvironment.Production, (await BindAsync(typeof(DeploymentEnvironment), "Producción")).Result.Model);
    }

    [Fact]
    public async Task Unknown_values_are_a_validation_error()
    {
        var context = await BindAsync(typeof(Criticality), "Urgente");
        Assert.False(context.Result.IsModelSet);
        Assert.False(context.ModelState.IsValid);
    }
}
