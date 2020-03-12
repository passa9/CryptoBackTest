using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HitBTC.Net.Models;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using MahApps.Metro.Controls;

namespace BackTest
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private List<HitCandle> candles;

        public SeriesCollection SeriesCollection { get; set; } = null;

        public string[] Labels { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            this.chart.DataTooltip = null;
            DataContext = this;
        }

        private HitPeriod GetPeriod(string period)
        {
            switch (period)
            {
                case "1m":
                    {
                        return HitPeriod.Minute1;
                    }
                case "3m":
                    {
                        return HitPeriod.Minute3;
                    }
                case "5m":
                    {
                        return HitPeriod.Minute5;
                    }
                case "15m":
                    {
                        return HitPeriod.Minute15;
                    }
                case "30m":
                    {
                        return HitPeriod.Minute30;
                    }
                case "1H":
                    {
                        return HitPeriod.Hour1;
                    }
                case "4H":
                    {
                        return HitPeriod.Hour4;
                    }
                case "1D":
                    {
                        return HitPeriod.Day1;
                    }
                case "7D":
                    {
                        return HitPeriod.Day7;
                    }
                case "1M":
                    {
                        return HitPeriod.Month1;
                    }
                default:
                    {
                        throw new ArgumentException("Valore periodo candela non valido");
                    }
            }
        }

        private async void BtnLoadCandles_OnClick(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;

            SeriesCollection = null;
            chart.Series = SeriesCollection;
            try
            {
            var hitbtcHitRestApi = new HitBTC.Net.HitRestApi();
            List<HitCandle> candles = new List<HitCandle>();

            var symbol = (string)cmbSymbol.SelectionBoxItem;
            var nCandles = (int)txbNCandle.Value;
            var period = GetPeriod((string)cmbCandle.SelectionBoxItem);

            if (nCandles > 1000)
            {
                int offset = 0;
                int maxCandles = 1000;
                while (nCandles >= 1000)
                {
                    var task = await hitbtcHitRestApi.GetCandlesAsync(symbol, period, maxCandles, offset);
                    var candlesBuffer= task.Result.ToList();
                    candles.AddRange(candlesBuffer);
                    nCandles = nCandles - maxCandles;
                    offset = offset + maxCandles;
                    System.Threading.Thread.Sleep(100);
                }
            }
            else
            {
                var task = await hitbtcHitRestApi.GetCandlesAsync(symbol, period, nCandles, 1);
                candles = task.Result.ToList();
            }

            this.candles = candles.OrderBy(x => x.Timestamp).ToList();

                btnLoadChart.IsEnabled = true;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                MessageBox.Show(e.ToString());
            }

            Cursor = Cursors.Arrow;
        }

        private void FillChart()
        {

            var chartValues = new ChartValues<OhlcPoint>();
            var timestamps = new List<string>();
            foreach (var candle in this.candles)
            {
                var ohlc = new OhlcPoint((double)candle.Open, (double)candle.Max, (double)candle.Min, (double)candle.Close);
                chartValues.Add(ohlc);

                timestamps.Add(candle.Timestamp.ToString("dd MMM"));
            }

            SeriesCollection = new SeriesCollection
            {
                new OhlcSeries()
                {
                    Title = "BTCUSD",
                    Values =chartValues
                }
            };

            chart.Series = SeriesCollection;
            
            Labels = timestamps.ToArray();
            chartAxisX.Labels = null;
           chartAxisX.Labels = Labels;

            //chartAxisY.Separator = new Separator()
            //{
            //    IsEnabled =  true,
            //    Step = 10
            //};
        }

        private void BtnStartBackTest_OnClick(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            try
            {
                decimal startAmountUSDT = (decimal)txbStartAmount.Value;
                int minPerdiod = (int)txbMinPerdiod.Value;
                int maxPerdiod = (int)txbMaxPerdiod.Value;
                decimal minEma = (decimal)txbMinEMA.Value;
                decimal maxEma = (decimal)txbMaxEMA.Value;

                BackTest(startAmountUSDT, minEma, maxEma, minPerdiod, maxPerdiod);

                expResult.IsExpanded = true;
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }

            Cursor = Cursors.Arrow;
        }

        private  void BackTest(decimal startAmountUSDT, decimal minEMA, decimal maxEMA, int minPeriod, int maxPeriod)
        {
            var scatterPlotsBuy = new ChartValues<ScatterPoint>();
            var scatterPlotsSell = new ChartValues<ScatterPoint>();

            Status status = Status.USDT;

            var firstBTCPrice = candles[maxPeriod].Open;
            var lastBTCPrice = candles.Last().Open;
            var startAmountInBTC = startAmountUSDT / firstBTCPrice;
            int nTrades = 0;

            IList<decimal> minEMAS = new List<decimal>();
            IList<decimal> maxEMAS = new List<decimal>();

            Console.WriteLine("Min EMA: " + minEMA + " Max EMA: " + maxEMA);
            Console.WriteLine("Start amount USDT: " + Math.Round(startAmountUSDT, 2) + " BTC: " + Math.Round(startAmountInBTC) + " BTC PRICE: " + Math.Round(firstBTCPrice, 2));
            Console.WriteLine("Start backtesting");
            Console.WriteLine("...");

            decimal currentUSDT = startAmountUSDT;
            decimal currentBTC = 0;

            for (int i = maxPeriod; i < candles.Count; i++)
            {
                var candle = candles[i];

                var avgMinEma = candles.Skip(i +1 - minPeriod).Take(minPeriod).Select(x => x.Open).Sum() / minPeriod;
                var avgMaxEma = candles.Skip(i +1 - maxPeriod).Take(maxPeriod).Select(x => x.Open).Sum() / maxPeriod;

                minEMAS.Add(avgMinEma);
                maxEMAS.Add(avgMaxEma);

                var ema = avgMinEma / avgMaxEma;

                if (ema > maxEMA && status == Status.USDT)
                {
                    status = Status.BTC;
                    currentBTC = currentUSDT / candle.Open;
                    currentUSDT = 0;
                    nTrades++;

                    var scatterPlots = new ScatterPoint(i, (double)candle.Open);
                    scatterPlotsBuy.Add(scatterPlots);
                }
                else if (ema < minEMA && status == Status.BTC)
                {
                    status = Status.USDT;
                    currentUSDT = currentBTC * candle.Open;
                    currentBTC = 0;
                    nTrades++;

                    var scatterPlots = new ScatterPoint(i, (double)candle.Open);
                    scatterPlotsSell.Add(scatterPlots);
                }
            }
            Console.WriteLine("End");
            Console.WriteLine("Total trades: " + nTrades);
            if (status == Status.USDT)
            {
                var endAmountInBTC = currentUSDT / lastBTCPrice;
                Console.WriteLine("End amount USDT: " + Math.Round(currentUSDT, 2) + " - BTC: " + Math.Round(endAmountInBTC, 2) + " - BTC PRICE: " + Math.Round(lastBTCPrice, 2));
                var profitIncrement = Math.Round(((currentUSDT - startAmountUSDT) / startAmountUSDT * 100), 2);
                var btcIncrement = Math.Round(((lastBTCPrice - firstBTCPrice) / firstBTCPrice * 100), 2);
                Console.WriteLine("Profit Increment: " + profitIncrement + "% - BTC Increment: " + btcIncrement + "%");
                Console.WriteLine("Real profit: " + (profitIncrement - btcIncrement) + "%");

                txbNTrades.Text = nTrades.ToString();
                txbStartUSDTAmount.Text = startAmountUSDT.ToString();
                txbEndUSDTAmount.Text = Math.Round(currentUSDT, 2).ToString();
                txbIncrementUSDT.Text = Math.Round(currentUSDT - startAmountUSDT, 2).ToString();
                txbIncrementUSDTPerc.Text = Math.Round(((currentUSDT - startAmountUSDT) / startAmountUSDT * 100), 2).ToString();

                txbStartBTCPrice.Text = Math.Round(firstBTCPrice,2).ToString();
                txbEndBTCPrice.Text = Math.Round(lastBTCPrice, 2).ToString();
                txbIncrementBTC.Text = Math.Round(lastBTCPrice - firstBTCPrice, 2).ToString();
                txbIncrementBTCPerc.Text = Math.Round(((lastBTCPrice - firstBTCPrice) / firstBTCPrice * 100), 2).ToString();

                var estimatedValue = (startAmountUSDT / firstBTCPrice) * lastBTCPrice;
                var profit = currentUSDT - estimatedValue;
                txbProfitUSDT.Text = Math.Round(profit,2).ToString();
                txbProfitUSDTPerc.Text = Math.Round(((currentUSDT - estimatedValue) / estimatedValue * 100), 2).ToString();

            }
            else
            {
                var endAmountInUSDT = currentBTC * lastBTCPrice;
                Console.WriteLine("End amount USDT: " + Math.Round(endAmountInUSDT, 2) + " - BTC: " + Math.Round(currentBTC, 2) + " - BTC PRICE: " + Math.Round(lastBTCPrice, 2));
                var profitIncrement = Math.Round(((endAmountInUSDT - startAmountUSDT) / startAmountUSDT * 100), 2);
                var btcIncrement = Math.Round(((lastBTCPrice - firstBTCPrice) / firstBTCPrice * 100), 2);
                Console.WriteLine("Profit Increment: " + profitIncrement + "% - BTC Increment: " + btcIncrement + "%");
                Console.WriteLine("Real profit: " + (profitIncrement - btcIncrement) + "%");


                txbNTrades.Text = nTrades.ToString();
                txbStartUSDTAmount.Text = startAmountUSDT.ToString();
                txbEndUSDTAmount.Text = Math.Round(endAmountInUSDT, 2).ToString();
                txbIncrementUSDT.Text = Math.Round(endAmountInUSDT - startAmountUSDT, 2).ToString();
                txbIncrementUSDTPerc.Text = Math.Round(((endAmountInUSDT - startAmountUSDT) / startAmountUSDT * 100), 2).ToString();

                txbStartBTCPrice.Text = Math.Round(firstBTCPrice, 2).ToString();
                txbEndBTCPrice.Text = Math.Round(lastBTCPrice, 2).ToString();
                txbIncrementBTC.Text = Math.Round(lastBTCPrice - firstBTCPrice, 2).ToString();
                txbIncrementBTCPerc.Text = Math.Round(((lastBTCPrice - firstBTCPrice) / firstBTCPrice * 100), 2).ToString();

                var estimatedValue = (startAmountUSDT / firstBTCPrice) * lastBTCPrice;
                var profit = endAmountInUSDT - estimatedValue;
                txbProfitUSDT.Text = Math.Round(profit, 2).ToString();
                txbProfitUSDTPerc.Text = Math.Round(((endAmountInUSDT - estimatedValue) / estimatedValue * 100), 2).ToString();
            }

            txbIncrementUSDT.Foreground = GetColorByValue(txbIncrementUSDT.Text);
            txbIncrementUSDTPerc.Foreground = GetColorByValue(txbIncrementUSDTPerc.Text);
            txbIncrementBTC.Foreground = GetColorByValue(txbIncrementBTC.Text);
            txbIncrementBTCPerc.Foreground = GetColorByValue(txbIncrementBTCPerc.Text);
            txbProfitUSDT.Foreground = GetColorByValue(txbProfitUSDT.Text);
            txbProfitUSDTPerc.Foreground = GetColorByValue(txbProfitUSDTPerc.Text);

            if (SeriesCollection != null)
            {
                DrawBackTestChart(minEMAS,maxEMAS,scatterPlotsBuy, scatterPlotsSell,minPeriod,maxPeriod);
            }
        }

        private SolidColorBrush GetColorByValue(string value) {
            if (decimal.Parse(value) < 0)
            {
                return Brushes.Red;
            }
            else if (decimal.Parse(value) > 0)
            {
                return Brushes.Green;
            }
            else {
                return Brushes.Black;
            }
        }

        private void DrawBackTestChart(IList<decimal> minEmas, IList<decimal> maxEmas, ChartValues<ScatterPoint> scatterPlotBuy, ChartValues<ScatterPoint> scatterPlotSell,int minPeriod, int maxPeriod)
        {
          var seriesToRemove =  SeriesCollection.Where(x => x.GetType() == typeof(ScatterSeries) || x.GetType() == typeof(LineSeries));

            foreach (var seriesView in seriesToRemove)
            {
                SeriesCollection.Remove(seriesView);
            }


            var minPeriodoChartValue = new ChartValues<double>();
            var maxPeriodoChartValue = new ChartValues<double>();

            for (int i = 0; i < maxPeriod; i++)
            {
                minPeriodoChartValue.Add(double.NaN);
                maxPeriodoChartValue.Add(double.NaN);
            }

            for (int i = 0; i < minEmas.Count; i++)
            {
                minPeriodoChartValue.Add((double)minEmas[i]);
                maxPeriodoChartValue.Add((double)maxEmas[i]);
            }

            var minEmaSeries = new LineSeries
            {
                Values = minPeriodoChartValue,
                Fill = Brushes.Transparent,
                Title = minPeriod.ToString() + " Periods",
                PointGeometry = null,
                StrokeThickness = 1.1
            };

            var maxEmaSeries = new LineSeries
            {
                Values = maxPeriodoChartValue,
                Fill = Brushes.Transparent,
                Title = maxPeriod.ToString() + " Periods",
                PointGeometry = null,
                StrokeThickness = 1.1
            };

            SeriesCollection.Add(minEmaSeries);
            SeriesCollection.Add(maxEmaSeries);

            var scatterPlotSeriesBuy = new ScatterSeries()
            {
                Values = scatterPlotBuy,
                Stroke = Brushes.Green,
                Fill = Brushes.Green,
                Foreground = Brushes.Green,
            };
            var scatterPlotSeriesSell = new ScatterSeries()
            {
                Values = scatterPlotSell,
                Stroke = Brushes.Red,
                Fill = Brushes.Red,
                Foreground = Brushes.Red,
            };

            SeriesCollection.Add(scatterPlotSeriesBuy);
            SeriesCollection.Add(scatterPlotSeriesSell);

            chart.Series = null;
            chart.Series = SeriesCollection;
        }

        private void BtnLoadChart_OnClick(object sender, RoutedEventArgs e)
        {
            FillChart();
        }
    }
}
