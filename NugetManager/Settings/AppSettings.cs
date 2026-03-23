using BeyondWPF.Core.Settings;

namespace NugetManager.Settings;

public class AppSettings : BaseSettings
{
    private string _theme = "Dark";
    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    private bool _isRoundedCorners = true;
    public bool IsRoundedCorners
    {
        get => _isRoundedCorners;
        set => SetProperty(ref _isRoundedCorners, value);
    }

    private bool _useAccentColor = true;
    public bool UseAccentColor
    {
        get => _useAccentColor;
        set => SetProperty(ref _useAccentColor, value);
    }

    private bool _saveWindowPosition = true;
    public bool SaveWindowPosition
    {
        get => _saveWindowPosition;
        set => SetProperty(ref _saveWindowPosition, value);
    }

    private double _windowTop = 100;
    public double WindowTop
    {
        get => _windowTop;
        set => SetProperty(ref _windowTop, value);
    }

    private double _windowLeft = 100;
    public double WindowLeft
    {
        get => _windowLeft;
        set => SetProperty(ref _windowLeft, value);
    }

    private double _windowWidth = 1100;
    public double WindowWidth
    {
        get => _windowWidth;
        set => SetProperty(ref _windowWidth, value);
    }

    private double _windowHeight = 700;
    public double WindowHeight
    {
        get => _windowHeight;
        set => SetProperty(ref _windowHeight, value);
    }

    private string _windowState = "Normal";
    public string WindowState
    {
        get => _windowState;
        set => SetProperty(ref _windowState, value);
    }

    // BaGet config
    private int _bagetPort = 5000;
    public int BaGetPort
    {
        get => _bagetPort;
        set => SetProperty(ref _bagetPort, value);
    }

    private string _packagesFolder = "";
    public string PackagesFolder
    {
        get => _packagesFolder;
        set => SetProperty(ref _packagesFolder, value);
    }

    // Last scanned folder for projects
    private string _lastProjectFolder = "";
    public string LastProjectFolder
    {
        get => _lastProjectFolder;
        set => SetProperty(ref _lastProjectFolder, value);
    }
}
