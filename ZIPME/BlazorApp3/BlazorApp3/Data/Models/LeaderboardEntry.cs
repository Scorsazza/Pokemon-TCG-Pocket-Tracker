namespace BlazorApp3.Data.Models
{
    public class LeaderboardEntryNew
    {
        public string UserName { get; set; } = string.Empty;
        public decimal CompletionPercentage { get; set; }
        public int UniqueCards { get; set; }
        public int TotalCards { get; set; }
        public int BountiesCompleted { get; set; }
        public int TradesCompleted { get; set; }
    }
}