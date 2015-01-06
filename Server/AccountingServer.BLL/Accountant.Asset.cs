﻿using System;
using System.Collections.Generic;
using System.Linq;
using AccountingServer.Entities;

namespace AccountingServer.BLL
{
    public partial class Accountant
    {
        /// <summary>
        ///     获取指定月的最后一天
        /// </summary>
        /// <param name="year">年</param>
        /// <param name="month">月</param>
        /// <returns>此月最后一天</returns>
        private static DateTime LastDayOfMonth(int year, int month)
        {
            while (month > 12)
            {
                month -= 12;
                year++;
            }
            while (month < 1)
            {
                month += 12;
                year--;
            }
            return new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        }

        /// <summary>
        ///     调整资产计算表
        /// </summary>
        /// <param name="asset">资产</param>
        private static void InternalRegular(Asset asset)
        {
            if (asset.Remark == Asset.IgnoranceMark)
                return;
            if (!asset.Date.HasValue ||
                !asset.Value.HasValue)
                return;

            var lst = asset.Schedule == null ? new List<AssetItem>() : asset.Schedule.ToList();
            foreach (var assetItem in lst)
                if (assetItem.Date.HasValue)
                    assetItem.Date = LastDayOfMonth(assetItem.Date.Value.Year, assetItem.Date.Value.Month);

            lst.Sort(new AssetItemComparer());

            if (lst.Count == 0 ||
                !(lst[0] is AcquisationItem))
                lst.Insert(
                           0,
                           new AcquisationItem
                               {
                                   Date = asset.Date,
                                   OrigValue = asset.Value.Value
                               });
            else if (lst[0].Remark != AssetItem.IgnoranceMark)
            {
                (lst[0] as AcquisationItem).Date = asset.Date;
                (lst[0] as AcquisationItem).OrigValue = asset.Value.Value;
            }

            var bookValue = 0D;
            for (var i = 0; i < lst.Count; i++)
            {
                var item = lst[i];
                if (item is AcquisationItem)
                {
                    bookValue += (item as AcquisationItem).OrigValue;
                    item.BookValue = bookValue;
                }
                else if (item is DepreciateItem)
                {
                    bookValue -= (item as DepreciateItem).Amount;
                    item.BookValue = bookValue;
                }
                else if (item is DevalueItem)
                {
                    if (bookValue <= (item as DevalueItem).FairValue
                        &&
                        item.Remark != AssetItem.IgnoranceMark)
                    {
                        lst.RemoveAt(i--);
                        continue;
                    }
                    bookValue = (item as DevalueItem).FairValue;
                    item.BookValue = (item as DevalueItem).FairValue;
                }
                else if (item is DispositionItem)
                    if (item.Remark != AssetItem.IgnoranceMark)
                    {
                        (item as DispositionItem).NetValue = bookValue;
                        bookValue = 0;
                    }
                    else
                        bookValue -= (item as DispositionItem).NetValue;
            }

            asset.Schedule = lst.ToArray();
        }

        /// <summary>
        ///     根据账面调整资产计算表
        /// </summary>
        /// <param name="asset">资产</param>
        /// <returns>资产计算表项目</returns>
        private IEnumerable<AssetItem> ExternalRegular(Asset asset)
        {
            if (asset.Remark == Asset.IgnoranceMark)
                yield break;

            var filter1 = new VoucherDetail
                              {
                                  Title = asset.Title,
                                  Content = asset.ID.ToString()
                              };
            //var filter2 = new VoucherDetail
            //                        {
            //                            Title = asset.DepreciationTitle,
            //                            Content = asset.ID.ToString()
            //                        };
            //var filter3 = new VoucherDetail
            //                        {
            //                            Title = asset.DevaluationTitle,
            //                            Content = asset.ID.ToString()
            //                        };
            var vouchers1 = m_Db.SelectVouchersWithDetail(filter1);
            //var vouchers2 = m_Db.SelectVouchersWithDetail(filter2);
            //var vouchers3 = m_Db.SelectVouchersWithDetail(filter3);

            foreach (var voucher in vouchers1)
            {
                var value = voucher.Details.Single(d => d.IsMatch(filter1)).Fund.Value;
                if (value > 0)
                    yield return
                        new AcquisationItem
                            {
                                VoucherID = voucher.ID,
                                Date = voucher.Date,
                                OrigValue = value
                            };
                else
                    yield return
                        new DispositionItem
                            {
                                VoucherID = voucher.ID,
                                Date = voucher.Date
                            };
            }
        }

