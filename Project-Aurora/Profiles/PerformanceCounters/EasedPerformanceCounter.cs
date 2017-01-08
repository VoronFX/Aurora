using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Scripting.Runtime;

namespace Aurora.Profiles.PerformanceCounters
{
	public abstract class EasedPerformanceCounterMultiFloat
		: EasedPerformanceCounter<float[]>
	{
		private readonly float[] defaultValues;

		protected EasedPerformanceCounterMultiFloat(float[] defaultValues)
		{
			this.defaultValues = defaultValues;
		}

		protected override float[] GetEasedValue(CounterFrame<float[]> currentFrame)
		{
			var prev = currentFrame.PreviousValue ?? defaultValues;
			var curr = currentFrame.CurrentValue ?? defaultValues;

			return prev.Select((x, i) => x + (curr[i] - x) * Math.Min(Utils.Time.GetMillisecondsSinceEpoch()
				- currentFrame.Timestamp, UpdateInterval) / UpdateInterval).ToArray();
		}
	}

	public abstract class EasedPerformanceCounterFloat
		: EasedPerformanceCounter<float>
	{
		protected override float GetEasedValue(CounterFrame<float> currentFrame)
		{
			return currentFrame.PreviousValue + (currentFrame.CurrentValue - currentFrame.PreviousValue) *
				   Math.Min(Utils.Time.GetMillisecondsSinceEpoch() - currentFrame.Timestamp, UpdateInterval) / UpdateInterval;
		}
	}
	//Aurora Internal/GpuNVidia#0/CoreUsage
	//Graphic Card/ATI#0/CoreUsage

	public abstract class EasedPerformanceCounter<T>
	{
		public int UpdateInterval { get; set; } = 1000;
		public int IdleTimeout { get; set; } = 3;

		protected struct CounterFrame<T2>
		{
			public readonly T2 PreviousValue;
			public readonly T2 CurrentValue;
			public readonly long Timestamp;

			public CounterFrame(T2 previousValue, T2 currentValue)
			{
				PreviousValue = previousValue;
				CurrentValue = currentValue;
				Timestamp = Utils.Time.GetMillisecondsSinceEpoch();
			}
		}

		private CounterFrame<T> frame;
		private T lastEasedValue;

		private readonly Timer timer;
		private int counterUsage;
		private bool sleeping = true;
		private int awakening;

		protected abstract T GetEasedValue(CounterFrame<T> currentFrame);
		protected abstract T UpdateValue();

		public T GetValue(bool easing = true)
		{
			counterUsage = IdleTimeout;
			if (sleeping)
			{
				if (Interlocked.CompareExchange(ref awakening, 1, 0) == 1)
				{
					sleeping = false;
					timer.Change(0, Timeout.Infinite);
				}
			}

			if (easing)
			{
				lastEasedValue = GetEasedValue(frame);
				return lastEasedValue;
			}
			return frame.CurrentValue;
		}

		protected EasedPerformanceCounter()
		{
			timer = new Timer(UpdateTick, null, Timeout.Infinite, Timeout.Infinite);
		}

		private void UpdateTick(object state)
		{
			try
			{
				frame = new CounterFrame<T>(lastEasedValue, UpdateValue());
			}
			catch (Exception exc)
			{
				Global.logger.LogLine("EasedPerformanceCounter exception: " + exc, Logging_Level.Error);
			}
			finally
			{
				counterUsage--;
				if (counterUsage <= 0)
				{
					awakening = 0;
					sleeping = true;
				}
				else
				{
					timer.Change(UpdateInterval, Timeout.Infinite);
				}
			}
		}
	}

	public class PerformanceCounterManager
	{
		private static readonly ConcurrentDictionary<Tuple<string, string, string>, Func<float>> InternalPerformanceCounters =
			new ConcurrentDictionary<Tuple<string, string, string>, Func<float>>();

		public static void RegisterInternal(string categoryName, string counterName, string instanceName, [NotNull]Func<float> newSample)
		{
			InternalPerformanceCounters.AddOrUpdate(new Tuple<string, string, string>(categoryName, counterName, instanceName),
				newSample, (tuple, func) => newSample);
		}

		private readonly ConcurrentDictionary<Tuple<string, string, string, long>, IntervalPerformanceCounter<float>> countersInstances =
			new ConcurrentDictionary<Tuple<string, string, string, long>, IntervalPerformanceCounter<float>>();


		public IntervalPerformanceCounter<float> GetCounter(string categoryName, string counterName, string instanceName, long updateInterval)
		{
			return countersInstances.GetOrAdd(
				new Tuple<string, string, string, long>(categoryName, counterName, instanceName, updateInterval),
				tuple =>
				{
					Func<float> value;
					if (!InternalPerformanceCounters.TryGetValue(null, out value))
					{
						var performanceCounter = new PerformanceCounter(categoryName, counterName, instanceName);
						value = () => performanceCounter.NextValue();
					}
					return new IntervalPerformanceCounter<float>(categoryName, counterName, instanceName,
						updateInterval, (int)Math.Ceiling(3000f / updateInterval), value);
				});
		}

		public class IntervalPerformanceCounter<T>
		{
			public string CategoryName { get; }
			public string CounterName { get; }
			public string InstanceName { get; }
			public long UpdateInterval { get; }
			public int IdleTimeout { get; }

			private T lastValue;
			private readonly Func<T> newSample;

			public bool IsSleeping => sleeping;

			private readonly Timer timer;
			private int counterUsage;
			private bool sleeping = true;
			private int awakening;

			public T GetValue()
			{
				counterUsage = IdleTimeout;
				if (sleeping)
				{
					if (Interlocked.CompareExchange(ref awakening, 1, 0) == 1)
					{
						sleeping = false;
						timer.Change(0, Timeout.Infinite);
					}
				}

				return lastValue;
			}

			public IntervalPerformanceCounter(string categoryName, string counterName, string instanceName,
				long updateInterval, int idleTimeout, Func<T> newSample)
			{
				this.newSample = newSample;
				UpdateInterval = updateInterval;
				IdleTimeout = idleTimeout;
				CategoryName = categoryName;
				CounterName = counterName;
				InstanceName = instanceName;
				timer = new Timer(UpdateTick, null, Timeout.Infinite, Timeout.Infinite);
			}

			private void UpdateTick(object state)
			{
				try
				{
					lastValue = newSample();
				}
				catch (Exception exc)
				{
					Global.logger.LogLine($"IntervalPerformanceCounter exception in {CategoryName}/{CounterName}/{InstanceName}/{UpdateInterval}: {exc}", Logging_Level.Error);
				}
				finally
				{
					counterUsage--;
					if (counterUsage <= 0)
					{
						awakening = 0;
						sleeping = true;
					}
					else
					{
						timer.Change(UpdateInterval, Timeout.Infinite);
					}
				}
			}
		}
	}


}