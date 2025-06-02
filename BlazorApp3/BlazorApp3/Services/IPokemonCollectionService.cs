using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorApp3.Data.Models;

namespace BlazorApp3.Services
{
    public interface IPokemonCollectionService
    {
        Task<List<PokemonCard>> GetAllCardsAsync();
        Task<List<CardCollectionDto>> GetMyCollectionAsync(string userId, string? pack = null, string? rarity = null, bool? isCollected = null);
        Task<UserStatsDto> GetUserStatsAsync(string userId);
        Task SyncCardsFromApiAsync();
        Task SetQuantityAsync(string userId, string cardId, int quantity);
        Task AddCardAsync(string userId, AddCardRequest req);
        Task RemoveCardAsync(string userId, string cardId);
        Task<List<LeaderboardEntry>> GetLeaderboardAsync(int top = 10, string? packFilter = null);
        Task ToggleTradeAsync(string userId, string cardId);
        Task<List<string>> GetAllExpansionsAsync();
    }
}
