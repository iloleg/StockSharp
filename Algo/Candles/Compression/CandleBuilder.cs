#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: StockSharp.Algo.Candles.Compression.Algo
File: CandleBuilder.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Algo.Candles.Compression
{
	using System;
	using System.Collections.Generic;

	using Ecng.Common;
	using Ecng.Configuration;

	using StockSharp.Algo.Storages;
	using StockSharp.Logging;
	using StockSharp.Messages;
	using StockSharp.Localization;

	/// <summary>
	/// Candles builder.
	/// </summary>
	/// <typeparam name="TCandleMessage">The type of candle which the builder will create.</typeparam>
	public abstract class CandleBuilder<TCandleMessage> : BaseLogReceiver, ICandleBuilder
		where TCandleMessage : CandleMessage
	{
		/// <summary>
		/// The candle type.
		/// </summary>
		public abstract MarketDataTypes CandleType { get; }

		/// <summary>
		/// Initialize <see cref="CandleBuilder{TCandleMessage}"/>.
		/// </summary>
		protected CandleBuilder()
		{
		}

		/// <summary>
		/// To process the new data.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">The new data by which it is decided to start or end the current candle creation.</param>
		/// <returns>A new candles changes.</returns>
		public IEnumerable<CandleMessage> Process(MarketDataMessage message, CandleMessage currentCandle, ICandleBuilderSourceValue value)
		{
			var changes = new List<CandleMessage>();

			Process(message, currentCandle, value, changes);

			return changes;
		}

		/// <summary>
		/// To process the new data.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">The new data by which it is decided to start or end the current candle creation.</param>
		/// <param name="changes">A new candles changes.</param>
		public virtual void Process(MarketDataMessage message, CandleMessage currentCandle, ICandleBuilderSourceValue value, IList<CandleMessage> changes)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			if (value == null)
				throw new ArgumentNullException(nameof(value));

			var candle = ProcessValue(message, (TCandleMessage)currentCandle, value);

			if (candle == null)
			{
				// skip the value that cannot be processed
				return;
			}

			if (candle == currentCandle)
			{
				if (message.IsCalcVolumeProfile)
				{
					if (candle.VolumeProfile == null)
						throw new InvalidOperationException();

					candle.VolumeProfile.Update(value);
				}

				//candle.State = CandleStates.Changed;
				changes.Add(candle);
			}
			else
			{
				if (currentCandle != null)
				{
					currentCandle.State = CandleStates.Finished;
					changes.Add(currentCandle);
				}

				if (message.IsCalcVolumeProfile)
				{
					candle.VolumeProfile = new CandleMessageVolumeProfile();
					candle.VolumeProfile.Update(value);
				}

				candle.State = CandleStates.Active;
				changes.Add(candle);
			}
		}

		/// <summary>
		/// To create a new candle.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">Data with which a new candle should be created.</param>
		/// <returns>Created candle.</returns>
		protected virtual TCandleMessage CreateCandle(MarketDataMessage message, TCandleMessage currentCandle, ICandleBuilderSourceValue value)
		{
			throw new NotSupportedException(LocalizedStrings.Str637);
		}

		/// <summary>
		/// Whether the candle is created before data adding.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data by which it is decided to end the current candle creation.</param>
		/// <returns><see langword="true" /> if the candle should be finished. Otherwise, <see langword="false" />.</returns>
		protected virtual bool IsCandleFinishedBeforeChange(MarketDataMessage message, TCandleMessage candle, ICandleBuilderSourceValue value)
		{
			return false;
		}

		/// <summary>
		/// To fill in the initial candle settings.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data.</param>
		/// <returns>Candle.</returns>
		protected virtual TCandleMessage FirstInitCandle(MarketDataMessage message, TCandleMessage candle, ICandleBuilderSourceValue value)
		{
			if (candle == null)
				throw new ArgumentNullException(nameof(candle));

			if (value == null)
				throw new ArgumentNullException(nameof(value));

			candle.SecurityId = message.SecurityId;

			candle.OpenPrice = value.Price;
			candle.ClosePrice = value.Price;
			candle.LowPrice = value.Price;
			candle.HighPrice = value.Price;
			//candle.TotalPrice = value.Price;

			candle.OpenVolume = value.Volume;
			candle.CloseVolume = value.Volume;
			candle.LowVolume = value.Volume;
			candle.HighVolume = value.Volume;

			if (value.Volume != null)
				candle.TotalVolume = value.Volume.Value;

			return candle;
		}

		/// <summary>
		/// To update the candle data.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data.</param>
		protected virtual void UpdateCandle(MarketDataMessage message, TCandleMessage candle, ICandleBuilderSourceValue value)
		{
			if (candle == null)
				throw new ArgumentNullException(nameof(candle));

			if (value == null)
				throw new ArgumentNullException(nameof(value));

			Update(candle, value);
		}

		private static void Update(TCandleMessage candle, ICandleBuilderSourceValue value)
		{
			var price = value.Price;
			var time = value.Time;
			var volume = value.Volume;

			if (price < candle.LowPrice)
			{
				candle.LowPrice = price;
				candle.LowTime = time;
				candle.LowVolume = volume;
			}

			if (price > candle.HighPrice)
			{
				candle.HighPrice = price;
				candle.HighTime = time;
				candle.HighVolume = volume;
			}

			candle.ClosePrice = price;

			if (volume != null)
			{
				var v = volume.Value;

				candle.TotalPrice += price * v;

				candle.CloseVolume = v;
				candle.TotalVolume += v;

				var dir = value.OrderDirection;
				if (dir != null)
					candle.RelativeVolume = (candle.RelativeVolume ?? 0) + (dir.Value == Sides.Buy ? v : -v);
			}

			candle.CloseTime = time;
		}

		/// <summary>
		/// To process the new data.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">The new data by which it is decided to start or end the current candle creation.</param>
		/// <returns>A new candle. If there is not necessary to create a new candle, then <paramref name="currentCandle" /> is returned. If it is impossible to create a new candle (<paramref name="value" /> cannot be applied to candles), then <see langword="null" /> is returned.</returns>
		protected virtual TCandleMessage ProcessValue(MarketDataMessage message, TCandleMessage currentCandle, ICandleBuilderSourceValue value)
		{
			if (currentCandle == null || IsCandleFinishedBeforeChange(message, currentCandle, value))
			{
				currentCandle = CreateCandle(message, currentCandle, value);
				this.AddDebugLog("NewCandle {0} ForValue {1}", currentCandle, value);
				return currentCandle;
			}

			UpdateCandle(message, currentCandle, value);

			// TODO performance
			//this.AddDebugLog("UpdatedCandle {0} ForValue {1}", currentCandle, value);

			return currentCandle;
		}

		///// <summary>
		///// To finish the candle forcibly.
		///// </summary>
		///// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		///// <param name="candleMessage">Candle.</param>
		//protected void ForceFinishCandle(MarketDataMessage message, CandleMessage candleMessage)
		//{
		//	var info = _info.TryGetValue(message);

		//	if (info == null)
		//		return;

		//	var isNone = candleMessage.State == CandleStates.None;

		//	// если успела прийти новая свеча
		//	if (isNone && info.CurrentCandle != null)
		//		return;

		//	if (!isNone && info.CurrentCandle != candleMessage)
		//		return;

		//	info.CurrentCandle = isNone ? null : candleMessage;

		//	if (!isNone)
		//		candleMessage.State = CandleStates.Finished;

		//	RaiseProcessing(series, candleMessage);
		//}
	}

	/// <summary>
	/// The builder of candles of <see cref="TimeFrameCandleMessage"/> type.
	/// </summary>
	public class TimeFrameCandleBuilder : CandleBuilder<TimeFrameCandleMessage>
	{
		private readonly IExchangeInfoProvider _exchangeInfoProvider;
		//private sealed class TimeoutInfo : Disposable
		//{
		//	private readonly MarketTimer _timer;
		//	private DateTime _emptyCandleTime;
		//	private readonly TimeSpan _timeFrame;
		//	private readonly TimeSpan _offset;
		//	private DateTime _nextTime;

		//	public TimeoutInfo(CandleSeries series, TimeFrameCandleBuilder builder)
		//	{
		//		if (series == null)
		//			throw new ArgumentNullException(nameof(series));

		//		if (builder == null)
		//			throw new ArgumentNullException(nameof(builder));

		//		_timeFrame = (TimeSpan)series.Arg;
		//		_offset = TimeSpan.FromTicks((long)((decimal)((decimal)_timeFrame.Ticks + builder.Timeout)));

		//		var security = series.Security;
		//		var connector = security.Connector;

		//		var isFirstTime = true;

		//		_timer = new MarketTimer(connector, () =>
		//		{
		//			if (isFirstTime)
		//			{
		//				isFirstTime = false;

		//				var bounds = _timeFrame.GetCandleBounds(security);

		//				_emptyCandleTime = bounds.Min;
		//				_nextTime = GetLimitTime(bounds.Min);

		//				return;
		//			}

		//			if (security.GetMarketTime() >= _nextTime)
		//			{
		//				_nextTime += _timeFrame;

		//				var candle = LastCandle;

		//				if (candle == null)
		//				{
		//					candle = new TimeFrameCandle
		//					{
		//						Security = security,
		//						TimeFrame = _timeFrame,
		//						OpenTime = _emptyCandleTime,
		//						CloseTime = _emptyCandleTime + _timeFrame,
		//					};

		//					_emptyCandleTime += _timeFrame;

		//					if (!builder.GenerateEmptyCandles)
		//						return;
		//				}

		//				builder.ForceFinishCandle(series, candle);
		//			}
		//		}).Interval(_timeFrame).Start();
		//	}

		//	private TimeFrameCandle _lastCandle;

		//	public TimeFrameCandle LastCandle
		//	{
		//		private get { return _lastCandle; }
		//		set
		//		{
		//			_lastCandle = value;
		//			_emptyCandleTime = value.OpenTime + _timeFrame;
		//			_nextTime = GetLimitTime(value.OpenTime);
		//		}
		//	}

		//	private DateTime GetLimitTime(DateTime currentCandleTime)
		//	{
		//		return currentCandleTime + _offset;
		//	}

		//	protected override void DisposeManaged()
		//	{
		//		base.DisposeManaged();
		//		_timer.Dispose();
		//	}
		//}

		//private readonly SynchronizedDictionary<CandleSeries, TimeoutInfo> _timeoutInfos = new SynchronizedDictionary<CandleSeries, TimeoutInfo>();

		/// <summary>
		/// The candle type.
		/// </summary>
		public override MarketDataTypes CandleType => MarketDataTypes.CandleTimeFrame;

		/// <summary>
		/// Whether to create empty candles (<see cref="CandleStates.None"/>) in the lack of trades. The default mode is enabled.
		/// </summary>
		public bool GenerateEmptyCandles { get; set; }

		private Unit _timeout = 10.Percents();

		/// <summary>
		/// The time shift from the time frame end after which a signal is sent to close the unclosed candle forcibly. The default is 10% of the time frame.
		/// </summary>
		public Unit Timeout
		{
			get => _timeout;
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				if (value < 0)
					throw new ArgumentOutOfRangeException(nameof(value), value, LocalizedStrings.OffsetValueIncorrect);

				_timeout = value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TimeFrameCandleBuilder"/>.
		/// </summary>
		public TimeFrameCandleBuilder()
			: this(ConfigManager.TryGetService<IExchangeInfoProvider>() ?? new InMemoryExchangeInfoProvider())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TimeFrameCandleBuilder"/>.
		/// </summary>
		/// <param name="exchangeInfoProvider">The exchange boards provider.</param>
		public TimeFrameCandleBuilder(IExchangeInfoProvider exchangeInfoProvider)
		{
			if (exchangeInfoProvider == null)
				throw new ArgumentNullException(nameof(exchangeInfoProvider));

			_exchangeInfoProvider = exchangeInfoProvider;

			GenerateEmptyCandles = true;
		}

		///// <summary>
		///// Reset state.
		///// </summary>
		//public override void Reset()
		//{
		//	base.Reset();

		//	_timeoutInfos.Clear();
		//}

		/// <summary>
		/// To create a new candle.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">Data with which a new candle should be created.</param>
		/// <returns>Created candle.</returns>
		protected override TimeFrameCandleMessage CreateCandle(MarketDataMessage message, TimeFrameCandleMessage currentCandle, ICandleBuilderSourceValue value)
		{
			var timeFrame = (TimeSpan)message.Arg;

			var board = _exchangeInfoProvider.GetOrCreateBoard(message.SecurityId.BoardCode);
			var bounds = timeFrame.GetCandleBounds(value.Time, board, board.WorkingTime);

			if (value.Time < bounds.Min)
				return null;

			var openTime = bounds.Min;

			var candle = FirstInitCandle(message, new TimeFrameCandleMessage
			{
				TimeFrame = timeFrame,
				OpenTime = openTime,
				HighTime = openTime,
				LowTime = openTime,
				CloseTime = openTime, // реальное окончание свечи определяет по последней сделке
			}, value);

			return candle;
		}

		/// <summary>
		/// Whether the candle is created before data adding.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data by which it is decided to end the current candle creation.</param>
		/// <returns><see langword="true" /> if the candle should be finished. Otherwise, <see langword="false" />.</returns>
		protected override bool IsCandleFinishedBeforeChange(MarketDataMessage message, TimeFrameCandleMessage candle, ICandleBuilderSourceValue value)
		{
			return value.Time < candle.OpenTime || (candle.OpenTime + candle.TimeFrame) <= value.Time;
		}
	}

	/// <summary>
	/// The builder of candles of <see cref="TickCandleMessage"/> type.
	/// </summary>
	public class TickCandleBuilder : CandleBuilder<TickCandleMessage>
	{
		/// <summary>
		/// The candle type.
		/// </summary>
		public override MarketDataTypes CandleType => MarketDataTypes.CandleTick;

		/// <summary>
		/// Initializes a new instance of the <see cref="TickCandleBuilder"/>.
		/// </summary>
		public TickCandleBuilder()
		{
		}

		/// <summary>
		/// To create a new candle.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">Data with which a new candle should be created.</param>
		/// <returns>Created candle.</returns>
		protected override TickCandleMessage CreateCandle(MarketDataMessage message, TickCandleMessage currentCandle, ICandleBuilderSourceValue value)
		{
			return FirstInitCandle(message, new TickCandleMessage
			{
				MaxTradeCount = (int)message.Arg,
				OpenTime = value.Time,
				CloseTime = value.Time,
				HighTime = value.Time,
				LowTime = value.Time,
				TotalTicks = 1,
			}, value);
		}

		/// <summary>
		/// Whether the candle is created before data adding.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data by which it is decided to end the current candle creation.</param>
		/// <returns><see langword="true" /> if the candle should be finished. Otherwise, <see langword="false" />.</returns>
		protected override bool IsCandleFinishedBeforeChange(MarketDataMessage message, TickCandleMessage candle, ICandleBuilderSourceValue value)
		{
			return candle.TotalTicks != null && candle.TotalTicks.Value >= candle.MaxTradeCount;
		}

		/// <summary>
		/// To update the candle data.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data.</param>
		protected override void UpdateCandle(MarketDataMessage message, TickCandleMessage candle, ICandleBuilderSourceValue value)
		{
			base.UpdateCandle(message, candle, value);

			var ticks = candle.TotalTicks;

			if (ticks == null)
				throw new InvalidOperationException();

			candle.TotalTicks = ticks.Value + 1;
		}
	}

	/// <summary>
	/// The builder of candles of <see cref="VolumeCandleMessage"/> type.
	/// </summary>
	public class VolumeCandleBuilder : CandleBuilder<VolumeCandleMessage>
	{
		/// <summary>
		/// The candle type.
		/// </summary>
		public override MarketDataTypes CandleType => MarketDataTypes.CandleVolume;

		/// <summary>
		/// Initializes a new instance of the <see cref="VolumeCandleBuilder"/>.
		/// </summary>
		public VolumeCandleBuilder()
		{
		}

		/// <summary>
		/// To create a new candle.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">Data with which a new candle should be created.</param>
		/// <returns>Created candle.</returns>
		protected override VolumeCandleMessage CreateCandle(MarketDataMessage message, VolumeCandleMessage currentCandle, ICandleBuilderSourceValue value)
		{
			return FirstInitCandle(message, new VolumeCandleMessage
			{
				Volume = (decimal)message.Arg,
				OpenTime = value.Time,
				CloseTime = value.Time,
				HighTime = value.Time,
				LowTime = value.Time,
			}, value);
		}

		/// <summary>
		/// Whether the candle is created before data adding.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data by which it is decided to end the current candle creation.</param>
		/// <returns><see langword="true" /> if the candle should be finished. Otherwise, <see langword="false" />.</returns>
		protected override bool IsCandleFinishedBeforeChange(MarketDataMessage message, VolumeCandleMessage candle, ICandleBuilderSourceValue value)
		{
			return candle.TotalVolume >= candle.Volume;
		}
	}

	/// <summary>
	/// The builder of candles of <see cref="RangeCandleMessage"/> type.
	/// </summary>
	public class RangeCandleBuilder : CandleBuilder<RangeCandleMessage>
	{
		/// <summary>
		/// The candle type.
		/// </summary>
		public override MarketDataTypes CandleType => MarketDataTypes.CandleRange;

		/// <summary>
		/// Initializes a new instance of the <see cref="RangeCandleBuilder"/>.
		/// </summary>
		public RangeCandleBuilder()
		{
		}

		/// <summary>
		/// To create a new candle.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="currentCandle">The current candle.</param>
		/// <param name="value">Data with which a new candle should be created.</param>
		/// <returns>Created candle.</returns>
		protected override RangeCandleMessage CreateCandle(MarketDataMessage message, RangeCandleMessage currentCandle, ICandleBuilderSourceValue value)
		{
			return FirstInitCandle(message, new RangeCandleMessage
			{
				PriceRange = (Unit)message.Arg,
				OpenTime = value.Time,
				CloseTime = value.Time,
				HighTime = value.Time,
				LowTime = value.Time,
			}, value);
		}

		/// <summary>
		/// Whether the candle is created before data adding.
		/// </summary>
		/// <param name="message">Market-data message (uses as a subscribe/unsubscribe in outgoing case, confirmation event in incoming case).</param>
		/// <param name="candle">Candle.</param>
		/// <param name="value">Data by which it is decided to end the current candle creation.</param>
		/// <returns><see langword="true" /> if the candle should be finished. Otherwise, <see langword="false" />.</returns>
		protected override bool IsCandleFinishedBeforeChange(MarketDataMessage message, RangeCandleMessage candle, ICandleBuilderSourceValue value)
		{
			return (decimal)(candle.LowPrice + candle.PriceRange) <= candle.HighPrice;
		}
	}

	/// <summary>
	/// The builder of candles of <see cref="PnFCandleMessage"/> type.
	/// </summary>
	public class PnFCandleBuilder : CandleBuilder<PnFCandleMessage>
	{
		/// <summary>
		/// The candle type.
		/// </summary>
		public override MarketDataTypes CandleType => MarketDataTypes.CandlePnF;

		/// <summary>
		/// Initializes a new instance of the <see cref="PnFCandleBuilder"/>.
		/// </summary>
		public PnFCandleBuilder()
		{
		}

		/// <inheritdoc />
		public override void Process(MarketDataMessage message, CandleMessage currentCandle, ICandleBuilderSourceValue value, IList<CandleMessage> changes)
		{
			var currentPnFCandle = (PnFCandleMessage)currentCandle;

			var price = value.Price;
			var volume = value.Volume;
			var time = value.Time;
			var side = value.OrderDirection;
			var pnf = (PnFArg)message.Arg;

			var pnfStep = (decimal)(1 * pnf.BoxSize);

			if (currentPnFCandle == null)
			{
				var openPrice = MathHelper.Floor(price, pnfStep);
				var highPrice = openPrice + pnfStep;

				changes.Add(CreateCandle(message, pnf, openPrice, highPrice, openPrice, highPrice, price, volume, side, time));
			}
			else
			{
				if (currentPnFCandle.LowPrice <= price && price <= currentPnFCandle.HighPrice)
				{
					UpdateCandle(currentPnFCandle, price, volume, time, side);
					changes.Add(currentPnFCandle);
				}
				else
				{
					var isX = currentPnFCandle.OpenPrice < currentPnFCandle.ClosePrice;

					if (isX)
					{
						if (price > currentPnFCandle.HighPrice)
						{
							currentPnFCandle.HighPrice = currentPnFCandle.ClosePrice = MathHelper.Floor(price, pnfStep) + pnfStep;
							UpdateCandle(currentPnFCandle, price, volume, time, side);
							changes.Add(currentPnFCandle);
						}
						else if (price < (currentPnFCandle.HighPrice - pnfStep * pnf.ReversalAmount))
						{
							currentPnFCandle.State = CandleStates.Finished;
							changes.Add(currentPnFCandle);

							var highPrice = currentPnFCandle.HighPrice - pnfStep;
							var lowPrice = MathHelper.Floor(price, pnfStep);

							currentPnFCandle = CreateCandle(message, pnf, highPrice, highPrice, lowPrice, lowPrice, price, volume, side, time);
							changes.Add(currentPnFCandle);
						}
						else
						{
							UpdateCandle(currentPnFCandle, price, volume, time, side);
							changes.Add(currentPnFCandle);
						}
					}
					else
					{
						if (price < currentPnFCandle.LowPrice)
						{
							currentPnFCandle.LowPrice = currentPnFCandle.ClosePrice = MathHelper.Floor(price, pnfStep);
							UpdateCandle(currentPnFCandle, price, volume, time, side);
							changes.Add(currentPnFCandle);
						}
						else if (price > (currentPnFCandle.LowPrice + pnfStep * pnf.ReversalAmount))
						{
							currentPnFCandle.State = CandleStates.Finished;
							changes.Add(currentPnFCandle);

							var highPrice = MathHelper.Floor(price, pnfStep) + pnfStep;
							var lowPrice = currentPnFCandle.LowPrice + pnfStep;

							currentPnFCandle = CreateCandle(message, pnf, lowPrice, highPrice, lowPrice, highPrice, price, volume, side, time);
							changes.Add(currentPnFCandle);
						}
						else
						{
							UpdateCandle(currentPnFCandle, price, volume, time, side);
							changes.Add(currentPnFCandle);
						}
					}
				}
			}
		}

		private static void UpdateCandle(PnFCandleMessage currentPnFCandle, decimal price, decimal? volume, DateTimeOffset time, Sides? side)
		{
			currentPnFCandle.TotalTicks = currentPnFCandle.TotalTicks ?? 0 + 1;

			if (volume != null)
			{
				var v = volume.Value;

				currentPnFCandle.TotalVolume += v;
				currentPnFCandle.TotalPrice += v * price;

				currentPnFCandle.RelativeVolume = currentPnFCandle.RelativeVolume ?? 0 + (side == Sides.Buy ? v : -v);
			}

			currentPnFCandle.CloseVolume = volume;
			currentPnFCandle.CloseTime = time;

			currentPnFCandle.VolumeProfile?.Update(price, volume, side);
		}

		private static PnFCandleMessage CreateCandle(MarketDataMessage message, PnFArg pnfArg, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, decimal price, decimal? volume, Sides? side, DateTimeOffset time)
		{
			var candle = new PnFCandleMessage
			{
				OpenPrice = openPrice,
				ClosePrice = closePrice,
				HighPrice = highPrice,
				LowPrice = lowPrice,
				OpenVolume = volume,
				//CloseVolume = volume,
				HighVolume = volume,
				LowVolume = volume,
				SecurityId = message.SecurityId,
				OpenTime = time,
				//CloseTime = time,
				HighTime = time,
				LowTime = time,
				PnFArg = pnfArg,
				State = CandleStates.Active,
			};

			if (message.IsCalcVolumeProfile)
				candle.VolumeProfile = new CandleMessageVolumeProfile();

			UpdateCandle(candle, price, volume, time, side);

			return candle;
		}
	}

	/// <summary>
	/// The builder of candles of <see cref="RenkoCandleMessage"/> type.
	/// </summary>
	public class RenkoCandleBuilder : CandleBuilder<RenkoCandleMessage>
	{
		/// <summary>
		/// The candle type.
		/// </summary>
		public override MarketDataTypes CandleType => MarketDataTypes.CandleRenko;

		/// <summary>
		/// Initializes a new instance of the <see cref="RenkoCandleBuilder"/>.
		/// </summary>
		public RenkoCandleBuilder()
		{
		}

		/// <inheritdoc />
		public override void Process(MarketDataMessage message, CandleMessage currentCandle, ICandleBuilderSourceValue value, IList<CandleMessage> changes)
		{
			var currentRenkoCandle = (RenkoCandleMessage)currentCandle;

			var price = value.Price;
			var volume = value.Volume;
			var time = value.Time;
			var side = value.OrderDirection;
			var boxSize = (Unit)message.Arg;

			var renkoStep = (decimal)(1 * boxSize);

			if (currentRenkoCandle == null)
			{
				var openPrice = MathHelper.Floor(price, renkoStep);

				changes.Add(CreateCandle(message, boxSize, openPrice, renkoStep, price, volume, side, time));
			}
			else
			{
				if (currentRenkoCandle.LowPrice <= price && price <= currentRenkoCandle.HighPrice)
				{
					currentRenkoCandle.TotalTicks++;

					if (volume != null)
					{
						currentRenkoCandle.TotalVolume += volume.Value;
						currentRenkoCandle.TotalPrice += volume.Value * price;

						currentRenkoCandle.RelativeVolume += side == Sides.Buy ? volume : -volume;
					}

					currentRenkoCandle.CloseVolume = volume;
					currentRenkoCandle.CloseTime = time;

					currentRenkoCandle.VolumeProfile?.Update(price, volume, side);

					changes.Add(currentRenkoCandle);
				}
				else
				{
					currentRenkoCandle.State = CandleStates.Finished;
					changes.Add(currentRenkoCandle);

					int times;
					bool isUp;
					decimal openPrice;

					if (price < currentRenkoCandle.LowPrice)
					{
						times = (int)((currentRenkoCandle.LowPrice - price) / renkoStep) + 1;
						isUp = false;
						openPrice = currentRenkoCandle.LowPrice;
					}
					else
					{
						times = (int)((price - currentRenkoCandle.HighPrice) / renkoStep) + 1;
						isUp = true;
						openPrice = currentRenkoCandle.HighPrice;
					}

					for (var i = 0; i < times; i++)
					{
						if (isUp)
						{
							currentRenkoCandle = CreateCandle(message, boxSize, openPrice, renkoStep, price, volume, side, time);
							changes.Add(currentRenkoCandle);
							openPrice += renkoStep;
						}
						else
						{
							currentRenkoCandle = CreateCandle(message, boxSize, openPrice, -renkoStep, price, volume, side, time);
							changes.Add(currentRenkoCandle);
							openPrice -= renkoStep;
						}

						currentRenkoCandle.State = CandleStates.Finished;
					}

					currentRenkoCandle.State = CandleStates.Active;
				}
			}
		}

		private static RenkoCandleMessage CreateCandle(MarketDataMessage message, Unit boxSize, decimal openPrice, decimal renkoStep, decimal price, decimal? volume, Sides? side, DateTimeOffset time)
		{
			var candle = new RenkoCandleMessage
			{
				OpenPrice = openPrice,
				ClosePrice = openPrice + renkoStep,
				//HighPrice = openPrice + renkoStep,
				//LowPrice = openPrice,
				OpenVolume = volume,
				CloseVolume = volume,
				HighVolume = volume,
				LowVolume = volume,
				SecurityId = message.SecurityId,
				OpenTime = time,
				CloseTime = time,
				HighTime = time,
				LowTime = time,
				BoxSize = boxSize,
				RelativeVolume = side == null ? null : (side == Sides.Buy ? volume : -volume),
				TotalTicks = 1,
				State = CandleStates.Active,
			};

			if (volume != null)
			{
				candle.TotalPrice += price * volume.Value;
				candle.TotalVolume += volume.Value;
			}

			if (renkoStep > 0)
			{
				candle.HighPrice = candle.ClosePrice;
				candle.LowPrice = candle.OpenPrice;
			}
			else
			{
				candle.HighPrice = candle.OpenPrice;
				candle.LowPrice = candle.ClosePrice;
			}

			if (message.IsCalcVolumeProfile)
			{
				candle.VolumeProfile = new CandleMessageVolumeProfile();
				candle.VolumeProfile.Update(price, volume, side);
			}

			return candle;
		}
	}
}