using System.Collections.Generic;
using UnityEngine;

namespace CSU.Tour
{
    public struct UserSample
    {
        public double time;
        public Vector3 bodyPosition;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
    }

    public struct MetricEstimate
    {
        public bool available;
        public float value;
        public float confidence;
        public string method;

        public static MetricEstimate Unavailable
            => new MetricEstimate { available = false, method = "unimplemented" };
    }

    public struct TourRecommendation
    {
        public bool available;
        public string poiId;
        public Vector3[] route;
    }

    public interface IPathQualityEvaluator
    {
        MetricEstimate Evaluate(IReadOnlyList<UserSample> sampledTrajectory);
    }

    public interface IViewingStateEstimator
    {
        MetricEstimate Estimate(IReadOnlyList<UserSample> history, TourPoi poi);
    }

    public interface IAdaptiveTourPlanner
    {
        TourRecommendation Recommend(
            UserSample user,
            IReadOnlyList<TourPoi> pois,
            IReadOnlyList<bool> visited,
            IReadOnlyList<MetricEstimate> viewingStates);
    }
}
