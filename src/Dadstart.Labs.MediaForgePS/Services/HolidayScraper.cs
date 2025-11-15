using System.Net.Http;
using HtmlAgilityPack;
using Dadstart.Labs.MediaForgePS.Models;

namespace Dadstart.Labs.MediaForgePS.Services;

/// <summary>
/// Service for scraping holiday information from timeanddate.com.
/// </summary>
public class HolidayScraper
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://www.timeanddate.com/holidays/us/";

    /// <summary>
    /// Initializes a new instance of the <see cref="HolidayScraper"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making web requests.</param>
    public HolidayScraper(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    /// <summary>
    /// Gets holidays for the specified date.
    /// </summary>
    /// <param name="date">Date to retrieve holidays for.</param>
    /// <returns>List of holidays for the specified date.</returns>
    public async Task<List<Holiday>> GetHolidaysAsync(DateTime date)
    {
        var uriBuilder = new UriBuilder(BaseUrl)
        {
            Query = $"year={date.Year}&month={date.Month}&day={date.Day}"
        };
        var url = uriBuilder.ToString();
        var html = await _httpClient.GetStringAsync(url);
        return ParseHolidays(html, date);
    }

    private List<Holiday> ParseHolidays(string html, DateTime date)
    {
        var holidays = new List<Holiday>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Look for holiday table rows - timeanddate.com typically uses table structure
        var tableRows = doc.DocumentNode.SelectNodes("//table[@class='table table--left table--inner-borders-rows table--striped']//tr") ??
                       doc.DocumentNode.SelectNodes("//table//tr[td]");

        if (tableRows == null)
            return holidays;

        foreach (var row in tableRows)
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 2)
                continue;

            try
            {
                var holiday = ParseHolidayRow(cells, date);
                if (holiday != null)
                    holidays.Add(holiday);
            }
            catch
            {
                // Skip rows that can't be parsed
                continue;
            }
        }

        // If no holidays found in table, try alternative parsing
        if (holidays.Count == 0)
        {
            var holidayNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'holiday')] | //span[contains(@class, 'holiday')]");
            if (holidayNodes != null)
            {
                foreach (var node in holidayNodes)
                {
                    var name = node.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        holidays.Add(new Holiday
                        {
                            Date = date,
                            Name = name,
                            Type = "Holiday",
                            Description = string.Empty,
                            Observances = string.Empty
                        });
                    }
                }
            }
        }

        return holidays;
    }

    private Holiday? ParseHolidayRow(HtmlNodeCollection cells, DateTime date)
    {
        if (cells.Count < 2)
            return null;

        var name = cells[1].InnerText.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var type = cells.Count > 2 ? cells[2].InnerText.Trim() : "Holiday";
        var description = cells.Count > 3 ? cells[3].InnerText.Trim() : string.Empty;
        var observances = cells.Count > 4 ? cells[4].InnerText.Trim() : string.Empty;

        // Try to extract date from first cell if it's different from the requested date
        var holidayDate = date;
        if (cells.Count > 0)
        {
            var dateText = cells[0].InnerText.Trim();
            if (!string.IsNullOrWhiteSpace(dateText) && DateTime.TryParse(dateText, out var parsedDate))
            {
                holidayDate = parsedDate;
            }
        }

        return new Holiday
        {
            Date = holidayDate,
            Name = name,
            Type = type,
            Description = description,
            Observances = observances
        };
    }
}

