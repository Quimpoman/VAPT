using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.AuditTrail;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class AuditTrailQueryService
    {
        private readonly EVOLVE_VAPTContext
            _context;


        public AuditTrailQueryService(
            EVOLVE_VAPTContext context)
        {
            _context =
                context;
        }


        public async Task<AuditTrailIndexViewModel>
            GetAuditTrailAsync(
                AuditTrailIndexViewModel model)
        {
            /*
             * Keep the page size bounded.
             * AuditTrail can become a very large table.
             */
            model.PageSize =
                Math.Clamp(
                    model.PageSize,
                    10,
                    100);


            model.Page =
                Math.Max(
                    model.Page,
                    1);


            var query =
                _context
                    .AuditTrail
                    .AsNoTracking()
                    .AsQueryable();


            // SEARCH
            if (!string.IsNullOrWhiteSpace(
                    model.Search))
            {
                var search =
                    model.Search.Trim();


                query =
                    query.Where(x =>
                        (x.Description != null &&
                         x.Description.Contains(search)) ||

                        (x.UserName != null &&
                         x.UserName.Contains(search)) ||

                        x.ActionType.Contains(search) ||

                        x.Module.Contains(search) ||

                        (x.TableName != null &&
                         x.TableName.Contains(search)) ||

                        (x.RecordID != null &&
                         x.RecordID.Contains(search)) ||

                        (x.IPAddress != null &&
                         x.IPAddress.Contains(search)) ||

                        (x.RequestPath != null &&
                         x.RequestPath.Contains(search)));
            }


            // USER
            if (!string.IsNullOrWhiteSpace(
                    model.UserName))
            {
                query =
                    query.Where(
                        x =>
                            x.UserName ==
                            model.UserName);
            }


            // MODULE
            if (!string.IsNullOrWhiteSpace(
                    model.Module))
            {
                query =
                    query.Where(
                        x =>
                            x.Module ==
                            model.Module);
            }


            // ACTION TYPE
            if (!string.IsNullOrWhiteSpace(
                    model.ActionType))
            {
                query =
                    query.Where(
                        x =>
                            x.ActionType ==
                            model.ActionType);
            }


            // SUCCESS / FAILURE
            if (model.IsSuccessful.HasValue)
            {
                query =
                    query.Where(
                        x =>
                            x.IsSuccessful ==
                            model.IsSuccessful.Value);
            }


            // DATE FROM
            if (model.DateFrom.HasValue)
            {
                var dateFrom =
                    model.DateFrom.Value.Date;


                query =
                    query.Where(
                        x =>
                            x.CreatedDate >=
                            dateFrom);
            }


            // DATE TO
            if (model.DateTo.HasValue)
            {
                /*
                 * Use "< next day" rather than <= 23:59:59.
                 * This avoids precision problems.
                 */
                var dateToExclusive =
                    model.DateTo
                        .Value
                        .Date
                        .AddDays(1);


                query =
                    query.Where(
                        x =>
                            x.CreatedDate <
                            dateToExclusive);
            }


            model.TotalCount =
                await query.CountAsync();


            /*
             * If the requested page is now beyond the
             * result set, return the last valid page.
             */
            var totalPages =
                model.TotalCount == 0
                    ? 1
                    : (int)Math.Ceiling(
                        model.TotalCount /
                        (double)model.PageSize);


            if (model.Page > totalPages)
            {
                model.Page =
                    totalPages;
            }


            model.Items =
                await query
                    .OrderByDescending(
                        x => x.CreatedDate)
                    .ThenByDescending(
                        x => x.AuditTrailID)
                    .Skip(
                        (model.Page - 1) *
                        model.PageSize)
                    .Take(
                        model.PageSize)
                    .Select(
                        x =>
                            new AuditTrailListItemViewModel
                            {
                                AuditTrailID =
                                    x.AuditTrailID,

                                CreatedDate =
                                    x.CreatedDate,

                                UserName =
                                    x.UserName,

                                ActionType =
                                    x.ActionType,

                                Module =
                                    x.Module,

                                Description =
                                    x.Description,

                                TableName =
                                    x.TableName,

                                RecordID =
                                    x.RecordID,

                                IPAddress =
                                    x.IPAddress,

                                IsSuccessful =
                                    x.IsSuccessful
                            })
                    .ToListAsync();


            /*
             * Populate filter drop-downs from the
             * complete AuditTrail dataset rather than
             * only the currently filtered result.
             */
            model.Users =
                await _context
                    .AuditTrail
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.UserName != null &&
                            x.UserName != "")
                    .Select(
                        x => x.UserName!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();


            model.Modules =
                await _context
                    .AuditTrail
                    .AsNoTracking()
                    .Select(
                        x => x.Module)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();


            model.ActionTypes =
                await _context
                    .AuditTrail
                    .AsNoTracking()
                    .Select(
                        x => x.ActionType)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();


            return model;
        }
        public async Task<AuditTrailDetailsViewModel?>
    GetAuditTrailDetailsAsync(
        long auditTrailId)
        {
            return await _context
                .AuditTrail
                .AsNoTracking()
                .Where(
                    x =>
                        x.AuditTrailID ==
                        auditTrailId)
                .Select(
                    x =>
                        new AuditTrailDetailsViewModel
                        {
                            AuditTrailID =
                                x.AuditTrailID,

                            UserID =
                                x.UserID,

                            UserName =
                                x.UserName,

                            ActionType =
                                x.ActionType,

                            Module =
                                x.Module,

                            TableName =
                                x.TableName,

                            RecordID =
                                x.RecordID,

                            Description =
                                x.Description,

                            OldValues =
                                x.OldValues,

                            NewValues =
                                x.NewValues,

                            RequestPath =
                                x.RequestPath,

                            IPAddress =
                                x.IPAddress,

                            IsSuccessful =
                                x.IsSuccessful,

                            CreatedDate =
                                x.CreatedDate
                        })
                .FirstOrDefaultAsync();
        }
    }
}