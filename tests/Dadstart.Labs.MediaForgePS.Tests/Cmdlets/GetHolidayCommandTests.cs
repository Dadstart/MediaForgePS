using Dadstart.Labs.MediaForgePS.Cmdlets;

namespace Dadstart.Labs.MediaForgePS.Tests.Cmdlets;

public class GetHolidayCommandTests
{
    [Fact]
    public void Cmdlet_WithSingleDate_CanBeInstantiated()
    {
        // Arrange & Act
        var cmdlet = new GetHolidayCommand
        {
            Date = new DateTime(2024, 1, 1)
        };

        // Assert
        Assert.NotNull(cmdlet);
        Assert.Equal(new DateTime(2024, 1, 1), cmdlet.Date);
    }

    [Fact]
    public void Cmdlet_WithDateRange_CanBeInstantiated()
    {
        // Arrange & Act
        var cmdlet = new GetHolidayCommand
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 1, 3)
        };

        // Assert
        Assert.NotNull(cmdlet);
        Assert.Equal(new DateTime(2024, 1, 1), cmdlet.StartDate);
        Assert.Equal(new DateTime(2024, 1, 3), cmdlet.EndDate);
    }

    [Fact]
    public void Cmdlet_Properties_CanBeSet()
    {
        // Arrange
        var cmdlet = new GetHolidayCommand();

        // Act
        cmdlet.Date = new DateTime(2024, 7, 4);
        cmdlet.StartDate = new DateTime(2024, 1, 1);
        cmdlet.EndDate = new DateTime(2024, 12, 31);

        // Assert
        Assert.Equal(new DateTime(2024, 7, 4), cmdlet.Date);
        Assert.Equal(new DateTime(2024, 1, 1), cmdlet.StartDate);
        Assert.Equal(new DateTime(2024, 12, 31), cmdlet.EndDate);
    }
}

