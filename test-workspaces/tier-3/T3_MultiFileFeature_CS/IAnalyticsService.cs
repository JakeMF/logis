using System.Collections.Generic;

namespace AnalyticsSystem
{
    /// <summary>
    /// Defines the contract for an analytics tracking service.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Tracks a simple page view event.
        /// </summary>
        /// <param name="pageName">The name of the page visited.</param>
        void TrackPageView(string pageName);
    }
}
