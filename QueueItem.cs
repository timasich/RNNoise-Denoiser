using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RNNoise_Denoiser;

public sealed class QueueItem : INotifyPropertyChanged
{
    bool _isChecked;
    string _input = string.Empty;
    string _output = string.Empty;
    string _status = string.Empty;
    string _statusKey = string.Empty; // Invariant status id for localization
    string _progress = string.Empty;
    string _time = string.Empty;
    string _errorDetails = string.Empty;
    int? _errorCode;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    public string Input
    {
        get => _input;
        set => SetField(ref _input, value);
    }

    public string Output
    {
        get => _output;
        set => SetField(ref _output, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    // Invariant status identifier (e.g. "Queued", "Preparing", "Processing...", "Done", "Cancelled", "Error")
    public string StatusKey
    {
        get => _statusKey;
        set
        {
            if (SetField(ref _statusKey, value))
            {
                // When key changes, update localized Status too
                RelocalizeStatus();
            }
        }
    }

    public string Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public string Time
    {
        get => _time;
        set => SetField(ref _time, value);
    }

    public string ErrorDetails
    {
        get => _errorDetails;
        set => SetField(ref _errorDetails, value);
    }

    public int? ErrorCode
    {
        get => _errorCode;
        set => SetField(ref _errorCode, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public void RelocalizeStatus()
    {
        if (string.IsNullOrWhiteSpace(_statusKey))
            return;

        // Translate common statuses via Localizer
        var localized = Localizer.Tr(_statusKey);
        if (_statusKey == "Error" && _errorCode.HasValue)
            localized = $"{localized} ({_errorCode.Value})";
        Status = localized;
    }
}