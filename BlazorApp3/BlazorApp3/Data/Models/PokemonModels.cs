using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace BlazorApp3.Data.Models
{
    // Pokémon Card Information
    public class PokemonCard
    {
        [Key]
        public string Id { get; set; } = "";

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = "";

        [MaxLength(100)]
        public string Expansion { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string Pack { get; set; } = "";

        [Required]
        [MaxLength(50)]
        public string Rarity { get; set; } = "";

        [MaxLength(20)]
        public string CardNumber { get; set; } = "";

        [MaxLength(500)]
        public string ImageUrl { get; set; } = "";

        [MaxLength(50)]
        public string Type { get; set; } = "";


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<UserCard> UserCards { get; set; } = new List<UserCard>();
        public virtual ICollection<BountyRequest> BountyRequests { get; set; } = new List<BountyRequest>();
        public virtual ICollection<BountyOffer> BountyOffers { get; set; } = new List<BountyOffer>();
    }

    // User's Card Collection
    public class UserCard
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        [Required]
        public string CardId { get; set; } = "";

        public int Quantity { get; set; } = 1;

        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

        public bool IsForTrade { get; set; } = false;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [ForeignKey("CardId")]
        public virtual PokemonCard Card { get; set; } = null!;
    }

    // Bounty Board - Users looking for specific cards
    public class BountyRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        [Required]
        public string CardId { get; set; } = "";

        [MaxLength(500)]
        public string Description { get; set; } = "";

        [Column(TypeName = "decimal(10,2)")]
        public decimal? OfferedAmount { get; set; }

        [MaxLength(200)]
        public string OfferedItem { get; set; } = "";

        public BountyStatus Status { get; set; } = BountyStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [ForeignKey("CardId")]
        public virtual PokemonCard Card { get; set; } = null!;

        public virtual ICollection<BountyOffer> Offers { get; set; } = new List<BountyOffer>();
    }

    // Offers to fulfill bounty requests
    public class BountyOffer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BountyRequestId { get; set; }

        [Required]
        public string OffererId { get; set; } = "";

        [Required]
        public string CardId { get; set; } = "";

        [MaxLength(500)]
        public string Message { get; set; } = "";

        public OfferStatus Status { get; set; } = OfferStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }

        // Navigation properties
        [ForeignKey("BountyRequestId")]
        public virtual BountyRequest BountyRequest { get; set; } = null!;

        [ForeignKey("OffererId")]
        public virtual ApplicationUser Offerer { get; set; } = null!;

        [ForeignKey("CardId")]
        public virtual PokemonCard Card { get; set; } = null!;
    }

    // User statistics and achievements
    public class UserStats
    {
        [Key]
        public string UserId { get; set; } = "";

        public int TotalCards { get; set; } = 0;
        public int UniqueCards { get; set; } = 0;
        public int CompletedSets { get; set; } = 0;
        public int BountiesCompleted { get; set; } = 0;
        public int BountiesPosted { get; set; } = 0;
        public int TradesCompleted { get; set; } = 0;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
    }

    // Enums
    public enum BountyStatus
    {
        Active,
        Completed,
        Cancelled,
        Expired
    }

    public enum OfferStatus
    {
        Pending,
        Accepted,
        Rejected,
        Cancelled
    }

    // DTOs for API responses
    public class CardCollectionDto
    {
        public string CardId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Pack { get; set; } = "";
        public string Rarity { get; set; } = "";
        public string CardNumber { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Expansion { get; set; } = "";
        public string Type { get; set; } = "";
        public int Quantity { get; set; } = 0;
        public bool IsCollected { get; set; } = false;
        public bool IsForTrade { get; set; } = false;
        public DateTime? CollectedAt { get; set; }
    }

    public class BountyRequestDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string CardName { get; set; } = "";
        public string CardImageUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal? OfferedAmount { get; set; }
        public string OfferedItem { get; set; } = "";
        public BountyStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int OfferCount { get; set; } = 0;
    }

    public class UserStatsDto
    {
        public int TotalCards { get; set; }
        public int UniqueCards { get; set; }
        public int CompletedSets { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int BountiesCompleted { get; set; }
        public int BountiesPosted { get; set; }
        public int TradesCompleted { get; set; }
        public int Rank { get; set; }
    }

    // API Request Models
    public class AddCardRequest
    {
        public string CardId { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public bool IsForTrade { get; set; } = false;
    }

    public class CreateBountyRequest
    {
        [Required]
        public string CardId { get; set; } = "";

        [MaxLength(500)]
        public string Description { get; set; } = "";

        public decimal? OfferedAmount { get; set; }

        [MaxLength(200)]
        public string OfferedItem { get; set; } = "";
    }

    public class CreateOfferRequest
    {
        [Required]
        public int BountyRequestId { get; set; }

        [Required]
        public string CardId { get; set; } = "";

        [MaxLength(500)]
        public string Message { get; set; } = "";
    }

    public class BountyOfferDto
    {
        public int Id { get; set; }
        public string OffererName { get; set; } = "";
        public string CardName { get; set; } = "";
        public string CardImageUrl { get; set; } = "";
        public string Message { get; set; } = "";
        public OfferStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }

    public class MyOfferDto
    {
        public int Id { get; set; }
        public int BountyRequestId { get; set; }
        public string RequesterName { get; set; } = "";
        public string RequestedCardName { get; set; } = "";
        public string RequestedCardImageUrl { get; set; } = "";
        public string OfferedCardName { get; set; } = "";
        public string OfferedCardImageUrl { get; set; } = "";
        public string Message { get; set; } = "";
        public OfferStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }

    public class LeaderboardEntry
    {
        public string UserName { get; set; } = "";
        public int UniqueCards { get; set; }
        public int TotalCards { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int BountiesCompleted { get; set; }
        public int TradesCompleted { get; set; }
    }
}