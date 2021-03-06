﻿/*
 * Developer: Ramtin Jokar [ Ramtinak@live.com ] [ My Telegram Account: https://t.me/ramtinak ]
 * 
 * Github source: https://github.com/ramtinak/InstagramApiSharp
 * Nuget package: https://www.nuget.org/packages/InstagramApiSharp
 * 
 * IRANIAN DEVELOPERS
 */
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
namespace InstagramApiSharp.Classes
{
    public enum GenderType
    {
        //Gender (1 = male, 2 = female, 3 = unknown)
        Male = 1,
        Female = 2,
        Unknown = 3
    }
    public class InstaDefault
    {
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }
    internal class InstaDefaultResponse : InstaDefault
    {
        public bool IsSucceed { get { return Status.ToLower() == "ok"; } }
    }

    internal class AccountArchiveStoryResponse
    {
        [JsonProperty("reel_auto_archive")]
        public string ReelAutoArchive { get; set; }
        [JsonProperty("message_prefs")]
        public string MessagePrefs { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
    }
    
    public enum MessageRepliesType
    {
        [JsonProperty("everyone")]
        Everyone,
        [JsonProperty("following")]
        Following,
        [JsonProperty("off")]
        Off
    }

}
