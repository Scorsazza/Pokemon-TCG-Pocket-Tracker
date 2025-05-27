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
    [Authorize]

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

        // GET: api/BountyBoard/requests
        [HttpGet("requests")]
        public async Task<ActionResult<List<BountyRequestDto>>> GetBountyRequests(
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
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(cardName))
            {
                query = query.Where(br => br.Card.Name.Contains(cardName));
            }

            if (!string.IsNullOrEmpty(pack))
            {
                query = query.Where(br => br.Card.Pack == pack);
            }

            if (status.HasValue)
            {
                query = query.Where(br => br.Status == status.Value);
            }
            else
            {
                // Default to active bounties only
                query = query.Where(br => br.Status == BountyStatus.Active);
            }

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

        // GET: api/BountyBoard/my-requests
        [HttpGet("my-requests")]
        public async Task<ActionResult<List<BountyRequestDto>>> GetMyBountyRequests()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var bounties = await _context.BountyRequests
                .Include(br => br.Card)
                .Include(br => br.Offers)
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

        // POST: api/BountyBoard/create-request
        [HttpPost("create-request")]
        public async Task<ActionResult> CreateBountyRequest([FromBody] CreateBountyRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Validate card exists
            var card = await _context.PokemonCards.FindAsync(request.CardId);
            if (card == null)
            {
                return NotFound("Card not found");
            }

            // Check if user already has an active bounty for this card
            var existingBounty = await _context.BountyRequests
                .FirstOrDefaultAsync(br => br.UserId == userId &&
                                          br.CardId == request.CardId &&
                                          br.Status == BountyStatus.Active);

            if (existingBounty != null)
            {
                return BadRequest("You already have an active bounty for this card");
            }

            var bountyRequest = new BountyRequest
            {
                UserId = userId,
                CardId = request.CardId,
                Description = request.Description,
                OfferedAmount = request.OfferedAmount,
                OfferedItem = request.OfferedItem,
                Status = BountyStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _context.BountyRequests.Add(bountyRequest);
            await _context.SaveChangesAsync();
            await _context.UpdateUserStatsAsync(userId);

            return Ok(new { Message = "Bounty request created successfully", BountyId = bountyRequest.Id });
        }

        // GET: api/BountyBoard/request/{id}/offers
        [HttpGet("request/{id}/offers")]
        public async Task<ActionResult<List<BountyOfferDto>>> GetBountyOffers(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Check if user owns this bounty request
            var bountyRequest = await _context.BountyRequests
                .FirstOrDefaultAsync(br => br.Id == id && br.UserId == userId);

            if (bountyRequest == null)
            {
                return NotFound("Bounty request not found or you don't have permission to view offers");
            }

            var offers = await _context.BountyOffers
                .Include(bo => bo.Offerer)
                .Include(bo => bo.Card)
                .Where(bo => bo.BountyRequestId == id)
                .OrderByDescending(bo => bo.CreatedAt)
                .Select(bo => new BountyOfferDto
                {
                    Id = bo.Id,
                    OffererName = bo.Offerer.UserName ?? "Unknown",
                    CardName = bo.Card.Name,
                    CardImageUrl = bo.Card.ImageUrl,
                    Message = bo.Message,
                    Status = bo.Status,
                    CreatedAt = bo.CreatedAt,
                    AcceptedAt = bo.AcceptedAt
                })
                .ToListAsync();

            return Ok(offers);
        }

        // POST: api/BountyBoard/create-offer
        [HttpPost("create-offer")]
        public async Task<ActionResult> CreateBountyOffer([FromBody] CreateOfferRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Validate bounty request exists and is active
            var bountyRequest = await _context.BountyRequests
                .FirstOrDefaultAsync(br => br.Id == request.BountyRequestId && br.Status == BountyStatus.Active);

            if (bountyRequest == null)
            {
                return NotFound("Active bounty request not found");
            }

            // Prevent users from making offers on their own bounties
            if (bountyRequest.UserId == userId)
            {
                return BadRequest("You cannot make an offer on your own bounty request");
            }

            // Validate user has the card they're offering
            var userCard = await _context.UserCards
                .FirstOrDefaultAsync(uc => uc.UserId == userId &&
                                          uc.CardId == request.CardId &&
                                          uc.Quantity > 0);

            if (userCard == null)
            {
                return BadRequest("You don't have this card in your collection");
            }

            // Check if user already made an offer for this bounty
            var existingOffer = await _context.BountyOffers
                .FirstOrDefaultAsync(bo => bo.BountyRequestId == request.BountyRequestId &&
                                          bo.OffererId == userId &&
                                          bo.Status == OfferStatus.Pending);

            if (existingOffer != null)
            {
                return BadRequest("You already have a pending offer for this bounty");
            }

            var bountyOffer = new BountyOffer
            {
                BountyRequestId = request.BountyRequestId,
                OffererId = userId,
                CardId = request.CardId,
                Message = request.Message,
                Status = OfferStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.BountyOffers.Add(bountyOffer);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Offer created successfully", OfferId = bountyOffer.Id });
        }

        // PUT: api/BountyBoard/accept-offer/{offerId}
        [HttpPut("accept-offer/{offerId}")]
        public async Task<ActionResult> AcceptBountyOffer(int offerId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var offer = await _context.BountyOffers
                .Include(bo => bo.BountyRequest)
                .FirstOrDefaultAsync(bo => bo.Id == offerId && bo.BountyRequest.UserId == userId);

            if (offer == null)
            {
                return NotFound("Offer not found or you don't have permission to accept it");
            }

            if (offer.Status != OfferStatus.Pending)
            {
                return BadRequest("This offer has already been processed");
            }

            // Accept the offer
            offer.Status = OfferStatus.Accepted;
            offer.AcceptedAt = DateTime.UtcNow;

            // Complete the bounty request
            offer.BountyRequest.Status = BountyStatus.Completed;
            offer.BountyRequest.CompletedAt = DateTime.UtcNow;

            // Reject all other pending offers for this bounty
            var otherOffers = await _context.BountyOffers
                .Where(bo => bo.BountyRequestId == offer.BountyRequestId &&
                            bo.Id != offerId &&
                            bo.Status == OfferStatus.Pending)
                .ToListAsync();

            foreach (var otherOffer in otherOffers)
            {
                otherOffer.Status = OfferStatus.Rejected;
            }

            await _context.SaveChangesAsync();

            // Update stats for both users
            await _context.UpdateUserStatsAsync(userId);
            await _context.UpdateUserStatsAsync(offer.OffererId);

            return Ok(new { Message = "Offer accepted successfully" });
        }

        // PUT: api/BountyBoard/reject-offer/{offerId}
        [HttpPut("reject-offer/{offerId}")]
        public async Task<ActionResult> RejectBountyOffer(int offerId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var offer = await _context.BountyOffers
                .Include(bo => bo.BountyRequest)
                .FirstOrDefaultAsync(bo => bo.Id == offerId && bo.BountyRequest.UserId == userId);

            if (offer == null)
            {
                return NotFound("Offer not found or you don't have permission to reject it");
            }

            if (offer.Status != OfferStatus.Pending)
            {
                return BadRequest("This offer has already been processed");
            }

            offer.Status = OfferStatus.Rejected;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Offer rejected successfully" });
        }

        // DELETE: api/BountyBoard/cancel-request/{id}
        [HttpDelete("cancel-request/{id}")]
        public async Task<ActionResult> CancelBountyRequest(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var bountyRequest = await _context.BountyRequests
                .FirstOrDefaultAsync(br => br.Id == id && br.UserId == userId);

            if (bountyRequest == null)
            {
                return NotFound("Bounty request not found or you don't have permission to cancel it");
            }

            if (bountyRequest.Status != BountyStatus.Active)
            {
                return BadRequest("Only active bounty requests can be cancelled");
            }

            bountyRequest.Status = BountyStatus.Cancelled;

            // Cancel all pending offers
            var pendingOffers = await _context.BountyOffers
                .Where(bo => bo.BountyRequestId == id && bo.Status == OfferStatus.Pending)
                .ToListAsync();

            foreach (var offer in pendingOffers)
            {
                offer.Status = OfferStatus.Cancelled;
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Bounty request cancelled successfully" });
        }

        // GET: api/BountyBoard/my-offers
        [HttpGet("my-offers")]
        public async Task<ActionResult<List<MyOfferDto>>> GetMyOffers()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var offers = await _context.BountyOffers
                .Include(bo => bo.BountyRequest)
                .ThenInclude(br => br.User)
                .Include(bo => bo.BountyRequest)
                .ThenInclude(br => br.Card)
                .Include(bo => bo.Card)
                .Where(bo => bo.OffererId == userId)
                .OrderByDescending(bo => bo.CreatedAt)
                .Select(bo => new MyOfferDto
                {
                    Id = bo.Id,
                    BountyRequestId = bo.BountyRequestId,
                    RequesterName = bo.BountyRequest.User.UserName ?? "Unknown",
                    RequestedCardName = bo.BountyRequest.Card.Name,
                    RequestedCardImageUrl = bo.BountyRequest.Card.ImageUrl,
                    OfferedCardName = bo.Card.Name,
                    OfferedCardImageUrl = bo.Card.ImageUrl,
                    Message = bo.Message,
                    Status = bo.Status,
                    CreatedAt = bo.CreatedAt,
                    AcceptedAt = bo.AcceptedAt
                })
                .ToListAsync();

            return Ok(offers);
        }
    }
}