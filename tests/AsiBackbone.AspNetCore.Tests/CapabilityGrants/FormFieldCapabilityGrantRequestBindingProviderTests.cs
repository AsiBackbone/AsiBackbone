using AsiBackbone.AspNetCore.CapabilityGrants;
using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.CapabilityGrants;

/// <summary>
/// Tests for <see cref="FormFieldCapabilityGrantRequestBindingProvider" />.
/// </summary>
public sealed class FormFieldCapabilityGrantRequestBindingProviderTests
{
    /// <summary>
    /// Verifies the binding ignores field order and name casing but not value order.
    /// </summary>
    [Fact]
    public void CreateBindingIsCanonical()
    {
        string first = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(new Dictionary<string, StringValues>
        {
            ["OrderId"] = "order-1",
            ["tags"] = new StringValues(["a", "b"])
        });
        string reordered = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(new Dictionary<string, StringValues>
        {
            ["tags"] = new StringValues(["a", "b"]),
            ["orderid"] = "order-1"
        });
        string valuesSwapped = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(new Dictionary<string, StringValues>
        {
            ["orderid"] = "order-1",
            ["tags"] = new StringValues(["b", "a"])
        });

        Assert.StartsWith(FormFieldCapabilityGrantRequestBindingProvider.BindingPrefix, first, StringComparison.Ordinal);
        Assert.Equal(first, reordered);
        Assert.NotEqual(first, valuesSwapped);
    }

    /// <summary>
    /// Verifies field boundaries cannot be shifted to produce the same binding.
    /// </summary>
    [Fact]
    public void CreateBindingSeparatesFieldsUnambiguously()
    {
        string split = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(new Dictionary<string, StringValues>
        {
            ["a"] = "b",
            ["c"] = "d"
        });
        string joined = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(new Dictionary<string, StringValues>
        {
            ["a"] = "b\",\"c\":\"d"
        });

        Assert.NotEqual(split, joined);
    }

    /// <summary>
    /// Verifies excluded fields are left out and colliding names are rejected.
    /// </summary>
    [Fact]
    public void CreateBindingAppliesExclusionsAndRejectsCollisions()
    {
        string withoutExcluded = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(
            new Dictionary<string, StringValues>(StringComparer.Ordinal) { ["orderId"] = "1" });
        string withExcluded = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(
            new Dictionary<string, StringValues>(StringComparer.Ordinal) { ["orderId"] = "1", ["__requestverificationtoken"] = "x" },
            [FormFieldCapabilityGrantBindingOptions.DefaultAntiforgeryFormFieldName]);

        Assert.Equal(withoutExcluded, withExcluded);
        _ = Assert.Throws<ArgumentException>(() => FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(
            new Dictionary<string, StringValues>(StringComparer.Ordinal) { ["Id"] = "1", ["id"] = "2" }));
        _ = Assert.Throws<ArgumentException>(() => FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(
            new Dictionary<string, StringValues>(StringComparer.Ordinal) { [" "] = "1" }));
        _ = Assert.Throws<ArgumentNullException>(() => FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(null!));
    }

    /// <summary>
    /// Verifies the configured antiforgery field, the token field, and configured exclusions are left out of the request
    /// binding.
    /// </summary>
    [Fact]
    public async Task ResolveAsyncExcludesAntiforgeryTokenAndConfiguredFields()
    {
        FormFieldCapabilityGrantBindingOptions bindingOptions = new();
        _ = bindingOptions.ExcludedFieldNames.Add("returnUrl");
        var provider = new FormFieldCapabilityGrantRequestBindingProvider(
            Options.Create(bindingOptions),
            Options.Create(new SignedCapabilityGrantOptions { TokenFormFieldName = "grant" }),
            Options.Create(new AntiforgeryOptions { FormFieldName = "custom-antiforgery" }));
        DefaultHttpContext httpContext = CreateFormContext(new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            ["orderId"] = "1",
            ["grant"] = "asibcg1.token",
            ["custom-antiforgery"] = "af",
            ["__RequestVerificationToken"] = "default-af",
            ["returnUrl"] = "/orders"
        });

        CapabilityGrantRequestBinding binding = await provider.ResolveAsync(
            httpContext,
            EndpointGovernanceDescriptor.None("orders.approve"),
            TestContext.Current.CancellationToken);

        Assert.True(binding.IsAvailable);
        Assert.Equal(
            FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(new Dictionary<string, StringValues> { ["orderId"] = "1" }),
            binding.Value);
    }

    /// <summary>
    /// Verifies a request that cannot carry a body binds the empty field set.
    /// </summary>
    [Fact]
    public async Task ResolveAsyncBindsEmptyFieldSetForBodylessRequest()
    {
        FormFieldCapabilityGrantRequestBindingProvider provider = CreateProvider();
        var withoutDetection = new DefaultHttpContext();
        var withDetection = new DefaultHttpContext();
        withDetection.Request.ContentType = "application/json";
        withDetection.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(canHaveBody: false));
        var withBody = new DefaultHttpContext();
        withBody.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(canHaveBody: true));

        CapabilityGrantRequestBinding first = await provider.ResolveAsync(withoutDetection, EndpointGovernanceDescriptor.None("op"), TestContext.Current.CancellationToken);
        CapabilityGrantRequestBinding second = await provider.ResolveAsync(withDetection, EndpointGovernanceDescriptor.None("op"), TestContext.Current.CancellationToken);
        CapabilityGrantRequestBinding third = await provider.ResolveAsync(withBody, EndpointGovernanceDescriptor.None("op"), TestContext.Current.CancellationToken);

        string empty = FormFieldCapabilityGrantRequestBindingProvider.CreateBinding([]);
        Assert.Equal(empty, first.Value);
        Assert.Equal(empty, second.Value);
        Assert.False(third.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(third.FailureMessage));
    }

    /// <summary>
    /// Verifies the binding result factories validate their arguments.
    /// </summary>
    [Fact]
    public void BindingResultFactoriesValidateArguments()
    {
        Assert.True(CapabilityGrantRequestBinding.None.IsAvailable);
        Assert.Null(CapabilityGrantRequestBinding.None.Value);
        Assert.Equal("value", CapabilityGrantRequestBinding.Bound(" value ").Value);
        _ = Assert.Throws<ArgumentException>(() => CapabilityGrantRequestBinding.Bound(" "));
        _ = Assert.Throws<ArgumentException>(() => CapabilityGrantRequestBinding.Unavailable(""));
    }

    private static FormFieldCapabilityGrantRequestBindingProvider CreateProvider()
    {
        return new FormFieldCapabilityGrantRequestBindingProvider(
            Options.Create(new FormFieldCapabilityGrantBindingOptions()),
            Options.Create(new SignedCapabilityGrantOptions()));
    }

    private static DefaultHttpContext CreateFormContext(Dictionary<string, StringValues> fields)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(fields);
        return httpContext;
    }

    private sealed class BodyDetection(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
