﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AccountingServer.BLL;
using AccountingServer.Console;
using AccountingServer.Console.Plugin;
using AccountingServer.Entities;

namespace AccountingServer.Plugins.THUInfo
{
    /// <summary>
    ///     从info.tsinghua.edu.cn更新账户
    /// </summary>
    [Plugin(Alias = "f")]
    public partial class THUInfo : PluginBase
    {
        /// <summary>
        ///     对账忽略标志
        /// </summary>
        private const string IgnoranceMark = "reconciliation";

        public THUInfo(Accountant accountant) : base(accountant) { }

        /// <summary>
        ///     执行插件表达式
        ///     参数的个数和内容决定行为。
        ///     <list type="bullet">
        ///         <item>
        ///             <description>若第一个参数为<c>String.Empty</c>且参数个数为3，则以第二个参数为用户名，第三个参数为密码更新消费记录；</description>
        ///         </item>
        ///         <item>
        ///             <description>若无参数，则只进行对账；</description>
        ///         </item>
        ///         <item>
        ///             <description>若第一个参数非空，则每个参数表示一天的消费情况。</description>
        ///         </item>
        ///     </list>
        /// </summary>
        /// <param name="pars">参数列表</param>
        /// <returns>执行结果</returns>
        public override IQueryResult Execute(params string[] pars)
        {
            if (pars.Length >= 3)
                if (String.IsNullOrEmpty(pars[0]))
                    FetchData(pars[1], pars[2]);
            lock (m_Lock)
            {
                if (m_Data == null)
                    throw new NullReferenceException();
                List<Problem> noRemark, tooMuch, tooFew;
                List<VDetail> noRecord;
                Compare(out noRemark, out tooMuch, out tooFew, out noRecord);
                if (pars.Length == 0 ||
                    String.IsNullOrEmpty(pars[0]) ||
                    noRemark.Any() ||
                    tooMuch.Any() ||
                    tooFew.Any(p => p.Details.Any()) ||
                    noRecord.Any())
                {
                    var sb = new StringBuilder();
                    foreach (var problem in noRemark)
                    {
                        sb.AppendLine("---No Remark");
                        foreach (var r in problem.Records)
                            sb.AppendLine(r.ToString());
                        foreach (var v in problem.Details.Select(d => d.Voucher).Distinct())
                            sb.AppendLine(CSharpHelper.PresentVoucher(v));
                        sb.AppendLine();
                    }
                    foreach (var problem in tooMuch)
                    {
                        sb.AppendLine("---Too Much Voucher");
                        foreach (var r in problem.Records)
                            sb.AppendLine(r.ToString());
                        foreach (var v in problem.Details.Select(d => d.Voucher).Distinct())
                            sb.AppendLine(CSharpHelper.PresentVoucher(v));
                        sb.AppendLine();
                    }
                    foreach (var problem in tooFew)
                    {
                        sb.AppendLine("---Too Few Voucher");
                        foreach (var r in problem.Records)
                            sb.AppendLine(r.ToString());
                        foreach (var v in problem.Details.Select(d => d.Voucher).Distinct())
                            sb.AppendLine(CSharpHelper.PresentVoucher(v));
                        sb.AppendLine();
                    }
                    if (noRecord.Any())
                    {
                        sb.AppendLine("---No Record");
                        foreach (var v in noRecord.Select(d => d.Voucher).Distinct())
                            sb.AppendLine(CSharpHelper.PresentVoucher(v));
                    }
                    if (sb.Length == 0)
                        return new Suceed();
                    return new EditableText(sb.ToString());
                }
                if (!tooFew.Any())
                    return new Suceed();

                List<TransactionRecord> fail;
                foreach (var voucher in AutoGenerate(tooFew.SelectMany(p => p.Records), pars, out fail))
                    Accountant.Upsert(voucher);

                if (fail.Any())
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("---Can not generate");
                    foreach (var r in fail)
                        sb.AppendLine(r.ToString());
                    return new EditableText(sb.ToString());
                }
                return new Suceed();
            }
        }

        /// <summary>
        ///     自动生成的记账凭证类型
        /// </summary>
        private enum RegularType
        {
            /// <summary>
            ///     餐费
            /// </summary>
            Meals,

