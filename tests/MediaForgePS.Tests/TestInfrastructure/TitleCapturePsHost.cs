using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;

namespace Dadstart.Labs.MediaForge.Tests.TestInfrastructure;

/// <summary>
/// PowerShell host that records <see cref="PSHostRawUserInterface.WindowTitle"/> updates.
/// </summary>
public sealed class TitleCapturePsHost : PSHost
{
    private readonly TitleCaptureUserInterface _ui;
    private readonly Guid _instanceId = Guid.NewGuid();

    public TitleCapturePsHost(string windowTitle)
    {
        _ui = new TitleCaptureUserInterface(windowTitle);
    }

    public string WindowTitle
    {
        get => _ui.TitleRawUi.WindowTitle;
        set => _ui.TitleRawUi.WindowTitle = value;
    }

    public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;

    public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;

    public override Guid InstanceId => _instanceId;

    public override string Name => "TitleCaptureHost";

    public override PSHostUserInterface UI => _ui;

    public override Version Version { get; } = new(1, 0);

    public override void EnterNestedPrompt()
    {
    }

    public override void ExitNestedPrompt()
    {
    }

    public override void NotifyBeginApplication()
    {
    }

    public override void NotifyEndApplication()
    {
    }

    public override void SetShouldExit(int exitCode)
    {
    }

    private sealed class TitleCaptureUserInterface : PSHostUserInterface
    {
        public TitleCaptureUserInterface(string windowTitle)
        {
            TitleRawUi = new TitleCaptureRawUi(windowTitle);
        }

        public TitleCaptureRawUi TitleRawUi { get; }

        public override PSHostRawUserInterface RawUI => TitleRawUi;

        public override string ReadLine() => string.Empty;

        public override SecureString ReadLineAsSecureString() => new();

        public override void Write(string value)
        {
        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
        }

        public override void WriteLine(string value)
        {
        }

        public override void WriteErrorLine(string value)
        {
        }

        public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
        }

        public override void WriteDebugLine(string message)
        {
        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
        }

        public override void WriteVerboseLine(string message)
        {
        }

        public override void WriteWarningLine(string message)
        {
        }

        public override Dictionary<string, PSObject> Prompt(
            string caption,
            string message,
            Collection<FieldDescription> descriptions) => [];

        public override int PromptForChoice(
            string caption,
            string message,
            Collection<ChoiceDescription> choices,
            int defaultChoice) => defaultChoice;

        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName) => new(userName, new SecureString());

        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName,
            PSCredentialTypes allowedCredentialTypes,
            PSCredentialUIOptions options) => new(userName, new SecureString());
    }

    private sealed class TitleCaptureRawUi : PSHostRawUserInterface
    {
        public TitleCaptureRawUi(string windowTitle)
        {
            WindowTitle = windowTitle;
        }

        public override string WindowTitle { get; set; }

        public override ConsoleColor BackgroundColor { get; set; }

        public override ConsoleColor ForegroundColor { get; set; }

        public override Size BufferSize { get; set; } = new(120, 50);

        public override Size WindowSize { get; set; } = new(120, 50);

        public override Size MaxWindowSize => new(120, 50);

        public override Size MaxPhysicalWindowSize => new(120, 50);

        public override Coordinates CursorPosition { get; set; }

        public override Coordinates WindowPosition { get; set; }

        public override int CursorSize { get; set; } = 25;

        public override bool KeyAvailable => false;

        public override void FlushInputBuffer()
        {
        }

        public override KeyInfo ReadKey(ReadKeyOptions options) =>
            throw new NotSupportedException();

        public override BufferCell[,] GetBufferContents(Rectangle rectangle) => new BufferCell[1, 1];

        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill)
        {
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
        }

        public override int LengthInBufferCells(string source) => source.Length;

        public override int LengthInBufferCells(char source) => 1;
    }
}
