using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.ScanProfile;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class ScanProfileService
    {
        private readonly EVOLVE_VAPTContext _context;
        private readonly AuditTrailService _auditTrailService;


        public ScanProfileService(
            EVOLVE_VAPTContext context,
            AuditTrailService auditTrailService)
        {
            _context = context;
            _auditTrailService = auditTrailService;
        }
        /// <summary>
        /// Returns all active Scan Profiles for the listing page.
        /// </summary>
        public async Task<List<ScanProfileListViewModel>>
            GetScanProfilesAsync()
        {
            return await _context.vwGet_ScanProfileData

                .AsNoTracking()

                .Where(x =>
                    x.IsActive == true)

                .OrderBy(x =>
                    x.ProfileName)

                .Select(x =>
                    new ScanProfileListViewModel
                    {
                        ScanProfileID =
                            x.ScanProfileID,

                        ProfileCode =
                            x.ProfileCode,

                        ProfileName =
                            x.ProfileName,

                        ScannerTypeCode =
                            x.ScannerTypeCode,

                        ScannerTypeName =
                            x.ScannerTypeName,

                        IsIntrusive =
                            x.IsIntrusive,

                        RequiresExplicitApproval =
                            x.RequiresExplicitApproval,

                        TimeoutMinutes =
                            x.TimeoutMinutes,

                        MaxConcurrency =
                            x.MaxConcurrency,

                        CreatedByEmail =
                            x.CreatedByEmail,

                        CreatedDate =
                            x.CreatedDate
                    })

                .ToListAsync();
        }
        /// <summary>
        /// Loads active scanner engines for Create/Edit forms.
        /// </summary>
        public async Task PopulateFormOptionsAsync(
            ScanProfileFormViewModel model)
        {
            model.ScannerTypeOptions =
                await _context.ref_ScannerType

                    .AsNoTracking()

                    .Where(x =>
                        x.IsActive == true)

                    .OrderBy(x =>
                        x.ScannerTypeName)

                    .Select(x =>
                        new SelectListItem
                        {
                            Value =
                                x.ScannerTypeID.ToString(),

                            Text =
                                x.ScannerTypeName
                        })

                    .ToListAsync();
        }
        /// <summary>
        /// Creates a controlled reusable scanner configuration.
        ///
        /// Scanner-specific options will be configured separately.
        /// No raw command or arbitrary CLI arguments are stored here.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, int? ScanProfileID)>
            CreateScanProfileAsync(
                ScanProfileFormViewModel model,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var profileCode =
                model.ProfileCode.Trim();

            var profileName =
                model.ProfileName.Trim();

            var description =
                model.Description?.Trim();


            // Only active registered scanners may be assigned
            // to a Scan Profile.
            var scannerType =
                await _context.ref_ScannerType
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScannerTypeID ==
                            model.ScannerTypeID &&
                        x.IsActive == true);


            if (scannerType == null)
            {
                return (
                    false,
                    "The selected scanner is invalid or inactive.",
                    null
                );
            }


            // ProfileCode is intended to be a stable
            // application/business identifier.
            var duplicateCode =
                await _context.mst_ScanProfile
                    .AnyAsync(x =>
                        x.ProfileCode == profileCode);


            if (duplicateCode)
            {
                return (
                    false,
                    "A Scan Profile with this Profile Code already exists.",
                    null
                );
            }


            /*
             * The ViewModel should already reject this,
             * but the service enforces the business rule too.
             *
             * Service-side enforcement protects callers other
             * than the MVC form.
             */
            if (model.IsIntrusive &&
                !model.RequiresExplicitApproval)
            {
                return (
                    false,
                    "Intrusive profiles must require explicit approval.",
                    null
                );
            }


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                var scanProfile =
                    new mst_ScanProfile
                    {
                        ScannerTypeID =
                            model.ScannerTypeID,

                        ProfileCode =
                            profileCode,

                        ProfileName =
                            profileName,

                        Description =
                            description,

                        IsIntrusive =
                            model.IsIntrusive,

                        RequiresExplicitApproval =
                            model.RequiresExplicitApproval,

                        TimeoutMinutes =
                            model.TimeoutMinutes,

                        MaxConcurrency =
                            model.MaxConcurrency,

                        IsActive =
                            true,

                        CreatedBy =
                            userId,

                        CreatedDate =
                            DateTime.Now
                    };


                _context.mst_ScanProfile.Add(
                    scanProfile);


                /*
                 * Save first so SQL Server generates
                 * ScanProfileID for the AuditTrail record.
                 */
                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId:
                        userId,

                    userName:
                        userName,

                    actionType:
                        "CREATE",

                    module:
                        "Scan Profiles",

                    tableName:
                        "mst_ScanProfile",

                    recordId:
                        scanProfile
                            .ScanProfileID
                            .ToString(),

                    description:
                        $"Created Scan Profile '{scanProfile.ProfileName}' for scanner {scannerType.ScannerTypeName}.",

                    oldValues:
                        null,

                    newValues:
                        new
                        {
                            scanProfile.ScanProfileID,
                            scanProfile.ProfileCode,
                            scanProfile.ProfileName,

                            ScannerType =
                                scannerType.ScannerTypeCode,

                            scanProfile.IsIntrusive,
                            scanProfile.RequiresExplicitApproval,
                            scanProfile.TimeoutMinutes,
                            scanProfile.MaxConcurrency,
                            scanProfile.IsActive
                        },

                    requestPath:
                        requestPath,

                    ipAddress:
                        ipAddress,

                    isSuccessful:
                        true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null,
                    scanProfile.ScanProfileID
                );
            }
            catch
            {
                // Profile creation and its audit record
                // must behave as one logical operation.
                await transaction.RollbackAsync();

                throw;
            }
        }


        /// <summary>
        /// Loads one active Scan Profile together with all
        /// available options for its selected scanner.
        /// </summary>
        public async Task<ScanProfileDetailsViewModel?>
            GetScanProfileDetailsAsync(int scanProfileId)
        {
            var profile =
                await _context.vwGet_ScanProfileData
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScanProfileID == scanProfileId &&
                        x.IsActive == true);


            if (profile == null)
            {
                return null;
            }


            // Load all active options that belong to the
            // scanner used by this profile.
            var scannerOptions =
                await _context.ref_ScannerOption
                    .AsNoTracking()
                    .Where(x =>
                        x.ScannerTypeID == profile.ScannerTypeID &&
                        x.IsActive == true)
                    .OrderBy(x => x.OptionName)
                    .ToListAsync();


            // Retrieve the values already configured for
            // this specific profile.
            var configuredOptions =
                await _context.trn_ScanProfileOption
                    .AsNoTracking()
                    .Where(x =>
                        x.ScanProfileID == scanProfileId &&
                        x.IsActive == true)
                    .ToDictionaryAsync(
                        x => x.ScannerOptionID);


            var model =
                new ScanProfileDetailsViewModel
                {
                    ScanProfileID =
                        profile.ScanProfileID,

                    ScannerTypeID =
                        profile.ScannerTypeID,

                    ProfileCode =
                        profile.ProfileCode,

                    ProfileName =
                        profile.ProfileName,

                    ScannerTypeCode =
                        profile.ScannerTypeCode,

                    ScannerTypeName =
                        profile.ScannerTypeName,

                    Description =
                        profile.Description,

                    IsIntrusive =
                        profile.IsIntrusive,

                    RequiresExplicitApproval =
                        profile.RequiresExplicitApproval,

                    TimeoutMinutes =
                        profile.TimeoutMinutes,

                    MaxConcurrency =
                        profile.MaxConcurrency,

                    CreatedByEmail =
                        profile.CreatedByEmail,

                    CreatedDate =
                        profile.CreatedDate
                };


            foreach (var scannerOption in scannerOptions)
            {
                configuredOptions.TryGetValue(
                    scannerOption.ScannerOptionID,
                    out var configuredOption);


                model.Options.Add(
                    new ScanProfileOptionViewModel
                    {
                        ScannerOptionID =
                            scannerOption.ScannerOptionID,

                        OptionCode =
                            scannerOption.OptionCode,

                        OptionName =
                            scannerOption.OptionName,

                        Description =
                            scannerOption.Description,

                        ValueType =
                            scannerOption.ValueType,

                        DefaultValue =
                            scannerOption.DefaultValue,

                        IsIntrusive =
                            scannerOption.IsIntrusive,

                        IsConfigured =
                            configuredOption != null,

                        /*
                         * If the profile has not configured the option yet,
                         * display the scanner option's default value.
                         */
                        OptionValue =
                            configuredOption?.OptionValue
                            ?? scannerOption.DefaultValue
                    });
            }


            return model;
        }


        /// <summary>
        /// Normalizes and validates an option value according
        /// to the option's declared ValueType.
        /// </summary>
        private static (bool Success, string? ErrorMessage, string? Value) ValidateOptionValue(
                string valueType,
                string? optionValue)
        {
            var value =
                optionValue?.Trim();


            switch (valueType.ToUpperInvariant())
            {
                case "BOOLEAN":
                    {
                        if (!bool.TryParse(
                                value,
                                out var booleanValue))
                        {
                            return (
                                false,
                                "The option requires a True or False value.",
                                null
                            );
                        }


                        // Store BOOLEAN values consistently.
                        return (
                            true,
                            null,
                            booleanValue
                                ? "true"
                                : "false"
                        );
                    }


                case "INTEGER":
                    {
                        if (!int.TryParse(
                                value,
                                out var integerValue))
                        {
                            return (
                                false,
                                "The option requires a whole-number value.",
                                null
                            );
                        }


                        return (
                            true,
                            null,
                            integerValue.ToString()
                        );
                    }


                case "TEXT":
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            return (
                                false,
                                "The option requires a value.",
                                null
                            );
                        }


                        if (value.Length > 500)
                        {
                            return (
                                false,
                                "The option value cannot exceed 500 characters.",
                                null
                            );
                        }


                        return (
                            true,
                            null,
                            value
                        );
                    }


                default:
                    return (
                        false,
                        $"Unsupported scanner option value type '{valueType}'.",
                        null
                    );
            }
        }


        /// <summary>
        /// Creates or updates one controlled option on a Scan Profile.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)>
            SaveScanProfileOptionAsync(
                int scanProfileId,
                int scannerOptionId,
                string? optionValue,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var profile =
                await _context.mst_ScanProfile
                    .FirstOrDefaultAsync(x =>
                        x.ScanProfileID == scanProfileId &&
                        x.IsActive == true);


            if (profile == null)
            {
                return (
                    false,
                    "Scan Profile could not be found."
                );
            }


            var scannerOption =
                await _context.ref_ScannerOption
                    .FirstOrDefaultAsync(x =>
                        x.ScannerOptionID == scannerOptionId &&
                        x.IsActive == true);


            if (scannerOption == null)
            {
                return (
                    false,
                    "Scanner option could not be found."
                );
            }


            /*
             * Prevent options from another scanner from being
             * attached to this profile.
             *
             * Example:
             * An Nmap option cannot be attached to a ZAP profile.
             */
            if (scannerOption.ScannerTypeID !=
                profile.ScannerTypeID)
            {
                return (
                    false,
                    "The selected option does not belong to this profile's scanner."
                );
            }


            /*
             * An intrusive option cannot silently turn a standard
             * profile into an intrusive configuration.
             */
            if (scannerOption.IsIntrusive &&
                (!profile.IsIntrusive ||
                 !profile.RequiresExplicitApproval))
            {
                return (
                    false,
                    "Intrusive scanner options require an intrusive profile with explicit approval enabled."
                );
            }


            var validation =
                ValidateOptionValue(
                    scannerOption.ValueType,
                    optionValue);


            if (!validation.Success)
            {
                return (
                    false,
                    validation.ErrorMessage
                );
            }


            /*
             * Retrieve even inactive records because the database
             * unique constraint allows only one row per
             * ScanProfileID + ScannerOptionID combination.
             */
            var existingOption =
                await _context.trn_ScanProfileOption
                    .FirstOrDefaultAsync(x =>
                        x.ScanProfileID == scanProfileId &&
                        x.ScannerOptionID == scannerOptionId);


            var isNew =
                existingOption == null;


            var oldValues =
                existingOption == null
                    ? null
                    : new
                    {
                        existingOption.OptionValue,
                        existingOption.IsActive
                    };


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                if (existingOption == null)
                {
                    existingOption =
                        new trn_ScanProfileOption
                        {
                            ScanProfileID =
                                scanProfileId,

                            ScannerOptionID =
                                scannerOptionId,

                            OptionValue =
                                validation.Value,

                            IsActive =
                                true,

                            CreatedBy =
                                userId,

                            CreatedDate =
                                DateTime.Now
                        };


                    _context.trn_ScanProfileOption.Add(
                        existingOption);
                }
                else
                {
                    existingOption.OptionValue =
                        validation.Value;

                    existingOption.IsActive =
                        true;

                    existingOption.EditedBy =
                        userId;

                    existingOption.EditedDate =
                        DateTime.Now;
                }


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId:
                        userId,

                    userName:
                        userName,

                    actionType:
                        isNew
                            ? "CREATE"
                            : "UPDATE",

                    module:
                        "Scan Profiles",

                    tableName:
                        "trn_ScanProfileOption",

                    recordId:
                        existingOption
                            .ScanProfileOptionID
                            .ToString(),

                    description:
                        $"Configured option '{scannerOption.OptionName}' for Scan Profile '{profile.ProfileName}'.",

                    oldValues:
                        oldValues,

                    newValues:
                        new
                        {
                            existingOption.ScanProfileID,
                            existingOption.ScannerOptionID,
                            Option =
                                scannerOption.OptionCode,
                            existingOption.OptionValue,
                            existingOption.IsActive
                        },

                    requestPath:
                        requestPath,

                    ipAddress:
                        ipAddress,

                    isSuccessful:
                        true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null
                );
            }
            catch
            {
                // Option configuration and its audit entry
                // must succeed or fail together.
                await transaction.RollbackAsync();

                throw;
            }
        }


        /// <summary>
        /// Loads an active Scan Profile for editing.
        /// </summary>
        public async Task<ScanProfileFormViewModel?>
            GetScanProfileForEditAsync(int scanProfileId)
        {
            var profile =
                await _context.mst_ScanProfile
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScanProfileID == scanProfileId &&
                        x.IsActive == true);


            if (profile == null)
            {
                return null;
            }


            var model =
                new ScanProfileFormViewModel
                {
                    ScanProfileID =
                        profile.ScanProfileID,

                    ScannerTypeID =
                        profile.ScannerTypeID,

                    ProfileCode =
                        profile.ProfileCode,

                    ProfileName =
                        profile.ProfileName,

                    Description =
                        profile.Description,

                    IsIntrusive =
                        profile.IsIntrusive,

                    RequiresExplicitApproval =
                        profile.RequiresExplicitApproval,

                    TimeoutMinutes =
                        profile.TimeoutMinutes,

                    MaxConcurrency =
                        profile.MaxConcurrency
                };


            await PopulateFormOptionsAsync(model);


            return model;
        }


        /// <summary>
        /// Updates a Scan Profile.
        ///
        /// Scanner-specific options are invalidated if the
        /// scanner type changes because those options belong
        /// to the previous scanner.
        /// </summary>
        public async Task<(
            bool Success,
            string? ErrorMessage,
            bool OptionsReset)>
            UpdateScanProfileAsync(
                ScanProfileFormViewModel model,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var profile =
                await _context.mst_ScanProfile
                    .FirstOrDefaultAsync(x =>
                        x.ScanProfileID == model.ScanProfileID &&
                        x.IsActive == true);


            if (profile == null)
            {
                return (
                    false,
                    "Scan Profile could not be found.",
                    false
                );
            }


            var scannerType =
                await _context.ref_ScannerType
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScannerTypeID == model.ScannerTypeID &&
                        x.IsActive == true);


            if (scannerType == null)
            {
                return (
                    false,
                    "The selected scanner is invalid or inactive.",
                    false
                );
            }


            var profileCode =
                model.ProfileCode.Trim();

            var profileName =
                model.ProfileName.Trim();

            var description =
                model.Description?.Trim();


            // Prevent duplicate stable Profile Codes.
            var duplicateCode =
                await _context.mst_ScanProfile
                    .AnyAsync(x =>
                        x.ScanProfileID != model.ScanProfileID &&
                        x.ProfileCode == profileCode);


            if (duplicateCode)
            {
                return (
                    false,
                    "A Scan Profile with this Profile Code already exists.",
                    false
                );
            }


            if (model.IsIntrusive &&
                !model.RequiresExplicitApproval)
            {
                return (
                    false,
                    "Intrusive profiles must require explicit approval.",
                    false
                );
            }


            var scannerChanged =
                profile.ScannerTypeID !=
                    model.ScannerTypeID;


            var oldValues = new
            {
                profile.ScannerTypeID,
                profile.ProfileCode,
                profile.ProfileName,
                profile.Description,
                profile.IsIntrusive,
                profile.RequiresExplicitApproval,
                profile.TimeoutMinutes,
                profile.MaxConcurrency
            };


            var hasChanges =
                scannerChanged ||

                !string.Equals(
                    profile.ProfileCode,
                    profileCode,
                    StringComparison.Ordinal) ||

                !string.Equals(
                    profile.ProfileName,
                    profileName,
                    StringComparison.Ordinal) ||

                !string.Equals(
                    profile.Description,
                    description,
                    StringComparison.Ordinal) ||

                profile.IsIntrusive !=
                    model.IsIntrusive ||

                profile.RequiresExplicitApproval !=
                    model.RequiresExplicitApproval ||

                profile.TimeoutMinutes !=
                    model.TimeoutMinutes ||

                profile.MaxConcurrency !=
                    model.MaxConcurrency;


            if (!hasChanges)
            {
                return (
                    false,
                    "No changes were detected.",
                    false
                );
            }


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                profile.ScannerTypeID =
                    model.ScannerTypeID;

                profile.ProfileCode =
                    profileCode;

                profile.ProfileName =
                    profileName;

                profile.Description =
                    description;

                profile.IsIntrusive =
                    model.IsIntrusive;

                profile.RequiresExplicitApproval =
                    model.RequiresExplicitApproval;

                profile.TimeoutMinutes =
                    model.TimeoutMinutes;

                profile.MaxConcurrency =
                    model.MaxConcurrency;

                profile.EditedBy =
                    userId;

                profile.EditedDate =
                    DateTime.Now;


                var optionsReset = false;


                /*
                 * Scanner options are scanner-specific.
                 *
                 * If MOCK becomes NMAP, old MOCK option assignments
                 * must not remain active.
                 */
                if (scannerChanged)
                {
                    var activeOptions =
                        await _context.trn_ScanProfileOption
                            .Where(x =>
                                x.ScanProfileID ==
                                    profile.ScanProfileID &&
                                x.IsActive == true)
                            .ToListAsync();


                    foreach (var option in activeOptions)
                    {
                        option.IsActive = false;
                        option.EditedBy = userId;
                        option.EditedDate = DateTime.Now;
                    }


                    optionsReset =
                        activeOptions.Count > 0;
                }
                else if (!model.IsIntrusive)
                {
                    /*
                     * A profile that becomes non-intrusive cannot
                     * retain intrusive scanner options.
                     */
                    var intrusiveOptions =
                        await _context.trn_ScanProfileOption

                            .Where(x =>
                                x.ScanProfileID ==
                                    profile.ScanProfileID &&
                                x.IsActive == true &&
                                x.ScannerOption.IsIntrusive == true)

                            .ToListAsync();


                    foreach (var option in intrusiveOptions)
                    {
                        option.IsActive = false;
                        option.EditedBy = userId;
                        option.EditedDate = DateTime.Now;
                    }


                    optionsReset =
                        intrusiveOptions.Count > 0;
                }


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId:
                        userId,

                    userName:
                        userName,

                    actionType:
                        "UPDATE",

                    module:
                        "Scan Profiles",

                    tableName:
                        "mst_ScanProfile",

                    recordId:
                        profile.ScanProfileID.ToString(),

                    description:
                        $"Updated Scan Profile '{profile.ProfileName}'.",

                    oldValues:
                        oldValues,

                    newValues:
                        new
                        {
                            profile.ScannerTypeID,
                            ScannerType =
                                scannerType.ScannerTypeCode,
                            profile.ProfileCode,
                            profile.ProfileName,
                            profile.Description,
                            profile.IsIntrusive,
                            profile.RequiresExplicitApproval,
                            profile.TimeoutMinutes,
                            profile.MaxConcurrency
                        },

                    requestPath:
                        requestPath,

                    ipAddress:
                        ipAddress,

                    isSuccessful:
                        true
                );


                if (optionsReset)
                {
                    await _auditTrailService.AddAsync(
                        userId:
                            userId,

                        userName:
                            userName,

                        actionType:
                            "RESET_OPTIONS",

                        module:
                            "Scan Profiles",

                        tableName:
                            "trn_ScanProfileOption",

                        recordId:
                            profile.ScanProfileID.ToString(),

                        description:
                            $"Scanner options for Scan Profile '{profile.ProfileName}' were deactivated because the profile configuration changed.",

                        oldValues:
                            null,

                        newValues:
                            new
                            {
                                profile.ScanProfileID,
                                OptionsActive = false
                            },

                        requestPath:
                            requestPath,

                        ipAddress:
                            ipAddress,

                        isSuccessful:
                            true
                    );
                }


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null,
                    optionsReset
                );
            }
            catch
            {
                // Profile changes and option invalidation must
                // succeed or fail together.
                await transaction.RollbackAsync();

                throw;
            }
        }

        /// <summary>
        /// Soft-deactivates a Scan Profile and its active
        /// option assignments.
        ///
        /// Historical records remain available for AuditTrail
        /// and future Scan Job history.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)>
            DeactivateScanProfileAsync(
                int scanProfileId,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var profile =
                await _context.mst_ScanProfile
                    .FirstOrDefaultAsync(x =>
                        x.ScanProfileID == scanProfileId &&
                        x.IsActive == true);


            if (profile == null)
            {
                return (
                    false,
                    "Scan Profile could not be found."
                );
            }


            var activeOptions =
                await _context.trn_ScanProfileOption
                    .Where(x =>
                        x.ScanProfileID == scanProfileId &&
                        x.IsActive == true)
                    .ToListAsync();


            var oldValues = new
            {
                profile.ScanProfileID,
                profile.ProfileCode,
                profile.ProfileName,
                profile.IsActive
            };


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                // Preserve the profile record but prevent future use.
                profile.IsActive = false;
                profile.EditedBy = userId;
                profile.EditedDate = DateTime.Now;


                // Option assignments belonging to an inactive profile
                // are no longer considered active configuration.
                foreach (var option in activeOptions)
                {
                    option.IsActive = false;
                    option.EditedBy = userId;
                    option.EditedDate = DateTime.Now;
                }


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId:
                        userId,

                    userName:
                        userName,

                    actionType:
                        "DEACTIVATE",

                    module:
                        "Scan Profiles",

                    tableName:
                        "mst_ScanProfile",

                    recordId:
                        profile.ScanProfileID.ToString(),

                    description:
                        $"Deactivated Scan Profile '{profile.ProfileName}'.",

                    oldValues:
                        oldValues,

                    newValues:
                        new
                        {
                            profile.ScanProfileID,
                            IsActive = false,
                            DeactivatedOptionCount =
                                activeOptions.Count
                        },

                    requestPath:
                        requestPath,

                    ipAddress:
                        ipAddress,

                    isSuccessful:
                        true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null
                );
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }

    }
}