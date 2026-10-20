using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplicationAgent.Collection.Application.Connectors;
using JobApplicationAgent.Collection.Application.Imports;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Collection.Infrastructure.Connectors;

public interface IAdzunaJobsClient
{
    Task<IReadOnlyList<ImportOfferCommand>> SearchAsync(string what, string? where, int page, CancellationToken ct);
}

public sealed class AdzunaJobsClient(HttpClient http, IOptions<AdzunaOptions> settings) : IAdzunaJobsClient
{
    private const int MaximumPage = 100;
    private const int MaximumPageSize = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<ImportOfferCommand>> SearchAsync(string what, string? where, int page, CancellationToken ct)
    {
        var options = settings.Value;
        if (string.IsNullOrWhiteSpace(options.AppId) || options.AppId.Length > 200 ||
            string.IsNullOrWhiteSpace(options.AppKey) || options.AppKey.Length > 500 ||
            options.AppId.Any(char.IsWhiteSpace) || options.AppKey.Any(char.IsWhiteSpace))
            throw new CollectionSourceUnavailableException("Adzuna source credentials are not configured.");
        if (!string.Equals(options.Country, "fr", StringComparison.OrdinalIgnoreCase))
            throw new CollectionSourceUnavailableException("Adzuna source is configured for France only.");
        if (string.IsNullOrWhiteSpace(what) || what.Length > 200 || (where?.Length ?? 0) > 200 ||
            page is < 1 or > MaximumPage || options.ResultsPerPage is < 1 or > MaximumPageSize)
            throw new FluentValidation.ValidationException("Adzuna search criteria or pagination are invalid.");

        var query = new List<string>
        {
            "app_id=" + Uri.EscapeDataString(options.AppId),
            "app_key=" + Uri.EscapeDataString(options.AppKey),
            "results_per_page=" + options.ResultsPerPage.ToString(CultureInfo.InvariantCulture),
            "what=" + Uri.EscapeDataString(what.Trim()),
            "sort_by=date",
            "content-type=application/json"
        };
        if (!string.IsNullOrWhiteSpace(where)) query.Add("where=" + Uri.EscapeDataString(where.Trim()));

        try
        {
            using var response = await http.GetAsync($"jobs/fr/search/{page}?{string.Join("&", query)}", ct);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new CollectionSourceUnavailableException("Adzuna could not provide offers right now.");

            var payload = await response.Content.ReadFromJsonAsync<AdzunaSearchResponse>(JsonOptions, ct)
                ?? throw new CollectionSourceUnavailableException("Adzuna returned an invalid response.");
            return (payload.Results ?? [])
                .Select(MapOffer)
                .Where(offer => offer is not null)
                .Cast<ImportOfferCommand>()
                .ToArray();
        }
        catch (HttpRequestException)
        {
            throw new CollectionSourceUnavailableException("Adzuna could not be reached.");
        }
        catch (JsonException)
        {
            throw new CollectionSourceUnavailableException("Adzuna returned an invalid response.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CollectionSourceUnavailableException("Adzuna did not respond in time.");
        }
    }

    private static ImportOfferCommand? MapOffer(AdzunaJob job)
    {
        var title = OfferNormalizer.Text(job.Title);
        var company = OfferNormalizer.Text(job.Company?.DisplayName);
        var location = OfferNormalizer.Text(job.Location?.DisplayName);
        if (title is null || company is null || !TryAdzunaLink(job.RedirectUrl, out var link)) return null;

        DateTime? publishedAtUtc = DateTimeOffset.TryParse(job.Created, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var created)
            ? created.UtcDateTime
            : null;
        var contractType = job.ContractType?.Trim().ToLowerInvariant() switch
        {
            "permanent" => "Permanent",
            "contract" => "FixedTerm",
            _ => "Unknown"
        };

        return new ImportOfferCommand(title, company, location, "Unknown", contractType,
            Description: OfferNormalizer.Text(job.Description), SourceName: "Adzuna", SourceUrl: link,
            PublishedAtUtc: publishedAtUtc);
    }

    private static bool TryAdzunaLink(string? value, out string link)
    {
        link = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !(uri.Host.Equals("adzuna.fr", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".adzuna.fr", StringComparison.OrdinalIgnoreCase))) return false;
        var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1, Fragment = string.Empty };
        link = builder.Uri.AbsoluteUri;
        return true;
    }

    private sealed record AdzunaSearchResponse([property: JsonPropertyName("results")] AdzunaJob[]? Results);
    private sealed record AdzunaJob(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("company")] AdzunaCompany? Company,
        [property: JsonPropertyName("location")] AdzunaLocation? Location,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("redirect_url")] string? RedirectUrl,
        [property: JsonPropertyName("created")] string? Created,
        [property: JsonPropertyName("contract_type")] string? ContractType);
    private sealed record AdzunaCompany([property: JsonPropertyName("display_name")] string? DisplayName);
    private sealed record AdzunaLocation([property: JsonPropertyName("display_name")] string? DisplayName);
}
