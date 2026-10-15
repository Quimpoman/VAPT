using EVOLVE_VAPT.Models;
using System.Net;
using System.Net.Sockets;

namespace EVOLVE_VAPT.Services
{
    /// <summary>
    /// Contains target-specific validation rules.
    ///
    /// This keeps URL/IP/domain/CIDR validation separate
    /// from database and controller logic.
    /// </summary>
    public class TargetValidationService
    {
        private readonly EVOLVE_VAPTContext _context;

        public TargetValidationService(
            EVOLVE_VAPTContext context)
        {
            _context = context;
        }


        /// <summary>
        /// Validates a Target Value according to the selected
        /// Target Type stored in ref_TargetType.
        /// </summary>
        public async Task<(bool IsValid, string? ErrorMessage)>
            ValidateTargetValueAsync(
                int targetTypeId,
                string? targetValue)
        {
            if (string.IsNullOrWhiteSpace(targetValue))
            {
                return (
                    false,
                    "Target Value is required."
                );
            }


            // Retrieve the reference record rather than relying
            // on hardcoded numeric TargetTypeID values.
            var targetType =
                await _context.ref_TargetType
                    .FindAsync(targetTypeId);


            if (targetType == null ||
                targetType.IsActive != true)
            {
                return (
                    false,
                    "The selected Target Type is invalid."
                );
            }


            var value =
                targetValue.Trim();


            switch (targetType.TargetTypeCode)
            {
                case "URL":
                    return ValidateUrl(value);

                case "DOMAIN":
                    return ValidateDomain(value);

                case "IPV4":
                    return ValidateIPv4(value);

                case "IPV6":
                    return ValidateIPv6(value);

                case "CIDR":
                    return ValidateCidr(value);

                case "API_SPEC":
                    return ValidateApiSpecification(value);

                default:
                    return (
                        false,
                        "Unsupported Target Type."
                    );
            }
        }


        /// <summary>
        /// Validates an absolute HTTP/HTTPS URL.
        /// </summary>
        private static (bool, string?) ValidateUrl(
            string value)
        {
            if (!Uri.TryCreate(
                    value,
                    UriKind.Absolute,
                    out var uri))
            {
                return (
                    false,
                    "Enter a valid absolute URL."
                );
            }


            // For the current VAPT implementation,
            // web targets must use HTTP or HTTPS.
            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps)
            {
                return (
                    false,
                    "URL targets must use HTTP or HTTPS."
                );
            }


            if (string.IsNullOrWhiteSpace(uri.Host))
            {
                return (
                    false,
                    "The URL must contain a valid host."
                );
            }


            return (true, null);
        }


        /// <summary>
        /// Validates a DNS/domain-style target.
        ///
        /// The value must be a hostname only and must not
        /// contain a URL scheme, path, query string or fragment.
        /// </summary>
        private static (bool, string?) ValidateDomain(
            string value)
        {
            if (value.Contains("://") ||
                value.Contains('/') ||
                value.Contains('?') ||
                value.Contains('#'))
            {
                return (
                    false,
                    "Enter the domain only, for example example.com."
                );
            }


            if (Uri.CheckHostName(value) !=
                UriHostNameType.Dns)
            {
                return (
                    false,
                    "Enter a valid domain name."
                );
            }


            return (true, null);
        }


        /// <summary>
        /// Validates that the supplied value is specifically IPv4.
        /// </summary>
        private static (bool, string?) ValidateIPv4(
            string value)
        {
            if (!IPAddress.TryParse(
                    value,
                    out var ipAddress) ||
                ipAddress.AddressFamily !=
                    AddressFamily.InterNetwork)
            {
                return (
                    false,
                    "Enter a valid IPv4 address."
                );
            }


            return (true, null);
        }


        /// <summary>
        /// Validates that the supplied value is specifically IPv6.
        /// </summary>
        private static (bool, string?) ValidateIPv6(
            string value)
        {
            if (!IPAddress.TryParse(
                    value,
                    out var ipAddress) ||
                ipAddress.AddressFamily !=
                    AddressFamily.InterNetworkV6)
            {
                return (
                    false,
                    "Enter a valid IPv6 address."
                );
            }


            return (true, null);
        }


        /// <summary>
        /// Validates basic IPv4/IPv6 CIDR syntax.
        ///
        /// Examples:
        /// 192.168.1.0/24
        /// 2001:db8::/32
        /// </summary>
        private static (bool, string?) ValidateCidr(
            string value)
        {
            var parts =
                value.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries);


            if (parts.Length != 2)
            {
                return (
                    false,
                    "Enter a valid CIDR range, for example 192.168.1.0/24."
                );
            }


            if (!IPAddress.TryParse(
                    parts[0],
                    out var ipAddress))
            {
                return (
                    false,
                    "The CIDR network address is invalid."
                );
            }


            if (!int.TryParse(
                    parts[1],
                    out var prefixLength))
            {
                return (
                    false,
                    "The CIDR prefix length is invalid."
                );
            }


            var maximumPrefix =
                ipAddress.AddressFamily ==
                    AddressFamily.InterNetwork
                    ? 32
                    : 128;


            if (prefixLength < 0 ||
                prefixLength > maximumPrefix)
            {
                return (
                    false,
                    $"The CIDR prefix must be between 0 and {maximumPrefix}."
                );
            }


            return (true, null);
        }


        /// <summary>
        /// API Specification handling is intentionally kept
        /// conservative for now.
        ///
        /// We have not yet decided whether API specifications
        /// will be represented by a URL, uploaded OpenAPI file,
        /// or another artifact-storage mechanism.
        /// </summary>
        private static (bool, string?)
            ValidateApiSpecification(
                string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (
                    false,
                    "API Specification value is required."
                );
            }


            return (true, null);
        }
    }
}