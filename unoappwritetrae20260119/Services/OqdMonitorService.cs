using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using unoappwritetrae20260119.Models;

namespace unoappwritetrae20260119.Services;

public sealed class OqdMonitorService
{
    private const string OqdUrl = "https://www.gulfmerc.com/";
    private static readonly HttpClient HttpClient = new();
    private readonly string _historyFilePath;
    private Timer? _timer;

    public OqdMonitorService()
    {
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "unoappwritetrae20260119");
        Directory.CreateDirectory(appFolder);
        _historyFilePath = Path.Combine(appFolder, "oqd-history.json");
    }

    public async Task<IReadOnlyList<OqdPricePoint>> LoadHistoryAsync()
    {
        if (!File.Exists(_historyFilePath))
        {
            return Array.Empty<OqdPricePoint>();
        }

        await using var stream = File.OpenRead(_historyFilePath);
        var items = await JsonSerializer.DeserializeAsync<List<OqdPricePoint>>(stream);
        if (items is null)
        {
            return Array.Empty<OqdPricePoint>();
        }

        return items
            .OrderBy(x => x.RecordedAt)
            .ToList();
    }

    public void StartDailyScheduler(Func<Task> onPriceFetched)
    {
        ScheduleNextFetch(onPriceFetched);
    }

    public async Task<IReadOnlyList<OqdPricePoint>> FetchAndPersistLatestAsync(CancellationToken cancellationToken = default)
    {
        var latest = await FetchLatestPriceAsync(cancellationToken);
        var history = (await LoadHistoryAsync()).ToList();

        var existingToday = history.FirstOrDefault(x => x.RecordedAt.Date == latest.RecordedAt.Date);
        if (existingToday is not null)
        {
            existingToday.RecordedAt = latest.RecordedAt;
            existingToday.Price = latest.Price;
        }
        else
        {
            history.Add(latest);
        }

        history = history
            .OrderBy(x => x.RecordedAt)
            .TakeLast(90)
            .ToList();

        await SaveHistoryAsync(history, cancellationToken);
        return history;
    }

    public async Task EnsureTodaySnapshotAsync(CancellationToken cancellationToken = default)
    {
        var history = await LoadHistoryAsync();
        if (history.Any(x => x.RecordedAt.Date == DateTime.Today))
        {
            return;
        }

        await FetchAndPersistLatestAsync(cancellationToken);
    }

    private async Task<OqdPricePoint> FetchLatestPriceAsync(CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(OqdUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var markerMatch = Regex.Match(
            html,
            @"OQD\s+Marker\s+Price.*?\bis\s+(?<price>\d+(?:\.\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!markerMatch.Success)
        {
            markerMatch = Regex.Match(
                html,
                @"OQD\s+Daily\s+Marker\s+Price(?:\s|<[^>]+>|&nbsp;|&#160;)*?(?<price>\d{1,4}\.\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        if (!markerMatch.Success)
        {
            throw new InvalidOperationException("Unable to parse OQD marker price from gulfmerc.com.");
        }

        var priceText = markerMatch.Groups["price"].Value;
        if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
        {
            throw new InvalidOperationException($"Invalid OQD marker price: {priceText}");
        }

        return new OqdPricePoint
        {
            RecordedAt = DateTime.Now,
            Price = price
        };
    }

    private async Task SaveHistoryAsync(List<OqdPricePoint> history, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_historyFilePath);
        await JsonSerializer.SerializeAsync(stream, history, cancellationToken: cancellationToken, options: new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private void ScheduleNextFetch(Func<Task> onPriceFetched)
    {
        var now = DateTime.Now;
        var next1Pm = now.Date.AddHours(13);
        if (next1Pm <= now)
        {
            next1Pm = next1Pm.AddDays(1);
        }

        var delay = next1Pm - now;
        _timer?.Dispose();
        _timer = new Timer(async _ =>
        {
            try
            {
                await FetchAndPersistLatestAsync();
                await onPriceFetched();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OQD fetch failed: {ex.Message}");
            }
            finally
            {
                ScheduleNextFetch(onPriceFetched);
            }
        }, null, delay, Timeout.InfiniteTimeSpan);
    }
}