            /// <summary>
            ///     超市购物
            /// </summary>
            Shopping
        }

        /// <summary>
        ///     自动生成记账凭证
        /// </summary>
        /// <param name="records">交易记录</param>
        /// <param name="pars">生成记账所需参数</param>
        /// <param name="failed">无法生产记账凭证的交易记录</param>
        private IEnumerable<Voucher> AutoGenerate(IEnumerable<TransactionRecord> records, IEnumerable<string> pars,
                                                  out List<TransactionRecord> failed)
        {
            var dic = GetDic(pars);
            var res = new List<Voucher>();
            failed = new List<TransactionRecord>();
            foreach (var grp in records.GroupBy(r => r.Date))
            {
                var lst = new List<TransactionRecord>();
                foreach (var record in grp)
                {
                    var rec = record;
                    Func<int, VoucherDetail> item =
                        dir => new VoucherDetail
                                   {
                                       Title = 1012,
                                       SubTitle = 05,
                                       Fund = dir * rec.Fund,
                                       Remark = rec.Index.ToString(CultureInfo.InvariantCulture)
                                   };
                    switch (record.Type)
                    {
                        case "支付宝充值":
                            res.Add(
                                    new Voucher
                                        {
                                            Date = grp.Key.Date,
                                            Details = new List<VoucherDetail>
                                                          {
                                                              item(1),
                                                              new VoucherDetail
                                                                  {
                                                                      Title = 1221,
                                                                      Content = "学生卡待领",
                                                                      Fund = -record.Fund
                                                                  }
                                                          }
                                        });
                            break;
                        case "领取圈存":
                            res.Add(
                                    new Voucher
                                        {
                                            Date = grp.Key.Date,
                                            Details = new List<VoucherDetail>
                                                          {
                                                              item(1),
                                                              new VoucherDetail
                                                                  {
                                                                      Title = 1002,
                                                                      Content = "3593",
                                                                      Fund = -record.Fund
                                                                  }
                                                          }
                                        });
                            break;
                        case "自助缴费(学生公寓水费)":
                            res.Add(
                                    new Voucher
                                        {
                                            Date = grp.Key.Date,
                                            Details = new List<VoucherDetail>
                                                          {
                                                              item(-1),
                                                              new VoucherDetail
                                                                  {
                                                                      Title = 1123,
                                                                      Content = "学生卡小钱包",
                                                                      Fund = record.Fund
                                                                  }
                                                          }
                                        });
                            break;
                        default:
                            lst.Add(record);
                            break;
                    }
                }
                if (!lst.Any())
                    continue;

                if (!dic.ContainsKey(grp.Key.Date))
                    failed.AddRange(grp);

                var ins = dic[grp.Key.Date];
                var id = 0;
                foreach (var inst in ins)
                {
                    if (id == lst.Count)
                        throw new Exception();
                    Func<TransactionRecord, int, VoucherDetail> newDetail =
                        (tr, dir) => new VoucherDetail
                                         {
                                             Title = 1012,
                                             SubTitle = 05,
                                             Fund = dir * tr.Fund,
                                             Remark = tr.Index.ToString(CultureInfo.InvariantCulture)
                                         };
                    switch (inst.Item1)
                    {
                        case RegularType.Meals:
                            DateTime? t = null;
                            while (true)
                            {
                                if (t.HasValue)
                                {
                                    if (lst[id].Time.Subtract(t.Value).TotalHours > 1)
                                        break;
                                }
                                else
                                    t = lst[id].Time;
                                if (lst[id].Type == "消费" &&
                                    lst[id].Location == "饮食中心")
                                    res.Add(
                                            new Voucher
                                                {
                                                    Date = grp.Key.Date,
                                                    Details = new List<VoucherDetail>
                                                                  {
                                                                      newDetail(lst[id], -1),
                                                                      new VoucherDetail
                                                                          {
                                                                              Title = 6602,
                                                                              SubTitle = 03,
                                                                              Content = inst.Item2,
                                                                              Fund = lst[id].Fund
                                                                          }
                                                                  }
                                                });
                                else
                                    break;
                                id++;
                                if (id == lst.Count)
                                    break;
                            }
                            break;
                        case RegularType.Shopping:
                            if (lst[id].Type == "消费" &&
                                (lst[id].Location == "紫荆服务楼超市" ||
                                 lst[id].Location == "饮食广场超市"))
                            {
                                res.Add(
                                        new Voucher
                                            {
                                                Date = grp.Key.Date,
                                                Details = new List<VoucherDetail>
                                                              {
                                                                  newDetail(lst[id], -1),
                                                                  new VoucherDetail
                                                                      {
                                                                          Title = 6602,
                                                                          SubTitle = 03,
                                                                          Content = inst.Item2,
                                                                          Fund = lst[id].Fund
                                                                      }
                                                              }
                                            });
                                id++;
                            }
                            break;
                    }
                }
            }
            return res;
        }

