﻿using AzAcme.Cli.Commands.Options;
using AzAcme.Core;
using AzAcme.Core.Exceptions;
using AzAcme.Core.Providers;
using AzAcme.Core.Providers.AzureDns;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using AzAcme.Core.Providers.CloudflareDns;
using Azure.ResourceManager;

namespace AzAcme.Cli
{
    internal class DnsFactory
    {
        public class DnsOptions
        {
            public DnsProviders Provider { get; set; }

            public DefaultAzureCredential? AzureCredential { get; set; } 
            
            public string? AzureDnsResourceId { get; set; }
            public string? AadTenantId { get; set; }
            public string? ZoneOverride { get; set; }
            
            public string? CloudlfareZoneIdentifier { get; set; }
            public string? CloudlfareApiToken { get; set; }
        }

        public static IDnsZone Create(ILogger logger, DnsOptions options)
        {
            switch (options.Provider)
            {
                case DnsProviders.Azure:
                    {
                        if(options.AzureCredential == null)
                        {
                            throw new ArgumentException("Azure Credentials must be set.");
                        }

                        if(string.IsNullOrEmpty(options.AzureDnsResourceId))
                        {
                            throw new ConfigurationException("Azure DNS Resource ID must be set.");
                        }

                        Lazy<IDnsZone> zone = new Lazy<IDnsZone>(() =>
                        {
                            logger.LogDebug("Getting DNS Client Token from AAD...");
                            var dnsClient = new ArmClient(options.AzureCredential);
                            IDnsZone azureDns = new AzureDnsZone(logger, dnsClient, options.AzureDnsResourceId, options.ZoneOverride);

                            return azureDns;
                        });

                        return new LazyDnsZone(zone);
                    }
                case DnsProviders.Cloudflare:
                    {
                        if(string.IsNullOrEmpty(options.CloudlfareApiToken))
                        {
                            throw new ArgumentException("Cloudflare API Token must be set.");
                        }

                        if(string.IsNullOrEmpty(options.CloudlfareZoneIdentifier))
                        {
                            throw new ConfigurationException("Cloudflare Zone ID must be set.");
                        }

                        Lazy<IDnsZone> zone = new Lazy<IDnsZone>(() => 
                            new CloudflareDnsZone(logger, options.CloudlfareApiToken, options.CloudlfareZoneIdentifier));

                        return new LazyDnsZone(zone);
                    }
            }

            throw new NotSupportedException("Unable to build DNS Client");
        }
    }
}
