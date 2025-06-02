using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlazorApp3.Data;
using BlazorApp3.Data.Models;
using System.Security.Claims;

namespace BlazorApp3.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BountyBoardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BountyBoardController> _logger;

        public BountyBoardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<BountyBoardController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost("create-request")]
        [IgnoreAntiforgeryToken]
        public async Task<ActionResult> CreateBountyRequest([FromBody] CreateBountyRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to create a bounty.");

            var card = await _context.PokemonCards.FindAsync(request.CardId);
            if (card == null)
                return NotFound("Card not found.");

            var existing = await _context.BountyRequests
                .FirstOrDefaultAsync(br => br.UserId == userId && br.CardId == request.CardId && br.Status == BountyStatus.Active);

            if (existing != null)
                return BadRequest("You already have an active bounty for this card.");

            var bounty = new BountyRequest
            {
                UserId = userId,
                CardId = request.CardId,
                Description = request.Description,
                OfferedAmount = request.OfferedAmount,
                OfferedItem = request.OfferedItem,
                Status = BountyStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _context.BountyRequests.Add(bounty);
            await _context.SaveChangesAsync();
            await _context.UpdateUserStatsAsync(userId);

            return Ok(new { Message = "Bounty request created successfully.", BountyId = bounty.Id });
        }

        [HttpGet("requests")]
        public async Task<ActionResult> GetBountyRequests(
            [FromQuery] string? cardName = null,
            [FromQuery] string? pack = null,
            [FromQuery] BountyStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.BountyRequests
                .Include(br => br.User)
                .Include(br => br.Card)
                .Include(br => br.Offers)
                    .ThenInclude(o => o.Card)
                .Include(br => br.Offers)
                    .ThenInclude(o => o.Offerer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(cardName))
                query = query.Where(br => br.Card.Name.Contains(cardName));

            if (!string.IsNullOrEmpty(pack))
                query = query.Where(br => br.Card.Pack == pack);

            query = query.Where(br => br.Status == (status ?? BountyStatus.Active));

            var totalCount = await query.CountAsync();
            var bounties = await query
                .OrderByDescending(br => br.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(br => new BountyRequestDto
                {
                    Id = br.Id,
                    UserName = br.User.UserName ?? "Unknown",
                    CardName = br.Card.Name,
                    CardImageUrl = br.Card.ImageUrl,
                    Description = br.Description,
                    OfferedAmount = br.OfferedAmount,
                    OfferedItem = br.OfferedItem,
                    Status = br.Status,
                    CreatedAt = br.CreatedAt,
                    OfferCount = br.Offers.Count
                })
                .ToListAsync();

            return Ok(new
            {
                Bounties = bounties,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpPost("create-offer")]
        [IgnoreAntiforgeryToken]
        public async Task<ActionResult> CreateBountyOffer([FromBody] CreateBountyOffer request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to make an offer.");

            var bounty = await _context.BountyRequests
                .FirstOrDefaultAsync(br => br.Id == request.BountyRequestId);

            if (bounty == null)
                return NotFound("Bounty request not found.");

            if (bounty.UserId == userId)
                return BadRequest("You cannot make an offer on your own bounty.");

            if (bounty.Status != BountyStatus.Active)
                return BadRequest("This bounty is no longer active.");

            // Check if user owns the card they're offering
            var userCard = await _context.UserCards
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CardId == request.CardId && uc.Quantity > 0);

            if (userCard == null)
                return BadRequest("You don't own this card or don't have any available.");

            // Check if user already made an offer for this bounty
            var existingOffer = await _context.BountyOffers
                .FirstOrDefaultAsync(bo => bo.BountyRequestId == request.BountyRequestId && bo.OffererId == userId);

            if (existingOffer != null)
                return BadRequest("You have already made an offer for this bounty.");

            var offer = new BountyOffer
            {
                BountyRequestId = request.BountyRequestId,
                OffererId = userId,
                CardId = request.CardId,
                CreatedAt = DateTime.UtcNow,
                Status = OfferStatus.Pending
            };

            _context.BountyOffers.Add(offer);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Offer created successfully.", OfferId = offer.Id });
        }

        [HttpPost("accept-offer/{offerId}")]
        [IgnoreAntiforgeryToken]
        public async Task<ActionResult> AcceptOffer(int offerId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to accept offers.");

            var offer = await _context.BountyOffers
                .Include(o => o.BountyRequest)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
                return NotFound("Offer not found.");

            if (offer.BountyRequest.UserId != userId)
                return Forbid("You can only accept offers on your own bounties.");

            if (offer.Status != OfferStatus.Pending)
                return BadRequest("This offer is no longer pending.");

            if (offer.BountyRequest.Status != BountyStatus.Active)
                return BadRequest("This bounty is no longer active.");

            // Accept the offer
            offer.Status = OfferStatus.Accepted;
            offer.BountyRequest.Status = BountyStatus.Completed;

            // Reject all other pending offers for this bounty
            var otherOffers = await _context.BountyOffers
                .Where(o => o.BountyRequestId == offer.BountyRequestId && o.Id != offerId && o.Status == OfferStatus.Pending)
                .ToListAsync();

            foreach (var otherOffer in otherOffers)
            {
                otherOffer.Status = OfferStatus.Rejected;
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Offer accepted successfully." });
        }

        [HttpPost("reject-offer/{offerId}")]
        [IgnoreAntiforgeryToken]
        public async Task<ActionResult> RejectOffer(int offerId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to reject offers.");

            var offer = await _context.BountyOffers
                .Include(o => o.BountyRequest)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null)
                return NotFound("Offer not found.");

            if (offer.BountyRequest.UserId != userId)
                return Forbid("You can only reject offers on your own bounties.");

            if (offer.Status != OfferStatus.Pending)
                return BadRequest("This offer is no longer pending.");

            offer.Status = OfferStatus.Rejected;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Offer rejected successfully." });
        }

        [HttpDelete("delete-bounty/{bountyId}")]
        [IgnoreAntiforgeryToken]
        public async Task<ActionResult> DeleteBounty(int bountyId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to delete bounties.");

            var bounty = await _context.BountyRequests
                .Include(br => br.Offers)
                .FirstOrDefaultAsync(br => br.Id == bountyId && br.UserId == userId);

            if (bounty == null)
                return NotFound("Bounty not found or you don't have permission to delete it.");

            // Delete associated offers first
            _context.BountyOffers.RemoveRange(bounty.Offers);
            _context.BountyRequests.Remove(bounty);

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Bounty deleted successfully." });
        }

        [HttpGet("my-bounties")]
        public async Task<ActionResult> GetMyBounties()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to view your bounties.");

            var bounties = await _context.BountyRequests
                .Include(br => br.Card)
                .Include(br => br.Offers)
                    .ThenInclude(o => o.Card)
                .Include(br => br.Offers)
                    .ThenInclude(o => o.Offerer)
                .Where(br => br.UserId == userId)
                .OrderByDescending(br => br.CreatedAt)
                .Select(br => new BountyRequestDto
                {
                    Id = br.Id,
                    UserName = "You",
                    CardName = br.Card.Name,
                    CardImageUrl = br.Card.ImageUrl,
                    Description = br.Description,
                    OfferedAmount = br.OfferedAmount,
                    OfferedItem = br.OfferedItem,
                    Status = br.Status,
                    CreatedAt = br.CreatedAt,
                    OfferCount = br.Offers.Count
                })
                .ToListAsync();

            return Ok(bounties);
        }

        [HttpGet("my-offers")]
        public async Task<ActionResult> GetMyOffers()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to view your offers.");

            var offers = await _context.BountyOffers
                .Include(o => o.BountyRequest)
                    .ThenInclude(br => br.Card)
                .Include(o => o.BountyRequest)
                    .ThenInclude(br => br.User)
                .Include(o => o.Card)
                .Where(o => o.OffererId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new BountyOfferDetailDto
                {
                    Id = o.Id,
                    BountyRequestId = o.BountyRequestId,
                    BountyCardName = o.BountyRequest.Card.Name,
                    BountyCardImageUrl = o.BountyRequest.Card.ImageUrl,
                    BountyOwnerName = o.BountyRequest.User.UserName ?? "Unknown",
                    OfferedCardName = o.Card.Name,
                    OfferedCardImageUrl = o.Card.ImageUrl,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            return Ok(offers);
        }
    }

    // DTOs for API responses
    public class CreateBountyOffer
    {
        public int BountyRequestId { get; set; }
        public string CardId { get; set; } = "";
    }

    public class BountyOfferDetailDto
    {
        public int Id { get; set; }
        public int BountyRequestId { get; set; }
        public string BountyCardName { get; set; } = "";
        public string BountyCardImageUrl { get; set; } = "";
        public string BountyOwnerName { get; set; } = "";
        public string OfferedCardName { get; set; } = "";
        public string OfferedCardImageUrl { get; set; } = "";
        public OfferStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}