        /// <summary>
        ///     解析自动补全指令
        /// </summary>
        /// <param name="pars">自动补全指令</param>
        /// <returns>解析结果</returns>
        private static Dictionary<DateTime, List<Tuple<RegularType, string>>> GetDic(IEnumerable<string> pars)
        {
            var dic = new Dictionary<DateTime, List<Tuple<RegularType, string>>>();
            foreach (var par in pars)
            {
                var sp = par.Split(' ', '/', ',');
                if (sp.Length == 0)
                    continue;
                var dt = DateTime.Now.Date;
                var lst = new List<Tuple<RegularType, string>>();
                foreach (var s in sp)
                {
                    var ss = s.Trim().ToLowerInvariant();
                    if (ss.StartsWith("."))
                    {
                        dt = DateTime.Now.Date.AddDays(1 - sp[0].Trim().Length);
                        continue;
                    }

                    var canteens = new Dictionary<string, string>
                                       {
                                           { "zj", "紫荆园" },
                                           { "tl", "桃李园" },
                                           { "gc", "观畴园" },
                                           { "yh", "清青永和" },
                                           { "bs", "清青比萨" },
                                           { "xx", "清青休闲" },
                                           { "sd", "清青时代" },
                                           { "kc", "清青快餐" },
                                           { "ys", "玉树园" },
                                           { "tt", "听涛园" },
                                           { "dx", "丁香园" },
                                           { "wx", "闻馨园" },
                                           { "zl", "芝兰园" }
                                       };

                    if (canteens.ContainsKey(ss))
                        lst.Add(new Tuple<RegularType, string>(RegularType.Meals, canteens[ss]));
                    else
                        switch (ss)
                        {
                            case "sp":
                                lst.Add(new Tuple<RegularType, string>(RegularType.Shopping, "食品"));
                                break;
                            case "sh":
                                lst.Add(new Tuple<RegularType, string>(RegularType.Shopping, "生活用品"));
                                break;
                            default:
                                throw new InvalidOperationException();
                        }
                }
                dic.Add(dt, lst);
            }
            return dic;
        }

        /// <summary>
        ///     包含记账凭证信息的细目
        /// </summary>
        private struct VDetail
        {
            /// <summary>
            ///     细目
            /// </summary>
            public VoucherDetail Detail;

            /// <summary>
            ///     细目所在的记账凭证
            /// </summary>
            public Voucher Voucher;
        }

        /// <summary>
        ///     对账问题
        /// </summary>
        private class Problem
        {
            /// <summary>
            ///     消费记录组
            /// </summary>
            public List<TransactionRecord> Records { get; private set; }

            /// <summary>
            ///     记账凭证组
            /// </summary>
            public List<VDetail> Details { get; private set; }

            public Problem(List<TransactionRecord> records, List<VDetail> details)
            {
                Records = records;
                Details = details;
            }
        }

