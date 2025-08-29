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
    string _progress = string.Empty;
    string _time = string.Empty;
    string _errorDetails = string.Empty;

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
}