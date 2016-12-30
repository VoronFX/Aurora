using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

	public abstract class EasedPerformanceCounter<T>
	{
		public int UpdateInterval { get; set; } = 1000;
		public int IdleTimeout { get; set; } = 3;

		protected struct CounterFrame<T>
		{
			public readonly T PreviousValue;
			public readonly T CurrentValue;
			public readonly long Timestamp;

			public CounterFrame(T previousValue, T currentValue)
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
}