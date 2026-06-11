using System;
using System.Collections.Generic;

namespace AnalyticsSystem
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Analytics Dashboard Simulation ===");

            // Initialize the service
            IAnalyticsService analytics = new CloudAnalyticsService("PROD-KEY-12345-XYZ", "https://api.analytics-cloud.io/v1");

            // Simulate user navigation
            analytics.TrackPageView("HomePage");
            analytics.TrackPageView("ProductDetails");
            analytics.TrackPageView("CheckoutPage");

            Console.WriteLine("Simulation complete.");
        }
    }
}
