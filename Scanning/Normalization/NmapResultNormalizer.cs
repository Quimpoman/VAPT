using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace EVOLVE_VAPT.Scanning.Normalization
{
    public sealed class NmapResultNormalizer :
        IScanResultNormalizer
    {
        public string ScannerTypeCode =>
            "NMAP";


        public IReadOnlyList<NormalizedFinding>
            Normalize(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(
                rawOutput))
            {
                throw new InvalidOperationException(
                    "Nmap output is empty.");
            }


            XDocument document;


            try
            {
                var settings =
                    new XmlReaderSettings
                    {
                        /*
                         * Do not allow scanner XML to resolve
                         * external DTDs or external resources.
                         */
                        DtdProcessing =
                            DtdProcessing.Ignore,

                        XmlResolver =
                            null
                    };


                using var stringReader =
                    new StringReader(
                        rawOutput);


                using var xmlReader =
                    XmlReader.Create(
                        stringReader,
                        settings);


                document =
                    XDocument.Load(
                        xmlReader,
                        LoadOptions.None);
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException(
                    "Nmap output contains invalid XML.",
                    ex);
            }


            var root =
                document.Root;


            if (root == null ||
                !string.Equals(
                    root.Name.LocalName,
                    "nmaprun",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Nmap output does not contain a valid nmaprun root element.");
            }


            var findings =
                new List<NormalizedFinding>();


            var hosts =
                GetChildren(
                    root,
                    "host")
                .ToList();


            for (var hostIndex = 0;
                 hostIndex < hosts.Count;
                 hostIndex++)
            {
                var host =
                    hosts[hostIndex];


                /*
                 * We currently normalize only hosts that
                 * Nmap positively identified as up.
                 */
                var hostStatus =
                    GetChild(
                        host,
                        "status")
                    ?.Attribute("state")
                    ?.Value;


                if (!string.Equals(
                    hostStatus,
                    "up",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                var address =
                    GetPrimaryAddress(
                        host);


                var hostname =
                    GetPreferredHostname(
                        host);


                var targetValue =
                    address
                    ?? hostname
                    ?? $"Host {hostIndex + 1}";


                /*
                 * Source IDs need only be unique inside
                 * this scanner result. Use the actual host
                 * address when available.
                 */
                var hostIdentifier =
                    address
                    ?? $"HOST-{hostIndex + 1:D3}";


                var portsElement =
                    GetChild(
                        host,
                        "ports");


                if (portsElement == null)
                {
                    continue;
                }


                foreach (var port in
                    GetChildren(
                        portsElement,
                        "port"))
                {
                    var stateElement =
                        GetChild(
                            port,
                            "state");


                    var state =
                        stateElement
                            ?.Attribute("state")
                            ?.Value;


                    /*
                     * Open ports are observations.
                     * Closed, filtered and ambiguous states
                     * are not turned into Findings here.
                     */
                    if (!string.Equals(
                        state,
                        "open",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }


                    var protocol =
                        port.Attribute("protocol")
                            ?.Value
                            ?.Trim();


                    var portId =
                        port.Attribute("portid")
                            ?.Value
                            ?.Trim();


                    if (string.IsNullOrWhiteSpace(
                            protocol) ||
                        string.IsNullOrWhiteSpace(
                            portId))
                    {
                        throw new InvalidOperationException(
                            "An open Nmap port is missing its protocol or port number.");
                    }


                    if (!int.TryParse(
                            portId,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var portNumber) ||
                        portNumber < 1 ||
                        portNumber > 65535)
                    {
                        throw new InvalidOperationException(
                            $"Nmap returned invalid port number '{portId}'.");
                    }


                    var serviceElement =
                        GetChild(
                            port,
                            "service");


                    var serviceName =
                        GetAttribute(
                            serviceElement,
                            "name");


                    var product =
                        GetAttribute(
                            serviceElement,
                            "product");


                    var version =
                        GetAttribute(
                            serviceElement,
                            "version");


                    var extraInfo =
                        GetAttribute(
                            serviceElement,
                            "extrainfo");


                    var tunnel =
                        GetAttribute(
                            serviceElement,
                            "tunnel");


                    var title =
                        BuildTitle(
                            protocol,
                            portNumber,
                            serviceName);


                    var evidence =
                        BuildEvidence(
                            targetValue,
                            address,
                            hostname,
                            protocol,
                            portNumber,
                            state,
                            serviceName,
                            product,
                            version,
                            extraInfo,
                            tunnel,
                            serviceElement);


                    findings.Add(
                        new NormalizedFinding
                        {
                            SourceFindingIdentifier =
                                $"NMAP-{hostIdentifier}-{protocol.ToUpperInvariant()}-{portNumber}",

                            Title =
                                title,

                            /*
                             * An exposed service is an observation,
                             * not automatically a vulnerability.
                             */
                            SeverityCode =
                                "INFO",

                            TargetValue =
                                targetValue,

                            Description =
                                BuildDescription(
                                    protocol,
                                    portNumber,
                                    serviceName),

                            Evidence =
                                evidence,

                            Recommendation =
                                "Review whether this exposed service is required, appropriately restricted, securely configured, and maintained according to organizational security policy.",

                            Reference =
                                "NMAP-SERVICE-DISCOVERY"
                        });
                }
            }


            return findings;
        }
        private static IEnumerable<XElement>
    GetChildren(
        XElement parent,
        string localName)
        {
            return parent
                .Elements()
                .Where(x =>
                    string.Equals(
                        x.Name.LocalName,
                        localName,
                        StringComparison.Ordinal));
        }


        private static XElement?
            GetChild(
                XElement parent,
                string localName)
        {
            return GetChildren(
                    parent,
                    localName)
                .FirstOrDefault();
        }


        private static string?
            GetAttribute(
                XElement? element,
                string attributeName)
        {
            var value =
                element
                    ?.Attribute(
                        attributeName)
                    ?.Value
                    ?.Trim();


            return string.IsNullOrWhiteSpace(
                value)
                    ? null
                    : value;
        }
        private static string?
    GetPrimaryAddress(
        XElement host)
        {
            var addresses =
                GetChildren(
                    host,
                    "address")
                .ToList();


            /*
             * Prefer IPv4/IPv6 network addresses over
             * MAC or other address types.
             */
            var networkAddress =
                addresses.FirstOrDefault(x =>
                {
                    var addressType =
                        x.Attribute("addrtype")
                            ?.Value;


                    return string.Equals(
                               addressType,
                               "ipv4",
                               StringComparison.OrdinalIgnoreCase)
                           ||
                           string.Equals(
                               addressType,
                               "ipv6",
                               StringComparison.OrdinalIgnoreCase);
                });


            networkAddress ??=
                addresses.FirstOrDefault();


            return GetAttribute(
                networkAddress,
                "addr");
        }
        private static string?
    GetPreferredHostname(
        XElement host)
        {
            var hostnames =
                GetChild(
                    host,
                    "hostnames");


            if (hostnames == null)
            {
                return null;
            }


            var hostname =
                GetChildren(
                    hostnames,
                    "hostname")
                .FirstOrDefault();


            return GetAttribute(
                hostname,
                "name");
        }
        private static string BuildTitle(
    string protocol,
    int portNumber,
    string? serviceName)
        {
            var normalizedProtocol =
                protocol.ToUpperInvariant();


            if (string.IsNullOrWhiteSpace(
                serviceName) ||
                string.Equals(
                    serviceName,
                    "unknown",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    $"Open {normalizedProtocol} Port {portNumber}";
            }


            return
                $"Open {normalizedProtocol} Service: {portNumber} / {serviceName}";
        }
        private static string BuildDescription(
    string protocol,
    int portNumber,
    string? serviceName)
        {
            var serviceText =
                string.IsNullOrWhiteSpace(
                    serviceName)
                    ? "an unidentified service"
                    : $"the '{serviceName}' service";


            return
                $"Nmap identified an open {protocol.ToUpperInvariant()} port {portNumber} exposing {serviceText}. " +
                "An open service is not automatically a vulnerability and requires analyst review to determine whether the exposure is expected and securely configured.";
        }
        private static string BuildEvidence(
    string targetValue,
    string? address,
    string? hostname,
    string protocol,
    int portNumber,
    string? state,
    string? serviceName,
    string? product,
    string? version,
    string? extraInfo,
    string? tunnel,
    XElement? serviceElement)
        {
            var lines =
                new List<string>
                {
                    $"Target: {targetValue}"
                };


            if (!string.IsNullOrWhiteSpace(
                    address))
            {
                lines.Add(
                    $"Address: {address}");
            }


            if (!string.IsNullOrWhiteSpace(
                    hostname))
            {
                lines.Add(
                    $"Hostname: {hostname}");
            }


            lines.Add(
                $"Port: {portNumber}/{protocol.ToLowerInvariant()}");

            lines.Add(
                $"State: {state ?? "unknown"}");


            if (!string.IsNullOrWhiteSpace(
                    serviceName))
            {
                lines.Add(
                    $"Service: {serviceName}");
            }


            if (!string.IsNullOrWhiteSpace(
                    product))
            {
                lines.Add(
                    $"Product: {product}");
            }


            if (!string.IsNullOrWhiteSpace(
                    version))
            {
                lines.Add(
                    $"Version: {version}");
            }


            if (!string.IsNullOrWhiteSpace(
                    extraInfo))
            {
                lines.Add(
                    $"Extra Info: {extraInfo}");
            }


            if (!string.IsNullOrWhiteSpace(
                    tunnel))
            {
                lines.Add(
                    $"Tunnel: {tunnel}");
            }


            if (serviceElement != null)
            {
                var cpes =
                    GetChildren(
                        serviceElement,
                        "cpe")

                        .Select(x =>
                            x.Value.Trim())

                        .Where(x =>
                            !string.IsNullOrWhiteSpace(
                                x))

                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)

                        .ToList();


                foreach (var cpe in cpes)
                {
                    lines.Add(
                        $"CPE: {cpe}");
                }
            }


            return string.Join(
                Environment.NewLine,
                lines);
        }
    }
}