        /// <summary>
        ///     根据资产计算表更新账面
        /// </summary>
        /// <param name="asset">资产</param>
        /// <param name="startDate">开始日期（若<paramref name="isCollapsed" />为<c>true</c>则表示压缩截止日期，在账上反映为无日期）</param>
        /// <param name="endDate">截止日期</param>
        /// <param name="isCollapsed">是否压缩</param>
        public void Update(Asset asset, DateTime? startDate, DateTime? endDate, bool isCollapsed = false)
        {
            if (asset.Schedule == null)
                return;

            var bookValue = 0D;
            foreach (var item in asset.Schedule)
            {
                if (item.Date.HasValue)
                {
                    if (BalanceComparer.CompareDate(startDate, item.Date.Value) < 0 &&
                        (!endDate.HasValue || endDate.Value >= item.Date.Value))
                        UpdateItem(asset, item, bookValue);
                    else if (isCollapsed && (!endDate.HasValue || endDate.Value >= item.Date.Value))
                        UpdateItem(asset, item, bookValue, true);
                }
                else if (!startDate.HasValue)
                    UpdateItem(asset, item, bookValue);
                else if (isCollapsed)
                    UpdateItem(asset, item, bookValue, true);

                bookValue = item.BookValue;
            }
        }

        private bool UpdateItem(Asset asset, AssetItem item, double bookValue, bool isCollapsed = false)
        {
            if (item.VoucherID == null)
            {
                if (item is AcquisationItem)
                {
                    var voucher = new Voucher
                                      {
                                          Date = isCollapsed ? null : item.Date,
                                          Type = VoucherType.Ordinal,
                                          Remark = "automatically generated",
                                          Details = new[]
                                                        {
                                                            new VoucherDetail
                                                                {
                                                                    Title = asset.Title,
                                                                    Content = asset.ID.ToString().ToUpperInvariant(),
                                                                    Fund = (item as AcquisationItem).OrigValue
                                                                }
                                                        }
                                      };
                    var res = m_Db.InsertVoucher(voucher);
                    item.VoucherID = voucher.ID;
                    return res;
                }
                if (item is DepreciateItem)
                {
                    var voucher = new Voucher
                                      {
                                          Date = isCollapsed ? null : item.Date,
                                          Type = VoucherType.Depreciation,
                                          Remark = "automatically generated",
                                          Details = new[]
                                                        {
                                                            new VoucherDetail
                                                                {
                                                                    Title = asset.DepreciationTitle,
                                                                    Content = asset.ID.ToString().ToUpperInvariant(),
                                                                    Fund = -(item as DepreciateItem).Amount
                                                                },
                                                            new VoucherDetail
                                                                {
                                                                    Title = asset.ExpenseTitle,
                                                                    SubTitle = asset.ExpenseSubTitle,
                                                                    Content = asset.ID.ToString().ToUpperInvariant(),
                                                                    Fund = (item as DepreciateItem).Amount
                                                                }
                                                        }
                                      };
                    var res = m_Db.InsertVoucher(voucher);
                    item.VoucherID = voucher.ID;
                    return res;
                }
                if (item is DevalueItem)
                {
                    var fund = bookValue - (item as DevalueItem).FairValue;
                    var voucher = new Voucher
                                      {
                                          Date = isCollapsed ? null : item.Date,
                                          Type = VoucherType.Devalue,
                                          Remark = "automatically generated",
                                          Details = new[]
                                                        {
                                                            new VoucherDetail
                                                                {
                                                                    Title = asset.DevaluationTitle,
                                                                    Content = asset.ID.ToString().ToUpperInvariant(),
                                                                    Fund = -fund
                                                                },
                                                            new VoucherDetail
                                                                {
                                                                    Title = asset.ExpenseTitle,
                                                                    // TODO : fork expense title
                                                                    SubTitle = asset.ExpenseSubTitle,
                                                                    Content = asset.ID.ToString().ToUpperInvariant(),
                                                                    Fund = fund
                                                                }
                                                        }
                                      };
                    var res = m_Db.InsertVoucher(voucher);
                    item.VoucherID = voucher.ID;
                    return res;
                }
                return false;
            }

            {
                var voucher = m_Db.SelectVoucher(item.VoucherID);
                if (voucher.Date != (isCollapsed ? null : item.Date))
                    return false;

                if (item is AcquisationItem)
                {
                    voucher.Type = VoucherType.Ordinal;
                    {
                        var ds = voucher.Details.Where(
                                                       d => d.IsMatch(
                                                                      new VoucherDetail
                                                                          {
                                                                              Title = asset.Title,
                                                                              Content =
                                                                                  asset.ID.ToString().ToUpperInvariant()
                                                                          })).ToList();
                        if (ds.Count == 0)
                        {
                            var l = voucher.Details.ToList();
                            l.Add(
                                  new VoucherDetail
                                      {
                                          Title = asset.Title,
                                          Content = asset.ID.ToString().ToUpperInvariant(),
                                          Fund = (item as AcquisationItem).OrigValue
                                      });
                            voucher.Details = l.ToArray();
                        }
                        else if (ds.Count > 1)
                            return false;

                        ds[0].Fund = (item as AcquisationItem).OrigValue;
                    }

                    return m_Db.UpdateVoucher(voucher);
                }
                if (item is DepreciateItem)
                {
                    voucher.Type = VoucherType.Depreciation;
                    {
                        var ds = voucher.Details.Where(
                                                       d => d.IsMatch(
                                                                      new VoucherDetail
                                                                          {
                                                                              Title = asset.DepreciationTitle,
                                                                              Content =
                                                                                  asset.ID.ToString().ToUpperInvariant()
                                                                          })).ToList();
                        if (ds.Count == 0)
                        {
                            var l = voucher.Details.ToList();
                            l.Add(
                                  new VoucherDetail
                                      {
                                          Title = asset.DepreciationTitle,
                                          Content = asset.ID.ToString().ToUpperInvariant(),
                                          Fund = (item as DepreciateItem).Amount
                                      });
                            voucher.Details = l.ToArray();
                        }
                        else if (ds.Count > 1)
                            return false;

                        ds[0].Fund = -(item as DepreciateItem).Amount;
                    }
                    {
                        var ds = voucher.Details.Where(
                                                       d => d.IsMatch(
                                                                      new VoucherDetail
                                                                          {
                                                                              Title = asset.ExpenseTitle,
                                                                              SubTitle = asset.ExpenseSubTitle,
                                                                              Content =
                                                                                  asset.ID.ToString().ToUpperInvariant()
                                                                          })).ToList();
                        if (ds.Count == 0)
                        {
                            var l = voucher.Details.ToList();
                            l.Add(
                                  new VoucherDetail
                                      {
                                          Title = asset.ExpenseTitle,
                                          SubTitle = asset.ExpenseSubTitle,
                                          Content = asset.ID.ToString().ToUpperInvariant(),
                                          Fund = (item as DepreciateItem).Amount
                                      });
                            voucher.Details = l.ToArray();
                        }
                        else if (ds.Count > 1)
                            return false;

                        ds[0].Fund = (item as DepreciateItem).Amount;
                    }

                    return m_Db.UpdateVoucher(voucher);
                }
                if (item is DevalueItem)
                {
                    var fund = bookValue - (item as DevalueItem).FairValue;
                    voucher.Type = VoucherType.Devalue;
                    {
                        var ds = voucher.Details.Where(
                                                       d => d.IsMatch(
                                                                      new VoucherDetail
                                                                          {
                                                                              Title = asset.DevaluationTitle,
                                                                              Content =
                                                                                  asset.ID.ToString().ToUpperInvariant()
                                                                          })).ToList();
                        if (ds.Count == 0)
                        {
                            var l = voucher.Details.ToList();
                            l.Add(
                                  new VoucherDetail
                                      {
                                          Title = asset.DevaluationTitle,
                                          Content = asset.ID.ToString().ToUpperInvariant(),
                                          Fund = -fund
                                      });
                            voucher.Details = l.ToArray();
                        }
                        else if (ds.Count > 1)
                            return false;

                        ds[0].Fund = -fund;
                    }
                    {
                        var ds = voucher.Details.Where(
                                                       d => d.IsMatch(
                                                                      new VoucherDetail
                                                                          {
                                                                              // TODO : fork expense title
                                                                              Title = asset.ExpenseTitle,
                                                                              SubTitle = asset.ExpenseSubTitle,
                                                                              Content =
                                                                                  asset.ID.ToString().ToUpperInvariant()
                                                                          })).ToList();
                        if (ds.Count == 0)
                        {
                            var l = voucher.Details.ToList();
                            l.Add(
                                  new VoucherDetail
                                      {
                                          Title = asset.ExpenseTitle,
                                          SubTitle = asset.ExpenseSubTitle,
                                          Content = asset.ID.ToString().ToUpperInvariant(),
                                          Fund = fund
                                      });
                            voucher.Details = l.ToArray();
                        }
                        else if (ds.Count > 1)
                            return false;

                        ds[0].Fund = fund;
                    }

                    return m_Db.UpdateVoucher(voucher);
                }
                return false;
            }
        }

