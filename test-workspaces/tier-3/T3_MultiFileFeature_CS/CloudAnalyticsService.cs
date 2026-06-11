using System;
using System.Collections.Generic;

namespace AnalyticsSystem
{
    /// <summary>
    /// A cloud-based implementation of the analytics service.
    /// </summary>
    public class CloudAnalyticsService : IAnalyticsService
    {
        private readonly string _apiKey;
        private readonly string _endpoint;

        public CloudAnalyticsService(string apiKey, string endpoint)
        {
            _apiKey = apiKey;
            _endpoint = endpoint;
        }

        /// <inheritdoc />
        public void TrackPageView(string pageName)
        {
            // Simulate a cloud API call
            Console.WriteLine($"[CloudAnalytics] Sending PageView: {pageName} to {_endpoint} (API Key: {_apiKey.Substring(0, 4)}...)");
        }
    }
}
