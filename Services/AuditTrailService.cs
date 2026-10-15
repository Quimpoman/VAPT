using EVOLVE_VAPT.Models;
using System.Text.Json;

namespace EVOLVE_VAPT.Services
{
    public class AuditTrailService
    {
        private readonly EVOLVE_VAPTContext _context;

        public AuditTrailService(EVOLVE_VAPTContext context)
        {
            _context = context;
        }

        public async Task AddAsync(
            string? userId,
            string? userName,
            string actionType,
            string module,
            string? tableName,
            string? recordId,
            string? description,
            object? oldValues = null,
            object? newValues = null,
            string? requestPath = null,
            string? ipAddress = null,
            bool isSuccessful = true)
        {
            var audit = new AuditTrail
            {
                UserID = userId,
                UserName = userName,

                ActionType = actionType,
                Module = module,

                TableName = tableName,
                RecordID = recordId,

                Description = description,

                OldValues = oldValues == null
                    ? null
                    : JsonSerializer.Serialize(oldValues),

                NewValues = newValues == null
                    ? null
                    : JsonSerializer.Serialize(newValues),

                RequestPath = requestPath,
                IPAddress = ipAddress,

                IsSuccessful = isSuccessful,

                CreatedDate = DateTime.Now
            };

            await _context.AuditTrail.AddAsync(audit);
        }

        public async Task LogAsync(
    string? userId,
    string? userName,
    string actionType,
    string module,
    string? tableName,
    string? recordId,
    string? description,
    object? oldValues = null,
    object? newValues = null,
    string? requestPath = null,
    string? ipAddress = null,
    bool isSuccessful = true)
        {
            await AddAsync(
                userId,
                userName,
                actionType,
                module,
                tableName,
                recordId,
                description,
                oldValues,
                newValues,
                requestPath,
                ipAddress,
                isSuccessful
            );

            await _context.SaveChangesAsync();
        }
    }
}