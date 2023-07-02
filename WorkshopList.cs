using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using SDG.Provider;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ShimmyMySherbet.DiscordWebhooks;
using System.Threading.Tasks;
using ShimmyMySherbet.DiscordWebhooks.Embeded;
using Newtonsoft.Json;
using System.Threading;

namespace WorkshopList
{
    public class WorkshopList : RocketPlugin<WorkshopListConfig>
    {
        public string WebhookURL
        {
            get
            {
                return Configuration.Instance.WebhookURL;
            }
        }

        public ulong MessageId { get; private set; }

        public string MessageEndpoint
        {
            get
            {
                return WebhookURL + $"/messages/{MessageId}";
            }
        }

        public string DataFilePath
        {
            get
            {
                string dir = Path.Combine(Directory, "data");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                return Path.Combine(dir, "data.dat");
            }
        }


        protected override void Load()
        {
            Logger.Log($"{Name} {Assembly.GetName().Version} has been loaded.");
            Task.Run(() => LoadMessageId());
            Level.onPostLevelLoaded += HandlePostLoaded;
        }

        private void HandlePostLoaded(int level)
        {
            ThreadPool.QueueUserWorkItem(async (o) => await SendWorkshopList());
        }

        protected override void Unload()
        {
            Level.onPostLevelLoaded -= HandlePostLoaded;
            Logger.Log($"{Name} has been unloaded.");
        }

        public async Task SendWorkshopList()
        {
            if (MessageExists(MessageId))
            {
                string content = string.Empty;
                foreach (ulong id in Provider.getServerWorkshopFileIDs())
                {
                    TempSteamworksWorkshop.getCachedDetails(new Steamworks.PublishedFileId_t(id), out CachedUGCDetails details);
                    content += $"\n{details.GetTitle()}" + $" ([{id}](https://steamcommunity.com/sharedfiles/filedetails/?id={details.fileId.m_PublishedFileId}))";
                }

                await DiscordWebhookService.EditMessageAsync(WebhookURL, MessageId, new WebhookMessage().PassEmbed().WithTitle($"Workshop Mods - {Provider.serverName}").WithDescription(content).WithColor(EmbedColor.SlateGray).WithTimestamp(DateTime.Now).Finalize());
            }
            else
            {
                try
                {
                    string content = string.Empty;
                    foreach (ulong id in Provider.getServerWorkshopFileIDs())
                    {
                        TempSteamworksWorkshop.getCachedDetails(new Steamworks.PublishedFileId_t(id), out CachedUGCDetails details);
                        content += $"\n{details.GetTitle()}" + $" ([{id}](https://steamcommunity.com/sharedfiles/filedetails/?id={details.fileId.m_PublishedFileId}))";
                    }

                    HttpWebRequest request = WebRequest.CreateHttp(WebhookURL + "?wait=true");
                    request.Method = "POST";
                    request.ContentType = "application/json";

                    string Payload = JsonConvert.SerializeObject(new WebhookMessage().PassEmbed().WithTitle($"Workshop Mods - {Provider.serverName}").WithDescription(content).WithColor(EmbedColor.SlateGray).WithTimestamp(DateTime.Now).Finalize());
                    byte[] Buffer = Encoding.UTF8.GetBytes(Payload);

                    request.ContentLength = Buffer.Length;
                    using (Stream write = (await request.GetRequestStreamAsync()))
                    {
                        await write.WriteAsync(Buffer, 0, Buffer.Length);
                        await write.FlushAsync();
                    }

                    var resp = (HttpWebResponse)(await request.GetResponseAsync());
                    Stream receivingStream = resp.GetResponseStream();
                    StreamReader readStream = new StreamReader(receivingStream, Encoding.UTF8);
                    Message msg = JsonConvert.DeserializeObject<Message>(await readStream.ReadToEndAsync());
                    SaveMessageId(msg.id);

                    /*using WebClient client = new();
                    client.QueryString = new() { { "wait", "true" } };
                    string str = client.UploadString(WebhookURL, JsonConvert.SerializeObject(new WebhookMessage().PassEmbed().WithTitle($"Workshop Mods - {Provider.serverName}").WithDescription(content).WithColor(EmbedColor.SlateGray).WithTimestamp(DateTime.Now).Finalize()));
                    Message msg = JsonConvert.DeserializeObject<Message>(str);
                    SaveMessageId(msg.id);*/
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "An exception occurred while attempting to execute a webhook.");
                }
            }
        }

        public void LoadMessageId()
        {
            string path = DataFilePath;
            if (!File.Exists(path)) { MessageId = ulong.MinValue; return; }

            Block block = ReadWrite.readBlock(path, false, false, 0);
            MessageId = block.readUInt64();
            return;
        }

        public void SaveMessageId(ulong messageId)
        {
            Block block = new();
            block.writeUInt64(messageId);
            ReadWrite.writeBlock(DataFilePath, false, false, block);
        }

        public bool MessageExists(ulong messageId)
        {
            if (messageId == ulong.MinValue) return false;

            try
            {
                using WebClient client = new();
                client.DownloadData(MessageEndpoint);
                Logger.Log("Found a proper message at the specified endpoint. Will edit that message.");
                return true;
            }
            catch
            {
                Logger.Log("An exception occured when attempting to download a message at the specified endpoint. Likely going to send a new message.");
                return false;
            }
        }
    }

    public class Message
    {
        public ulong id { get; set; }
    }
}
