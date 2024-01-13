using System;


namespace AdvancedTrackedRideStats {
    [Serializable]
    public class ATRStatsConfig {
        public static ATRStatsConfig Instance = new ATRStatsConfig();
		public float nearFactor = 0.95f;
    }
}
