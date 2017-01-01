﻿using System;
using System.Collections.Generic;

namespace AccountingServer.Entities
{
    /// <summary>
    ///     日期过滤器
    /// </summary>
    public struct DateFilter : IDateRange
    {
        /// <summary>
        ///     是否只允许无日期（若为<c>true</c>，则无须考虑<c>Nullable</c>）
        /// </summary>
        public bool NullOnly;

        /// <summary>
        ///     是否允许无日期
        /// </summary>
        public bool Nullable;

        /// <summary>
        ///     开始日期（含）
        /// </summary>
        public DateTime? StartDate;

        /// <summary>
        ///     截止日期（含）
        /// </summary>
        public DateTime? EndDate;

        /// <summary>
        ///     任意日期
        /// </summary>
        public static DateFilter Unconstrained = new DateFilter
                                                     {
                                                         NullOnly = false,
                                                         Nullable = true,
                                                         StartDate = null,
                                                         EndDate = null
                                                     };

        /// <summary>
        ///     仅限无日期
        /// </summary>
        public static DateFilter TheNullOnly = new DateFilter
                                                   {
                                                       NullOnly = true,
                                                       Nullable = true,
                                                       StartDate = null,
                                                       EndDate = null
                                                   };

        public DateFilter(DateTime? startDate, DateTime? endDate)
        {
            NullOnly = false;
            Nullable = !startDate.HasValue;
            StartDate = startDate;
            EndDate = endDate;
        }

        /// <inheritdoc />
        public DateFilter Range => this;
    }

    /// <summary>
    ///     日期比较器
    /// </summary>
    public class DateComparer : IComparer<DateTime?>
    {
        public int Compare(DateTime? x, DateTime? y) => DateHelper.CompareDate(x, y);
    }

    /// <summary>
    ///     日期辅助类
    /// </summary>
    public static class DateHelper
    {
        /// <summary>
        ///     比较两日期（可以为无日期）的先后
        /// </summary>
        /// <param name="b1Date">第一个日期</param>
        /// <param name="b2Date">第二个日期</param>
        /// <returns>相等为0，第一个先为-1，第二个先为1（无日期按无穷长时间以前考虑）</returns>
        public static int CompareDate(DateTime? b1Date, DateTime? b2Date)
        {
            if (b1Date.HasValue &&
                b2Date.HasValue)
                return b1Date.Value.CompareTo(b2Date.Value);
            if (b1Date.HasValue)
                return 1;
            if (b2Date.HasValue)
                return -1;
            return 0;
        }

        /// <summary>
        ///     判断日期是否符合日期过滤器
        /// </summary>
        /// <param name="dt">日期</param>
        /// <param name="rng">日期过滤器</param>
        /// <returns>是否符合</returns>
        public static bool Within(this DateTime? dt, DateFilter rng)
        {
            if (rng.NullOnly)
                return dt == null;

            if (!dt.HasValue)
                return rng.Nullable;

            if (rng.StartDate.HasValue)
                if (dt < rng.StartDate.Value)
                    return false;

            if (rng.EndDate.HasValue)
                if (dt > rng.EndDate.Value)
                    return false;

            return true;
        }
    }
}