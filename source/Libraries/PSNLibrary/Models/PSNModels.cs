using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNLibrary.Models
{       
    public class PlayedTitlesResponseData
    {
        public class PlayedTitlesRetrieve
        {
            public class Title
            {
                public class Image
                {
                    public string url { get; set; }
                }
                public string conceptId { get; set; }
                public string entitlementId { get; set; }
                public bool? isActive { get; set; }
                public string name { get; set; }
                public string platform { get; set; }
                public string productId { get; set; }
                public string subscriptionService { get; set; }
                public string titleId { get; set; }
                public Image image { get; set; }

                public override string ToString()
                {
                    return name;
                }
            } 

            public List<Title> games { get; set; }

        }
        public PlayedTitlesRetrieve gameLibraryTitlesRetrieve { get; set; }
    }
    public class AccountTitlesResponseData
    {
        public class AccountTitlesRetrieve
        {
            public class Title
            {
                public class Image
                {
                    public string url { get; set; }
                }
                public string conceptId { get; set; }
                public string entitlementId { get; set; }
                public bool? isActive { get; set; }
                public bool isDownloadable { get; set; }
                public bool isPreOrder { get; set; }
                public string name { get; set; }
                public string platform { get; set; }
                public string productId { get; set; }
                public string subscriptionService { get; set; }
                public string titleId { get; set; }
                public Image image { get; set; }

                public override string ToString()
                {
                    return name;
                }
            }

            public class PageInfo
            {
                public bool isLast { get; set; }
                public int offset { get; set; }
                public int size { get; set; }
                public int totalCount { get; set; }
            }

            public List<Title> games { get; set; }

            public PageInfo pageInfo;
        }
        public AccountTitlesRetrieve purchasedTitlesRetrieve { get; set; }   
    }
    public class AccountTitlesErrorResponse
    {        
        public class Error {
            public string message { get; set; }
        }
           
        public List<Error> errors { get; set; }

        public AccountTitlesResponseData data { get; set; }
    }

    public class PlayedTitles
    {
        public PlayedTitlesResponseData data { get; set; }
    }
    public class AccountTitles
    {
        public AccountTitlesResponseData data { get; set; }
    }

    public class PlayedTitlesMobile
    {
        public class PlayedTitleMobile
        {
            
            public string imageUrl { get; set; }
            public string name { get; set; }
            public string category { get; set; }
            public string titleId { get; set; }

            public override string ToString()
            {
                return name;
            }
        }

        public List<PlayedTitleMobile> titles { get; set; }
        public int? nextOffset { get; set; }
        public int previousOffset { get; set; }
        public int totalItemCount { get; set; }
    }

    public class TrophyTitleMobile
    {

        public string trophyTitleIconUrl { get; set; }
        public string trophyTitleName { get; set; }
        public string trophyTitlePlatform { get; set; }
        public string npCommunicationId { get; set; }

        public override string ToString()
        {
            return trophyTitleName;
        }
    }

    public class TrophyTitlesMobile
    {
        public List<TrophyTitleMobile> trophyTitles { get; set; }
        public int? nextOffset { get; set; }
        public int totalItemCount { get; set; }
    }

    public class TrophyTitlesWithIdsMobile
    {
        public class TrophyTitleWithIdsMobile
        {
            public string npTitleId { get; set; }
            public List<TrophyTitleMobile> trophyTitles { get; set; }
        }

        public List<TrophyTitleWithIdsMobile> titles { get; set; }
    }

    public class MobileTokens
    {
        public string access_token { get; set; }
    }
}