        /// <summary>
        ///     对账并自动补足备注
        /// </summary>
        /// <param name="noRemark">无法自动补足备注的记账凭证</param>
        /// <param name="tooMuch">一组交易记录对应的记账凭证过多</param>
        /// <param name="tooFew">一组交易记录对应的记账凭证过少</param>
        /// <param name="noRecord">一个记账凭证没有可能对应的交易记录</param>
        private void Compare(out List<Problem> noRemark, out List<Problem> tooMuch, out List<Problem> tooFew,
                             out List<VDetail> noRecord)
        {
            var data = new List<TransactionRecord>(m_Data);

            var detailQuery =
                new DetailQueryAryBase(
                    OperatorType.Substract,
                    new IQueryCompunded<IDetailQueryAtom>[]
                        {
                            new DetailQueryAtomBase(new VoucherDetail { Title = 1012, SubTitle = 05 }),
                            new DetailQueryAryBase(
                                OperatorType.Union,
                                new IQueryCompunded<IDetailQueryAtom>[]
                                    {
                                        new DetailQueryAtomBase(new VoucherDetail { Remark = "" }),
                                        new DetailQueryAtomBase(new VoucherDetail { Remark = IgnoranceMark })
                                    }
                                )
                        });
            var voucherQuery = new VoucherQueryAtomBase { DetailFilter = detailQuery };
            var bin = new HashSet<int>();
            var binConflict = new HashSet<int>();
            foreach (var d in Accountant.SelectVouchers(voucherQuery)
                                        .SelectMany(
                                                    v => v.Details.Where(d => d.IsMatch(detailQuery))
                                                          .Select(d => new VDetail { Detail = d, Voucher = v })))
            {
                var id = Convert.ToInt32(d.Detail.Remark);
                if (!bin.Add(id))
                {
                    binConflict.Add(id);
                    continue;
                }
                var record = data.SingleOrDefault(r => r.Index == id);
                if (record == null)
                {
                    d.Detail.Remark = null;
                    Accountant.Upsert(d.Voucher);
                    continue;
                }
                // ReSharper disable once PossibleInvalidOperationException
                if (!Accountant.IsZero(Math.Abs(d.Detail.Fund.Value) - record.Fund))
                {
                    d.Detail.Remark = null;
                    Accountant.Upsert(d.Voucher);
                    continue;
                }
                data.Remove(record);
            }
            foreach (var id in binConflict)
            {
                var record = m_Data.SingleOrDefault(r => r.Index == id);
                if (record != null)
                    data.Add(record);

                var fltr = new VoucherDetail
                               {
                                   Title = 1012,
                                   SubTitle = 05,
                                   Remark = id.ToString(CultureInfo.InvariantCulture)
                               };
                foreach (var voucher in Accountant.SelectVouchers(new VoucherQueryAtomBase(filter: fltr)))
                {
                    foreach (var d in voucher.Details.Where(d => d.IsMatch(fltr)))
                        d.Remark = null;
                    Accountant.Upsert(voucher);
                }
            }

            var filter0 = new VoucherDetail { Title = 1012, SubTitle = 05, Remark = "" };
            var account = Accountant.SelectVouchers(new VoucherQueryAtomBase(filter: filter0))
                                    .SelectMany(
                                                v => v.Details.Where(d => d.IsMatch(filter0))
                                                      .Select(d => new VDetail { Detail = d, Voucher = v })).ToList();

            noRemark = new List<Problem>();
            tooMuch = new List<Problem>();
            tooFew = new List<Problem>();
            noRecord = new List<VDetail>(account);

            foreach (var dfgrp in data.GroupBy(r => new { r.Date, r.Fund }))
            {
                var grp = dfgrp;
                var acc =
                    account.Where(
                                  d =>
                                  d.Voucher.Date == grp.Key.Date &&
                                  // ReSharper disable once PossibleInvalidOperationException
                                  Accountant.IsZero(Math.Abs(d.Detail.Fund.Value) - grp.Key.Fund)).ToList();
                noRecord.RemoveAll(acc.Contains);
                switch (acc.Count.CompareTo(grp.Count()))
                {
                    case 0:
                        if (acc.Count == 1)
                        {
                            acc.Single().Detail.Remark = grp.Single().Index.ToString(CultureInfo.InvariantCulture);
                            Accountant.Upsert(acc.Single().Voucher);
                        }
                        else
                            noRemark.Add(new Problem(grp.ToList(), acc));
                        break;
                    case 1:
                        tooMuch.Add(new Problem(grp.ToList(), acc));
                        break;
                    case -1:
                        tooFew.Add(new Problem(grp.ToList(), acc));
                        break;
                }
            }
        }
    }
}
