using Rocket.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkshopList
{
    public class WorkshopListConfig : IRocketPluginConfiguration
    {
        public string WebhookURL { get; set; }

        public void LoadDefaults()
        {
            WebhookURL = "https://discord.com/api/webhooks/NULL/NULL";
        }
    }
}
