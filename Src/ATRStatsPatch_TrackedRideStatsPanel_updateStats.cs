using HarmonyLib;
using Parkitect.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace AdvancedTrackedRideStats {
	public class AnimationCurveCache {
		public static Dictionary<AnimationCurve, AnimationCurveCache> cache = new Dictionary<AnimationCurve, AnimationCurveCache>();

		public AnimationCurve curve;
		public float minKey;
		public float maxKey;
		public float bestKey;
		public float bestValue;
		/// whether bestValue == worstValue
		public bool isConstant;

		public float minNearKey = 0f;
		public float maxNearKey = 0f;
		public float nearFactor = -1f;
		public AnimationCurveCache(AnimationCurve curve) {
			this.curve = curve;

			// figure out the key range:
			bool hasTimeRange = false;
			foreach (var kf in curve.keys) {
				if (!hasTimeRange) {
					hasTimeRange = true;
					minKey = kf.time;
					maxKey = kf.time;
				} else {
					if (kf.time < minKey) minKey = kf.time;
					if (kf.time > maxKey) maxKey = kf.time;
				}
			}
			if (!hasTimeRange) minKey = maxKey = 0f;

			// pass 1: check values at curve points
			bestKey = minKey;
			bestValue = curve.Evaluate(bestKey);
			var worstValue = bestValue;
			foreach (var kf in curve.keys) {
				var value = curve.Evaluate(kf.time);
				if (value > bestValue) {
					bestKey = kf.time;
					bestValue = value;
				}
				if (value < worstValue) worstValue = value;
			}

			// pass 2: check values across samples
			var steps = 100;
			for (var step = 1; step <= steps; step++) {
				var factor = (float)step / steps;
				var key = minKey * (1f - factor) + maxKey * factor;
				var value = curve.Evaluate(key);
				if (value > bestValue) {
					bestKey = key;
					bestValue = value;
				}
				if (value < worstValue) worstValue = value;
			}

			// other calculations:
			isConstant = Mathf.Approximately(bestValue, worstValue);
		}

		public void calcNear(float factor) {
			if (Mathf.Approximately(nearFactor, factor)) return;
			nearFactor = factor;

			var steps = 100;
			var stepSize = (maxKey - minKey) / steps;
			var refValue = factor * bestValue;

			minNearKey = bestKey;
			for (var step = 0; step < steps; step++) {
				var key = minNearKey - stepSize;
				if (key < minKey) break;
				var value = curve.Evaluate(key);
				if (value < refValue) break;
				minNearKey = key;
			}

			maxNearKey = bestKey;
			for (var step = 0; step < steps; step++) {
				var key = maxNearKey + stepSize;
				if (key > maxKey) break;
				var value = curve.Evaluate(key);
				if (value < refValue) break;
				maxNearKey = key;
			}
		}
	}

	[HarmonyPatch]
	public class ATRStatsPatch_TrackedRideStatsPanel_updateStats {

		static MethodBase TargetMethod() => AccessTools.Method(typeof(TrackedRideStatsPanel), "updateStats");

		static TextMeshProUGUI __statsTextFieldDummy = null;
		static TextMeshProUGUI __statsTextFieldSwap = null;

		[HarmonyPrefix]
		public static void Prefix(TrackedRideStatsPanel __instance) {
			// 
			// according to a friend, I probably shouldn't be doing it this way
			var self = __instance;
			var trav = Traverse.Create(self);
			if (__statsTextFieldDummy == null) {
				__statsTextFieldDummy = new TextMeshProUGUI();
			}
			var statsTextField = trav.Field<TextMeshProUGUI>("statsText");
			__statsTextFieldSwap = statsTextField.Value;
			statsTextField.Value = __statsTextFieldDummy;
		}

		[HarmonyPostfix]
		public static void Postfix(TrackedRideStatsPanel __instance) {
			var self = __instance;
			var trav = Traverse.Create(self);
			var trackedRide = trav.Field<TrackedRide>("trackedRide").Value;
			var stats = trackedRide.stats;

			var stringBuilder = new StringBuilder();
			const string nf = "0.##";
			const string nf1 = "0.#";
			const string posValue = "<pos=3.5em>";
			const string posBestContrib = "<pos=7em>";
			const string posBestValue = "<pos=10.5em>";
			const string posNearRange = "<pos=14em>";
			const string posDelta = "<pos=19em>";
			const string posDesc = "<pos=22em>";
			stringBuilder.AppendLine("e"
				+ posValue + "v"
				+ posBestContrib + "e<sup>max</sup>"
				+ posBestValue + "v<sup>max</sup>"
				+ posNearRange + "v<sup>near</sup>"
				+ posDelta + "Δe"
				+ posDesc + "<b>Description</b>"
			);

			float duration = stats.duration;
			float durationMins = duration / 60f;
			float total = 0f;
			float totalMax = 0f;
			var nearFactor = ATRStatsConfig.Instance.nearFactor;
			for (int step = -1; step <= 16; step++) {
				string label = "?";
				float value = 0f;
				float contrib = 0f;
				float bestValue = 0f;
				float bestContrib = 0f;
				string bestContribText = null;
				string extraText = null;
				string nearText = null;
				bool showDelta = true;
				AnimationCurve curve = null;
				switch (step) {
					case -1:
						label = "Base";
						bestContrib = contrib = trackedRide.baseExcitement;
						bestValue = float.NaN;
						value = float.NaN;
						break;
					case 0:
						label = "Duration (min)";
						value = durationMins;
						curve = trackedRide.rideLengthTimeContributionExcitement;
						break;
					case 1:
						label = "DirCh/min";
						//value = stats.directionChangesPerMinute;
						//if (Mathf.Approximately(value, 0f)) { // fallback
						if (!Mathf.Approximately(durationMins, 0f)) {
							value = (float)stats.directionChanges / durationMins;
						} else value = 0;
						//}
						extraText = "total: " + stats.directionChanges;
						curve = trackedRide.directionChangesPerMinuteContributionExcitement;
						break;
					case 2:
						label = "Drops";
						value = (float)stats.drops;
						curve = trackedRide.dropsContributionExcitement;
						break;
					case 3:
						label = "Inversions";
						value = (float)stats.inversions;
						curve = trackedRide.inversionsContributionExcitement;
						break;
					case 4:
						label = "Air Time";
						value = stats.airTime;
						curve = trackedRide.airtimeContributionExcitement;
						break;
					case 5:
						label = "Velocity (max)";
						value = stats.maxVelocity * 3.6f;
						curve = trackedRide.velocityContributionExcitement;
						break;
					case 6:
						label = "Vertical G (max)";
						value = stats.maxVertG;
						curve = trackedRide.maxPositiveVertGContributionExcitement;
						break;
					case 7:
						label = "Long G (max) / accel.";
						value = stats.maxLongG;
						curve = trackedRide.accelerationContributionExcitement;
						break;
					case 8:
						label = "Lateral G (avg)";
						value = stats.averageLatG;
						curve = trackedRide.averageLatGContributionExcitement;
						break;
					case 9:
						label = "Tun. transitions";
						value = (float)stats.undergroundOvergroundChanges;
						contrib = Mathf.Clamp(-0.00277778f * Mathf.Pow(value, 2f) + 0.0416667f * value, 0f, 0.15f);
						bestValue = 6;
						nearText = "6-8";
						bestContrib = Mathf.Clamp(-0.00277778f * Mathf.Pow(bestValue, 2f) + 0.0416667f * bestValue, 0f, 0.15f);
						break;
					case 10:
						label = "Attachments";
						value = stats.trackAttachments;
						contrib = Mathf.Clamp(0.0097518f * value - 0.000623434f * Mathf.Pow(value, 2f) + 1.31672E-05f * Mathf.Pow(value, 3f), 0f, 0.05f);
						break;
					case 11:
						if (!trackedRide.isRace) continue;
						label = "Race cars";
						value = trackedRide.Track.trains.Count;
						const float carCountMult = 0.008f;
						contrib = Mathf.Min(value, 12f) * carCountMult;
						bestValue = 12;
						bestContrib = 12f * carCountMult;
						nearText = Mathf.CeilToInt(bestContrib * nearFactor / carCountMult) + "-";
						break;
					case 12:
						label = "Train length";
						value = trackedRide.trainLength;
						contrib = Mathf.Clamp(value / 100f, 0f, 0.05f);
						bestValue = float.NaN;
						bestContrib = contrib;
						bestContribText = "5*";
						showDelta = false;
						break;
					case 13:
						label = "Sync station bonus";
						value = float.NaN;
						var syncMult = trackedRide.getExcitementBonusMultiplier();
						contrib = total * (syncMult - 1);
						totalMax *= syncMult;
						bestValue = 4;
						bestContribText = "+11%";
						break;
					case 14:
						label = "Deco";
						value = trackedRide.getDecoResultScore();
						contrib = trackedRide.maxDecorationContributionExcitement * value;
						bestValue = 1;
						bestContrib = trackedRide.maxDecorationContributionExcitement;
						extraText = "r: " + (trackedRide.decoAttractionScore * 100f).ToString(nf1) + "%"
							+ ", q: " + (trackedRide.decoQueueScore * 100f).ToString(nf1) + "%";
						break;
					case 15:
						label = "Duration penalty";
						var durationMult = Mathf.Clamp01(0.000257143f * duration * duration + 0.0124286f * duration + 0.25f);
						contrib = -total * (1f - durationMult);
						value = float.NaN;
						break;
					case 16:
						label = "Intensity penalty";
						if (stats.ratingIntensity > 0.9f) {
							var newTotal = Mathf.Lerp(total, total * stats.excitementMultiplicatorIntensity, trackedRide.excitementImportanceIntensity);
							contrib = newTotal - total;
							total = newTotal;
						}
						value = float.NaN;
						break;
				} // switch (step)
				if (curve != null) {
					AnimationCurveCache cinfo;
					if (!AnimationCurveCache.cache.TryGetValue(curve, out cinfo)) {
						cinfo = new AnimationCurveCache(curve);
						AnimationCurveCache.cache[curve] = cinfo;
					}
					cinfo.calcNear(nearFactor);
					contrib = curve.Evaluate(value);
					bestValue = cinfo.bestKey;
					bestContrib = cinfo.bestValue;
					if (cinfo.isConstant) {
						extraText = "constant";
					} else {
						nearText = cinfo.minNearKey.ToString(nf1) + "-" + cinfo.maxNearKey.ToString(nf1);
					}
				}
				//
				if (Mathf.Approximately(bestContrib, 0f) && Mathf.Approximately(contrib, 0) && bestContribText == null) continue;

				stringBuilder.Append($"{(contrib * 100f).ToString(nf)}");
				if (!float.IsNaN(value)) stringBuilder.Append($"{posValue}{value.ToString(nf)}");

				//stringBuilder.Append("<b>" + label + ":</b> " + (outValue * 100f).ToString(nf));

				if (!Mathf.Approximately(bestContrib, 0f) || bestContribText != null) {
					stringBuilder.Append($"{posBestContrib}{bestContribText ?? (bestContrib * 100f).ToString(nf)}");
					if (!float.IsNaN(bestValue)) stringBuilder.Append($"{posBestValue}{bestValue.ToString(nf)}");
				}
				if (nearText != null) {
					stringBuilder.Append(posNearRange + nearText);
				}
				if (!Mathf.Approximately(bestContrib, 0f) && showDelta) {
					stringBuilder.Append($"{posDelta}{((bestContrib - contrib) * 100f).ToString(nf)}");
				}

				stringBuilder.Append($"{posDesc}<b>{label}</b>");
				if (extraText != null) stringBuilder.Append($" [{extraText}]");

				stringBuilder.AppendLine("");
				total += contrib;
				totalMax += bestContrib;
			} // for
			stringBuilder.Append((total * 100f).ToString(nf));
			stringBuilder.Append(posBestContrib + "~" + (totalMax * 100f).ToString(nf));
			stringBuilder.Append(posDelta + ((totalMax - total) * 100f).ToString(nf));
			stringBuilder.Append(posDesc + "<b>Total</b>");
			stringBuilder.AppendLine(" (~" + (total / totalMax * 100f).ToString(nf) + "%)");
			stringBuilder.AppendLine("<b>Last updated:</b> " + DateTime.Now.ToString());
			stringBuilder.AppendLine("");
			var newText = stringBuilder.ToString();

			var statsTextField = trav.Field<TextMeshProUGUI>("statsText");
			var statsTextDummy = statsTextField.Value;
			var statsTextDummyText = statsTextDummy.text;
			var statsTextSwap = __statsTextFieldSwap;
			__statsTextFieldSwap = null;

			if (trackedRide.statsAreOutdated) {
				const string endOutdated = "</b></color>";
				var pos = statsTextDummyText.IndexOf(endOutdated);
				if (pos >= 0) {
					pos += endOutdated.Length;
					if (statsTextDummyText[pos] == '\r') pos++;
					if (statsTextDummyText[pos] == '\n') pos++;
					statsTextSwap.text = statsTextDummyText.Insert(pos, newText);
				} else statsTextSwap.text = newText + statsTextDummyText;
			} else {
				statsTextSwap.text = newText + statsTextDummyText;
			}
			statsTextField.Value = statsTextSwap;
		}
	}
	[HarmonyPatch]
	public class ATRStatsPatch_TrackedRideStatsPanel_onHide {
		static MethodBase TargetMethod() => AccessTools.Method(typeof(TrackedRideStatsPanel), "onHide");

		[HarmonyPrefix]
		public static void Prefix(TrackedRideStatsPanel __instance) {
			AnimationCurveCache.cache.Clear();
		}
	}
}
