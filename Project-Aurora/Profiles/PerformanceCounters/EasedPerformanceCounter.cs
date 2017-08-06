using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Scripting.Runtime;

namespace Aurora.Profiles.PerformanceCounters
{
	public static class PerformanceCounterManager
	{
		private static readonly ConcurrentDictionary<Tuple<string, string, string>, Func<float>> InternalPerformanceCounters =
			new ConcurrentDictionary<Tuple<string, string, string>, Func<float>>();

		private static readonly ConcurrentDictionary<Tuple<string, string, string>, Func<float>>
			CreatedSystemPerformanceCounters =
				new ConcurrentDictionary<Tuple<string, string, string>, Func<float>>();

		private static readonly ConcurrentDictionary<Tuple<string, string, string, long>, IntervalPerformanceCounter>
			CountersInstances =
				new ConcurrentDictionary<Tuple<string, string, string, long>, IntervalPerformanceCounter>();

		private static readonly ConcurrentQueue<IntervalPerformanceCounter> NewCounters
			= new ConcurrentQueue<IntervalPerformanceCounter>();

		public static void RegisterInternal(string categoryName, string counterName, string instanceName,
			[NotNull] Func<float> newSample)
		{
			InternalPerformanceCounters.AddOrUpdate(new Tuple<string, string, string>(categoryName, counterName, instanceName),
				newSample, (tuple, func) => newSample);
		}

		public static Func<float> GetSystemPerformanceCounter(string categoryName, string counterName, string instanceName)
			=> GetSystemPerformanceCounter(new Tuple<string, string, string>(categoryName, counterName, instanceName));

		public static Func<float> GetSystemPerformanceCounter(Tuple<string, string, string> key)
			=> CreatedSystemPerformanceCounters.GetOrAdd(key, tuple2 =>
			{
				var performanceCounter = new PerformanceCounter(key.Item1, key.Item2, key.Item3);
				return () => performanceCounter.NextValue();
			});

		public static IntervalPerformanceCounter GetCounter(string categoryName, string counterName, string instanceName,
			long updateInterval)
		{
			return CountersInstances.GetOrAdd(
				new Tuple<string, string, string, long>(categoryName, counterName, instanceName, updateInterval),
				tuple =>
				{
					var key = new Tuple<string, string, string>(categoryName, counterName, instanceName);
					Func<float> value;
					if (!InternalPerformanceCounters.TryGetValue(key, out value))
					{
						value = GetSystemPerformanceCounter(key);
					}
					var newCounter = new IntervalPerformanceCounter(tuple, (int)Math.Ceiling(3000f / updateInterval), value);
					NewCounters.Enqueue(newCounter);
					return newCounter;
				});
		}

		public sealed partial class IntervalPerformanceCounter
		{
			private sealed class IntervalCounterList : List<IntervalPerformanceCounter>
			{
				public readonly long Interval;
				public DateTime NextUpdate;

				public IntervalCounterList(long interval)
				{
					Interval = interval;
				}
			}

			private static readonly List<IntervalCounterList> Intervals = new List<IntervalCounterList>();
			private static readonly Timer Timer = new Timer(UpdateTick, null, Timeout.Infinite, Timeout.Infinite);
			private static int sleeping = 1;

			private static void UpdateTick(object state)
			{
				IntervalPerformanceCounter newCounter;
				if (!NewCounters.IsEmpty)
				{
					while (NewCounters.TryDequeue(out newCounter))
					{
						var interval = Intervals.FirstOrDefault(x => x.Interval == newCounter.UpdateInterval);
						if (interval == null)
						{
							interval = new IntervalCounterList(newCounter.UpdateInterval);
							Intervals.Add(interval);
						}
						interval.Add(newCounter);
					}
				}

				var time = DateTime.UtcNow;
				var nextUpdate = DateTime.MaxValue;
				var activeCounters = false;
				foreach (var interval in Intervals)
				{
					if (interval.NextUpdate <= time)
					{
						interval.NextUpdate = time.AddMilliseconds(interval.Interval);
						foreach (var counter in interval)
						{
							if (Volatile.Read(ref counter.counterUsage) > 0)
							{
								try
								{
									Volatile.Write(ref counter.lastFrame,
										new CounterFrame(counter.lastFrame.CurrentValue, counter.newSample()));
								}
								catch (Exception exc)
								{
									Global.logger.LogLine("IntervalPerformanceCounter exception in "
										+ $"{counter.CategoryName}/{counter.CounterName}/{counter.InstanceName}/{counter.UpdateInterval}: {exc}",
										Logging_Level.Error);
								}
								counter.counterUsage--;
								activeCounters = true;
							}
						}
					}
					nextUpdate = nextUpdate > interval.NextUpdate ? interval.NextUpdate : nextUpdate;
				}
				if (activeCounters)
				{
					var nextDelay = nextUpdate - DateTime.UtcNow;
					Timer.Change(nextDelay.Ticks > 0 ? nextDelay : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
				}
				else
				{
					Volatile.Write(ref sleeping, 1);
				}
			}
		}

		public sealed partial class IntervalPerformanceCounter
		{
			public Tuple<string, string, string, long> Key { get; }
			public string CategoryName => Key.Item1;
			public string CounterName => Key.Item2;
			public string InstanceName => Key.Item3;
			public long UpdateInterval => Key.Item4;
			public int IdleTimeout { get; }

			private sealed class CounterFrame
			{
				public readonly float PreviousValue;
				public readonly float CurrentValue;
				public readonly long Timestamp;

				public CounterFrame(float previousValue, float currentValue)
				{
					PreviousValue = previousValue;
					CurrentValue = currentValue;
					Timestamp = Utils.Time.GetMillisecondsSinceEpoch();
				}
			}

			private CounterFrame lastFrame = new CounterFrame(0, 0);
			private readonly Func<float> newSample;

			private int counterUsage;

			public float GetValue(bool easing = true)
			{
				Volatile.Write(ref counterUsage, IdleTimeout);

				if (Volatile.Read(ref sleeping) == 1)
				{
					if (Interlocked.CompareExchange(ref sleeping, 0, 1) == 1)
					{
						Timer.Change(0, Timeout.Infinite);
					}
				}

				var frame = Volatile.Read(ref lastFrame);
				if (!easing)
					return frame.CurrentValue;

				return frame.PreviousValue + (frame.CurrentValue - frame.PreviousValue) *
					   Math.Min(Utils.Time.GetMillisecondsSinceEpoch() - frame.Timestamp, UpdateInterval) / UpdateInterval;
			}

			public IntervalPerformanceCounter(Tuple<string, string, string, long> key, int idleTimeout, Func<float> newSample)
			{
				this.newSample = newSample;
				Key = key;
				IdleTimeout = idleTimeout;
			}
		}
	}
}