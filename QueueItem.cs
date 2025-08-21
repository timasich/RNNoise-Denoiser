public sealed class QueueItem
{
    public bool IsChecked { get; set; }
    public string Input { get; set; }
    public string Output { get; set; }
    public string Status { get; set; }
    public string Progress { get; set; }
    public string Time { get; set; }
    public string ErrorDetails { get; set; }
}