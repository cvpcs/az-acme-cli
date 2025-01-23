using AzAcme.Core.Providers.Helpers;
using AzAcme.Core.Providers.Models;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Microsoft.Extensions.Logging;

namespace AzAcme.Core.Providers.AzureDns
{
    public class AzureDnsZone : IDnsZone
    {
        private readonly ILogger logger;
        private readonly ArmClient client;
        private ResourceIdentifier azureDnsResource;
        private string zoneName;

        public AzureDnsZone(ILogger logger, ArmClient client, string azureDnsZoneResourceId, string? zoneOverride)
        {
            this.azureDnsResource = ResourceIdentifier.Parse(azureDnsZoneResourceId);
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.zoneName = !string.IsNullOrEmpty(zoneOverride) ? zoneOverride : this.azureDnsResource.Name;
        }

        public async Task<Order> SetTxtRecords(Order order)
        {
            // determine the TXT records needed first, so we validate all before applying.
            foreach(var challenge in order.Challenges)
            {
                var record = DnsHelpers.DetermineTxtRecordName(challenge.Identitifer, this.zoneName);
                challenge.SetRecordName(record);
            }

            // add them all
            foreach (var challenge in order.Challenges)
            {
                await UpdateTxtRecord(challenge);
            }

            return order;
        }

        public async Task<Order> RemoveTxtRecords(Order order)
        {
            // update the record first, so we validate all.
            foreach (var challenge in order.Challenges)
            {
                await RemoveTxtRecord(challenge);
            }

            return order;
        }

        private async Task RemoveTxtRecord(DnsChallenge challenge)
        {
            // Load all then filter
            var recordSets = client.GetDnsZoneResource(azureDnsResource).GetDnsTxtRecords();

            // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
            var records = recordSets.Where(x => (x.HasData ? x.Get() : x).Data.Name == challenge.TxtRecord).FirstOrDefault();
            
            if (records == null)
            {
                this.logger.LogDebug("No TXT record set for '{0}' found. Skipping delete.", challenge.TxtRecord);
            }
            else
            {
                this.logger.LogDebug("Removing TXT record set '{0}'.", challenge.TxtRecord);
                await records.DeleteAsync(Azure.WaitUntil.Completed);
            }
        }

        private async Task UpdateTxtRecord(DnsChallenge challenge)
        {
            var recordSets = client.GetDnsZoneResource(azureDnsResource).GetDnsTxtRecords();

            // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
            var records = recordSets.Where(x => (x.HasData ? x.Get() : x).Data.Name == challenge.TxtRecord).FirstOrDefault();

            if (records == null)
            {
                this.logger.LogDebug("DNS Records do not exist for '{0}'. Creating.",challenge.TxtRecord);
                var recordInfo = new DnsTxtRecordInfo();
                recordInfo.Values.Add(challenge.TxtValue);
                var recordData = new DnsTxtRecordData();
                recordData.TtlInSeconds = 60;
                recordData.DnsTxtRecords.Add(recordInfo);

                var result = await recordSets.CreateOrUpdateAsync(Azure.WaitUntil.Completed, challenge.TxtRecord, recordData);
            }
            else
            {
                if (!(records.Data.DnsTxtRecords.Any(txt => txt.Values.Any(val => val == challenge.TxtValue))))
                {
                    this.logger.LogDebug("Updating DNS Record for '{0}'.", challenge.TxtRecord);
                    var recordInfo = new DnsTxtRecordInfo();
                    recordInfo.Values.Add(challenge.TxtValue);
                    records.Data.DnsTxtRecords.Add(recordInfo);
                    var result = await recordSets.CreateOrUpdateAsync(Azure.WaitUntil.Completed, records.Data.Name, records.Data);
                }
                else
                {
                    this.logger.LogDebug("DNS Record with matching challenge value already exisits for '{0}'. Skipping.",challenge.TxtRecord);
                }
            }
            
        }

    }
}
