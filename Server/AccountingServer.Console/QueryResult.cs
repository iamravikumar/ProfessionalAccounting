﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using AccountingServer.Console.Chart;

namespace AccountingServer.Console
{
    public interface IQueryResult
    {
        bool AutoReturn { get; }
    }

    public interface IUpsertResult { }

    public class Suceed : IQueryResult
    {
        public override string ToString() { return "OK"; }
        public bool AutoReturn { get { return true; } }
    }

    public class Failed : IQueryResult, IUpsertResult
    {
        private readonly Exception m_Exception;
        public Failed(Exception exception) { m_Exception = exception; }
        public override string ToString() { return m_Exception.ToString(); }
        public bool AutoReturn { get { return true; } }
    }

    public class NumberAffected : IQueryResult, IUpsertResult
    {
        private readonly long m_N;
        public NumberAffected(long n) { m_N = n; }
        public override string ToString() { return m_N.ToString(CultureInfo.InvariantCulture); }
        public bool AutoReturn { get { return true; } }
    }

    public abstract class Text : IQueryResult
    {
        private readonly string m_Text;
        protected Text(string text) { m_Text = text; }
        public override string ToString() { return m_Text; }
        public abstract bool AutoReturn { get; }
    }

    public class EditableText : Text, IUpsertResult
    {
        public EditableText(string text) : base(text) { }
        public override bool AutoReturn { get { return false; } }
    }

    public class UnEditableText : Text
    {
        public UnEditableText(string text) : base(text) { }
        public override bool AutoReturn { get { return true; } }
    }

    public class ChartData : IQueryResult
    {
        public ChartData(AccountingChart chart)
        {
            ChartAreas = new List<ChartArea>();
            Series = new List<Series>();
            ChartAreas.Add(chart.Setup());
            foreach (var series in chart.GatherAsset())
                Series.Add(series);
        }

        public ChartData(IEnumerable<AccountingChart> accountingCharts)
        {
            ChartAreas = new List<ChartArea>();
            Series = new List<Series>();
            foreach (var chart in accountingCharts)
            {
                ChartAreas.Add(chart.Setup());
                foreach (var series in chart.GatherAsset())
                    Series.Add(series);
            }
        }

        public IList<Series> Series { get; private set; }
        public IList<ChartArea> ChartAreas { get; private set; }

        public bool AutoReturn { get { return true; } }
    }
}