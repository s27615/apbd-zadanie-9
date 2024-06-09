using apbd_9.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using apbd_9.Models;

namespace apbd_9.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripController : ControllerBase
    {
        private readonly Apbd9Context _context;
        public TripController(Apbd9Context context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> AllTrips([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Sortowanie malejąco po dacie rozpoczęcia
            var tripsQuery = _context.Trips
                .Include(e => e.IdCountries)
                .Include(e => e.ClientTrips)
                .ThenInclude(ct => ct.IdClientNavigation)
                .OrderByDescending(e => e.DateFrom);

            // Stronicowanie
            var totalTrips = await tripsQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalTrips / (double)pageSize);

            var trips = await tripsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.Name,
                    e.Description,
                    e.DateFrom,
                    e.DateTo,
                    e.MaxPeople,
                    Countries = e.IdCountries.Select(c => new
                    {
                        c.Name
                    }),
                    Clients = e.ClientTrips.Select(ct => new
                    {
                        ct.IdClientNavigation.FirstName,
                        ct.IdClientNavigation.LastName
                    })
                })
                .ToListAsync();

            var result = new
            {
                pageNum = page,
                pageSize = pageSize,
                allPages = totalPages,
                trips
            };

            return Ok(result);
        }
        
        [HttpDelete("/api/clients/{idClient}")]
        public async Task<IActionResult> DeleteClient(int idClient)
        {
            var client = await _context.Clients
                .Include(c => c.ClientTrips)
                .FirstOrDefaultAsync(c => c.IdClient == idClient);

            if (client == null)
            {
                return NotFound("Client not found");
            }

            if (client.ClientTrips.Count != 0)
            {
                return BadRequest("Client has trips assigned");
            }

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            return Ok("Client deleted successfully");
        }

        [HttpPost("/api/trips/{idTrip}/clients")]
        public async Task<IActionResult> AssignTripToClient(string firstName, string lastName, string email, string telephone, string pesel, int idTrip, string tripName, string paymentDate)
        {
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Pesel == pesel);
    
            if (client == null)
            {
                return BadRequest("Client with provided PESEL does not exist!");
            }

            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.IdTrip == idTrip);
    
            if (trip == null)
            {
                return BadRequest("Trip with provided IdTrip does not exist!");
            }

            var existingClientTrip = await _context.ClientTrips.FirstOrDefaultAsync(ct => ct.IdClient == client.IdClient && ct.IdTrip == idTrip);
            if (existingClientTrip != null)
            {
                return BadRequest("Client is already assigned to this trip");
            }

            if (trip.DateFrom < DateTime.Now)
            {
                return BadRequest("This trip has already started or ended!");
            }

            // Tworzenie nowego ClientTrip
            var clientTrip = new ClientTrip
            {
                IdClient = client.IdClient,
                IdTrip = idTrip,
                PaymentDate = string.IsNullOrEmpty(paymentDate) ? null : DateTime.Parse(paymentDate),
                RegisteredAt = DateTime.Now
            };

            _context.ClientTrips.Add(clientTrip);
            await _context.SaveChangesAsync();
            
            return Ok("Assigned client to the trip successfully");
        }
    }
}