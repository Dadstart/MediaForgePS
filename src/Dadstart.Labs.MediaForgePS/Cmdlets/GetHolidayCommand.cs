using System.Management.Automation;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Dadstart.Labs.MediaForgePS.Models;
using Dadstart.Labs.MediaForgePS.Services;

namespace Dadstart.Labs.MediaForgePS.Cmdlets;

/// <summary>
/// Gets holidays for a specified date or date range.
/// </summary>
[Cmdlet(VerbsCommon.Get, "Holiday")]
[OutputType(typeof(Holiday))]
public class GetHolidayCommand : PSCmdlet
{
    private HolidayScraper? _scraper;

    /// <summary>
    /// Initializes resources before processing records.
    /// </summary>
    protected override void BeginProcessing()
    {
        _scraper = ServiceProviderFactory.Current.GetRequiredService<HolidayScraper>();
    }

    /// <summary>
    /// Cleans up resources after processing records.
    /// </summary>
    protected override void EndProcessing()
    {
        if (_scraper is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _scraper = null;
    }

    /// <summary>
    /// Date to retrieve holidays for (SingleDate parameter set).
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "SingleDate")]
    public DateTime Date { get; set; }

    /// <summary>
    /// Start date for date range (DateRange parameter set).
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DateRange")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for date range (DateRange parameter set).
    /// </summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "DateRange")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Processes the cmdlet execution.
    /// </summary>
    protected override void ProcessRecord()
    {
        try
        {
            if (ParameterSetName == "SingleDate")
            {
                var holidays = GetHolidaysAsync(_scraper, Date).GetAwaiter().GetResult();
                foreach (var holiday in holidays)
                {
                    WriteObject(holiday);
                }
            }
            else
            {
                var currentDate = StartDate.Date;
                var endDate = EndDate.Date;

                if (currentDate > endDate)
                {
                    var errorRecord = new ErrorRecord(
                        new ArgumentException("StartDate must be less than or equal to EndDate"),
                        "InvalidDateRange",
                        ErrorCategory.InvalidArgument,
                        null);
                    WriteError(errorRecord);
                    return;
                }

                while (currentDate <= endDate)
                {
                    var holidays = GetHolidaysAsync(_scraper, currentDate).GetAwaiter().GetResult();
                    foreach (var holiday in holidays)
                    {
                        WriteObject(holiday);
                    }
                    currentDate = currentDate.AddDays(1);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            var errorRecord = new ErrorRecord(
                ex,
                "HttpRequestFailed",
                ErrorCategory.ConnectionError,
                null);
            WriteError(errorRecord);
        }
        catch (Exception ex)
        {
            var errorRecord = new ErrorRecord(
                ex,
                "UnexpectedError",
                ErrorCategory.NotSpecified,
                null);
            WriteError(errorRecord);
        }
    }

    private async Task<List<Holiday>> GetHolidaysAsync(HolidayScraper scraper, DateTime date)
    {
        return await scraper.GetHolidaysAsync(date);
    }
}


