using System;
using System.Diagnostics;
using System.Linq;
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

		private int counterUsage;
		private Task sleepingAwaiter = Task.FromResult(true);
		private TaskCompletionSource<bool> sleeping;

		protected abstract T GetEasedValue(CounterFrame<T> currentFrame);
		protected abstract T UpdateValue();

		public T GetValue(bool easing = true)
		{
			counterUsage = IdleTimeout;
			if (sleepingAwaiter.Status == TaskStatus.WaitingForActivation)
				Task.Run(() => sleeping.TrySetResult(true));
			if (easing)
			{
				lastEasedValue = GetEasedValue(frame);
				return lastEasedValue;
			}
			return frame.CurrentValue;
		}

		protected EasedPerformanceCounter()
		{
			Task.Run((Action)(async () =>
		   {
			   while (true)
			   {
					try
					{
						while (true)
					   {
						   counterUsage--;
						   if (counterUsage <= 0)
						   {
							   sleeping = new TaskCompletionSource<bool>();
							   sleepingAwaiter = sleeping.Task;
							   await sleepingAwaiter;
						   }

						   frame = new CounterFrame<T>(lastEasedValue, UpdateValue());

						   await Task.Delay(UpdateInterval);
					   }
				   }
				   catch (Exception exc)
				   {
					   Global.logger.LogLine("EasedPerformanceCounter exception: " + exc, Logging_Level.Error);
				   }
				   await Task.Delay(UpdateInterval);
			   }
		   }));
		}
	}
}