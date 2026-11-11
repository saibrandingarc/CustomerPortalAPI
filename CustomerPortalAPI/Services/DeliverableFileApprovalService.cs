using CustomerPortalAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortalAPI.Services
{
    public class DeliverableFileApprovalService
    {
        private readonly ApplicationDbContext _context;

        public DeliverableFileApprovalService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DeliverableFileApproval> UpsertAsync(
            string deliverableId,
            string fileId,
            string fileName,
            string? fileUrl,
            string? notes,
            string status)
        {
            var existing = await _context.DeliverableFileApprovals
                .FirstOrDefaultAsync(x => x.DeliverableId == deliverableId && x.FileId == fileId);

            if (existing != null)
            {
                existing.FileName = fileName;
                existing.FileUrl = fileUrl;
                existing.Notes = notes;
                existing.Status = status;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                existing = new DeliverableFileApproval
                {
                    DeliverableId = deliverableId,
                    FileId = fileId,
                    FileName = fileName,
                    FileUrl = fileUrl,
                    Notes = notes,
                    Status = status,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _context.DeliverableFileApprovals.Add(existing);
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<List<DeliverableFileApproval>> GetByDeliverableIdAsync(string deliverableId)
        {
            return await _context.DeliverableFileApprovals
                .Where(x => x.DeliverableId == deliverableId)
                .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .ToListAsync();
        }

        public async Task<DeliverableFileApproval?> GetByDeliverableAndFileAsync(string deliverableId, string fileId)
        {
            return await _context.DeliverableFileApprovals
                .FirstOrDefaultAsync(x => x.DeliverableId == deliverableId && x.FileId == fileId);
        }
    }
}
