using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.Core.Event;
using BililiveRecorder.Core.SimpleWebhook;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BililiveRecorder.Core.UnitTests.SimpleWebhook
{
    public class WebhookTests
    {
        #region URL Validation Tests

        [Theory]
        [InlineData("https://example.com/webhook", true)]
        [InlineData("http://example.com/webhook", true)]
        [InlineData("https://test.example.org/api/webhook", true)]
        [InlineData("http://localhost:8080/webhook", true)]
        [InlineData("https://192.168.1.1/webhook", true)]
        [InlineData("", false)]  // Empty string
        [InlineData(null, false)]  // Null
        [InlineData("not-a-url", false)]  // Invalid URL
        [InlineData("ftp://example.com", false)]  // Wrong scheme
        [InlineData("https://test.example.com", false)]  // Disallowed domain
        [InlineData("https://baidu.com/webhook", false)]  // Disallowed domain
        [InlineData("https://qq.com/webhook", false)]  // Disallowed domain
        [InlineData("https://google.com/webhook", false)]  // Disallowed domain
        [InlineData("https://bilibili.com/webhook", false)]  // Disallowed domain
        [InlineData("https://api.bilibili.com/webhook", false)]  // Disallowed subdomain
        [InlineData("https://live.bilibili.com/webhook", false)]  // Disallowed subdomain
        [InlineData("https://bilivideo.com/webhook", false)]  // Disallowed domain
        [InlineData("https://hdslb.com/webhook", false)]  // Disallowed domain
        [InlineData("https://b23.tv/webhook", false)]  // Disallowed domain
        public void IsUrlAllowed_ShouldValidateCorrectly(string url, bool expected)
        {
            // Act
            var result = BasicWebhookV2.IsUrlAllowed(url);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region WebhookV2 Constructor Tests

        [Fact]
        public void WebhookV2_Constructor_ShouldNotThrow_WhenUrlIsEmpty()
        {
            // Arrange & Act
            var config = new GlobalConfig { WebHookUrlsV2 = "" };
            var webhook = new BasicWebhookV2(config);

            // Assert
            Assert.NotNull(webhook);
        }

        [Fact]
        public void WebhookV2_Constructor_ShouldNotThrow_WhenUrlIsNull()
        {
            // Arrange & Act
            var config = new GlobalConfig { WebHookUrlsV2 = null };
            var webhook = new BasicWebhookV2(config);

            // Assert
            Assert.NotNull(webhook);
        }

        [Fact]
        public void WebhookV2_Constructor_ShouldThrow_WhenConfigIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BasicWebhookV2(null!));
        }

        [Fact]
        public void WebhookV2_ShouldSupportMultipleUrls()
        {
            // Arrange
            var urls = "https://example1.com/webhook\r\nhttps://example2.com/webhook\nhttps://example3.com/webhook";
            var config = new GlobalConfig { WebHookUrlsV2 = urls };

            // Act
            var webhook = new BasicWebhookV2(config);

            // Assert - verify it can be created with multiple URLs
            Assert.NotNull(webhook);
        }

        [Theory]
        [InlineData("https://example.com/webhook\n\n\n")]  // Extra newlines
        [InlineData("  https://example.com/webhook  ")]  // Whitespace
        [InlineData("https://example.com/webhook\r\n  \r\n")]  // Mixed whitespace
        public void WebhookV2_ShouldHandleWhitespace(string urls)
        {
            // Arrange & Act
            var config = new GlobalConfig { WebHookUrlsV2 = urls };
            var webhook = new BasicWebhookV2(config);

            // Assert - should not throw
            Assert.NotNull(webhook);
        }

        #endregion

        #region WebhookV1 Constructor Tests

        [Fact]
        public void WebhookV1_Constructor_ShouldNotThrow_WhenUrlIsEmpty()
        {
            // Arrange & Act
            var config = new ConfigV3();
            config.Global.WebHookUrls = "";
            var webhook = new BasicWebhookV1(config);

            // Assert
            Assert.NotNull(webhook);
        }

        [Fact]
        public void WebhookV1_Constructor_ShouldThrow_WhenConfigIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BasicWebhookV1(null!));
        }

        [Fact]
        public void WebhookV1_ShouldSupportMultipleUrls()
        {
            // Arrange
            var urls = "https://example1.com/webhook\r\nhttps://example2.com/webhook\nhttps://example3.com/webhook";
            var config = new ConfigV3();
            config.Global.WebHookUrls = urls;

            // Act
            var webhook = new BasicWebhookV1(config);

            // Assert - verify it can be created with multiple URLs
            Assert.NotNull(webhook);
        }

        #endregion

        #region Event Wrapper Serialization Tests

        [Fact]
        public void EventWrapper_ShouldSerializeEventType()
        {
            // Arrange
            var wrapper = new
            {
                EventType = "TestEvent",
                EventTimestamp = "2023-01-01T00:00:00Z",
                EventId = Guid.NewGuid(),
                EventData = new { Test = "data" }
            };

            // Act
            var json = JsonConvert.SerializeObject(wrapper);
            var parsed = JObject.Parse(json);

            // Assert
            Assert.Equal("TestEvent", parsed["EventType"]?.ToString());
            Assert.NotNull(parsed["EventData"]);
        }

        #endregion
    }
}
