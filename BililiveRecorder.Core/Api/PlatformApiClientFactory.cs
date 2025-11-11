using System;
using BililiveRecorder.Core.Api.Http;
using BililiveRecorder.Core.Config.V3;

namespace BililiveRecorder.Core.Api
{
    internal interface IPlatformApiClientFactory
    {
        IPlatformApiClient CreateClient(StreamingPlatform platform);
    }

    internal class PlatformApiClientFactory : IPlatformApiClientFactory
    {
        private readonly GlobalConfig globalConfig;

        public PlatformApiClientFactory(GlobalConfig globalConfig)
        {
            this.globalConfig = globalConfig ?? throw new ArgumentNullException(nameof(globalConfig));
        }

        public IPlatformApiClient CreateClient(StreamingPlatform platform)
        {
            return platform switch
            {
                StreamingPlatform.Bilibili => new BilibiliApiClient(globalConfig),
                StreamingPlatform.Douyin => new DouyinApiClient(globalConfig),
                _ => throw new NotSupportedException($"Platform {platform} is not supported")
            };
        }
    }
}