        /// <summary>
        ///     折旧
        /// </summary>
        public static void Depreciate(Asset asset)
        {
            if (!asset.Date.HasValue ||
                !asset.Value.HasValue ||
                !asset.Salvge.HasValue ||
                !asset.Life.HasValue)
                return;

            var items = asset.Schedule.ToList();
            items.RemoveAll(a => a is DepreciateItem && a.Remark != AssetItem.IgnoranceMark);

            switch (asset.Method)
            {
                case DepreciationMethod.None:
                    break;
                case DepreciationMethod.StraightLine:
                    {
                        var lastYear = asset.Date.Value.Year + asset.Life.Value;
                        var lastMonth = asset.Date.Value.Month;

                        var dt = asset.Date.Value;
                        var flag = false;
                        for (var i = 0;; i++)
                        {
                            if (i < items.Count)
                            {
                                if (items[i].Date > dt)
                                {
                                    dt = items[i].Date ?? dt;
                                    flag = false;
                                    continue;
                                }
                                if (flag)
                                    continue;
                                if (items[i] is AcquisationItem ||
                                    items[i] is DispositionItem)
                                    continue;
                                if (items[i] is DepreciateItem) // With IgnoranceMark
                                {
                                    flag = true;
                                    continue;
                                }
                            }

                            flag = true;

                            if (dt.Year == asset.Date.Value.Year
                                &&
                                dt.Month == asset.Date.Value.Month)
                            {
                                dt = LastDayOfMonth(dt.Year, dt.Month + 1);
                                if (i == items.Count)
                                    i--;
                                continue;
                            }

                            var amount = items[i - 1].BookValue - asset.Salvge.Value;
                            var monthes = 12 * (lastYear - dt.Year) + lastMonth - dt.Month;

                            if (amount <= Tolerance ||
                                monthes < 0) // Ended, Over-depreciated or Dispositoned
                            {
                                if (i < items.Count)
                                    continue; // If another AcquisationItem exists
                                break;
                            }

                            items.Insert(
                                         i,
                                         new DepreciateItem
                                             {
                                                 Date = dt,
                                                 Amount = amount / (monthes + 1),
                                                 BookValue = items[i - 1].BookValue - amount / (monthes + 1)
                                             });

                            dt = LastDayOfMonth(dt.Year, dt.Month + 1);
                        }
                    }
                    //if (mo < 12)
                    //    for (var mon = mo + 1; mon <= 12; mon++)
                    //        items.Add(
                    //                  new DepreciateItem
                    //                      {
                    //                          Date = LastDayOfMonth(yr, mon),
                    //                          Amount = amount / n / 12
                    //                      });
                    //for (var year = 1; year < n; year++)
                    //    for (var mon = 1; mon <= 12; mon++)
                    //        items.Add(
                    //                  new DepreciateItem
                    //                      {
                    //                          Date = LastDayOfMonth(yr + year, mon),
                    //                          Amount = amount / n / 12
                    //                      });
                    //// if (mo > 0)
                    //{
                    //    for (var mon = 1; mon <= mo; mon++)
                    //        items.Add(
                    //                  new DepreciateItem
                    //                      {
                    //                          Date = LastDayOfMonth(yr + n, mon),
                    //                          Amount = amount / n / 12
                    //                      });
                    //}
                    break;
                case DepreciationMethod.SumOfTheYear:
                    if (items.Any(a => a is DevalueItem || a.Remark == AssetItem.IgnoranceMark) ||
                        items.Count(a => a is AcquisationItem) != 1)
                        throw new NotImplementedException();
                    {
                        var n = asset.Life.Value;
                        var mo = asset.Date.Value.Month;
                        var yr = asset.Date.Value.Year;
                        var amount = asset.Value.Value - asset.Salvge.Value;
                        var z = n * (n + 1) / 2;
                        var nstar = n - mo / 12D;
                        var zstar = (Math.Floor(nstar) + 1) * (Math.Floor(nstar) + 2 * (nstar - Math.Floor(nstar))) / 2;
                        if (mo < 12)
                        {
                            var a = amount * n / z * (12 - mo) / z;
                            amount -= a;
                            for (var mon = mo + 1; mon <= 12; mon++)
                                items.Add(
                                          new DepreciateItem
                                              {
                                                  Date = LastDayOfMonth(yr, mon),
                                                  Amount = a / (12 - mo)
                                              });
                        }
                        for (var year = 1; year < n; year++)
                            for (var mon = 1; mon <= 12; mon++)
                                items.Add(
                                          new DepreciateItem
                                              {
                                                  Date = LastDayOfMonth(yr + year, mon),
                                                  Amount = amount * (nstar - year + 1) / zstar / 12
                                              });
                        // if (mo > 0)
                        {
                            for (var mon = 1; mon <= mo; mon++)
                                items.Add(
                                          new DepreciateItem
                                              {
                                                  Date = LastDayOfMonth(yr + n, mon),
                                                  Amount = amount * (nstar - (n + 1) + 2) / zstar / 12
                                              });
                        }
                    }
                    break;
                case DepreciationMethod.DoubleDeclineMethod:
                    throw new NotImplementedException();
            }

            asset.Schedule = items.ToArray();
        }
    }
}