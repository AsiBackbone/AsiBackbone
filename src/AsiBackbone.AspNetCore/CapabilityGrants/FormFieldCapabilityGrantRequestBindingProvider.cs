using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AsiBackbone.AspNetCore.CapabilityGrants;

/// <summary>
/// Binds a capability grant to the submitted form fields of the request.
/// </summary>
/// <remarks>
/// <para>
/// The binding value is <see cref="BindingPrefix" /> followed by the lowercase hexadecimal SHA-256 hash of a canonical
/// JSON encoding of the form fields: field names lower-cased with the invariant culture and sorted ordinally, each with
/// its values in submitted order. The antiforgery field configured in <see cref="AntiforgeryOptions.FormFieldName" />,
/// the token form field configured in <see cref="SignedCapabilityGrantOptions.TokenFormFieldName" />, and any name in
/// <see cref="FormFieldCapabilityGrantBindingOptions.ExcludedFieldNames" /> are left out. Issuers compute the same value
/// for the fields they intend to authorize with <see cref="CreateBinding" />.
/// </para>
/// <para>
/// A request that cannot carry a body binds the empty field set. A request with a form body that contains files, a body
/// that is not a form, or a form that cannot be read is not bound, which denies it. Only the form body is covered: the
/// query string and route values are not, so an endpoint that takes a resource identifier from its route should carry
/// that identifier as a form field or use a provider that binds it.
/// </para>
/// </remarks>
/// <param name="bindingOptions">The form-field binding options.</param>
/// <param name="grantOptions">The signed capability grant options, whose token form field is excluded.</param>
/// <param name="antiforgeryOptions">The antiforgery options, whose form field is excluded, when antiforgery is configured.</param>
public sealed class FormFieldCapabilityGrantRequestBindingProvider(
    IOptions<FormFieldCapabilityGrantBindingOptions> bindingOptions,
    IOptions<SignedCapabilityGrantOptions> grantOptions,
    IOptions<AntiforgeryOptions>? antiforgeryOptions = null) : ICapabilityGrantRequestBindingProvider
{
    /// <summary>
    /// Gets the prefix of every binding value produced by this provider.
    /// </summary>
    public const string BindingPrefix = "form-sha256:";

    private readonly FormFieldCapabilityGrantBindingOptions bindingOptions =
        bindingOptions?.Value ?? throw new ArgumentNullException(nameof(bindingOptions));

    private readonly SignedCapabilityGrantOptions grantOptions =
        grantOptions?.Value ?? throw new ArgumentNullException(nameof(grantOptions));

    private readonly AntiforgeryOptions? antiforgeryOptions = antiforgeryOptions?.Value;

    /// <inheritdoc />
    public async ValueTask<CapabilityGrantRequestBinding> ResolveAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        HttpRequest request = httpContext.Request;
        IReadOnlyCollection<string> excludedFieldNames = GetExcludedFieldNames();

        if (!request.HasFormContentType)
        {
            return CannotHaveBody(httpContext)
                ? CapabilityGrantRequestBinding.Bound(CreateBinding([], excludedFieldNames))
                : CapabilityGrantRequestBinding.Unavailable(
                    "The request body is not a form, so the form-field binding provider cannot bind it.");
        }

        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or BadHttpRequestException)
        {
            return CapabilityGrantRequestBinding.Unavailable("The request form could not be read.");
        }

        return form.Files.Count > 0
            ? CapabilityGrantRequestBinding.Unavailable(
                "The request form contains files, which the form-field binding provider does not bind.")
            : CapabilityGrantRequestBinding.Bound(CreateBinding(form, excludedFieldNames));
    }

    /// <summary>
    /// Computes the binding value for a set of form fields, for an issuer to place in
    /// <see cref="Core.CapabilityGrants.CapabilityGrant.ResourceBinding" />.
    /// </summary>
    /// <remarks>
    /// Pass the same exclusions the relying party applies. The relying party always excludes its antiforgery and token
    /// form fields, so an issuer that includes either here produces a binding that never matches.
    /// </remarks>
    /// <param name="fields">The form fields to bind.</param>
    /// <param name="excludedFieldNames">Field names to leave out, compared case-insensitively.</param>
    /// <returns>The binding value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fields" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Two field names are equal once lower-cased, or a field name is blank.</exception>
    public static string CreateBinding(
        IEnumerable<KeyValuePair<string, StringValues>> fields,
        IEnumerable<string>? excludedFieldNames = null)
    {
        ArgumentNullException.ThrowIfNull(fields);

        HashSet<string> excluded = new(
            (excludedFieldNames ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()),
            StringComparer.OrdinalIgnoreCase);
        SortedDictionary<string, StringValues> normalized = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, StringValues> field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                throw new ArgumentException("Form field names must not be blank.", nameof(fields));
            }

            if (excluded.Contains(field.Key.Trim()))
            {
                continue;
            }

            string name = field.Key.Trim().ToLowerInvariant();

            if (!normalized.TryAdd(name, field.Value))
            {
                throw new ArgumentException($"Form field names collide once lower-cased: '{name}'.", nameof(fields));
            }
        }

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartArray();
            foreach (KeyValuePair<string, StringValues> field in normalized)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(field.Key);
                writer.WriteStartArray();
                foreach (string? value in field.Value)
                {
                    writer.WriteStringValue(value ?? string.Empty);
                }

                writer.WriteEndArray();
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        return BindingPrefix + Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private static bool CannotHaveBody(HttpContext httpContext)
    {
        IHttpRequestBodyDetectionFeature? bodyDetection = httpContext.Features.Get<IHttpRequestBodyDetectionFeature>();

        if (bodyDetection is not null)
        {
            return !bodyDetection.CanHaveBody;
        }

        HttpRequest request = httpContext.Request;
        return request.ContentLength == 0
            || (request.ContentLength is null && string.IsNullOrEmpty(request.ContentType));
    }

    private string[] GetExcludedFieldNames()
    {
        List<string> names = [.. bindingOptions.ExcludedFieldNames];

        if (!string.IsNullOrWhiteSpace(antiforgeryOptions?.FormFieldName))
        {
            names.Add(antiforgeryOptions.FormFieldName);
        }

        if (!string.IsNullOrWhiteSpace(grantOptions.TokenFormFieldName))
        {
            names.Add(grantOptions.TokenFormFieldName);
        }

        return [.. names];
    }
}

/// <summary>
/// Configures <see cref="FormFieldCapabilityGrantRequestBindingProvider" />.
/// </summary>
public sealed class FormFieldCapabilityGrantBindingOptions
{
    /// <summary>
    /// Gets the default antiforgery form field name, excluded even when antiforgery options are not registered.
    /// </summary>
    public const string DefaultAntiforgeryFormFieldName = "__RequestVerificationToken";

    /// <summary>
    /// Gets additional form field names, compared case-insensitively, that the binding leaves out.
    /// </summary>
    /// <remarks>
    /// Contains <see cref="DefaultAntiforgeryFormFieldName" /> by default. Exclude only fields that do not change what the
    /// request does, because an excluded field can be changed without invalidating the grant.
    /// </remarks>
    public ISet<string> ExcludedFieldNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        DefaultAntiforgeryFormFieldName
    };
}
