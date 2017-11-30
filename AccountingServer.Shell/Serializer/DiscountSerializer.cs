﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccountingServer.BLL;
using AccountingServer.Entities;
using AccountingServer.Entities.Util;
using AccountingServer.Shell.Util;
using static AccountingServer.BLL.Parsing.Facade;
using static AccountingServer.BLL.Parsing.FacadeF;

namespace AccountingServer.Shell.Serializer
{
    public class DiscountSerializer : IEntitySerializer
    {
        private const string TheToken = "new Voucher {";

        /// <inheritdoc />
        public string PresentVoucher(Voucher voucher) => throw new NotImplementedException();

        /// <inheritdoc />
        public string PresentVoucherDetail(VoucherDetail detail) => throw new NotImplementedException();

        /// <inheritdoc />
        public Voucher ParseVoucher(string expr)
        {
            if (!expr.StartsWith(TheToken, StringComparison.Ordinal))
                throw new FormatException("格式错误");

            expr = expr.Substring(TheToken.Length);
            if (ParsingF.Token(ref expr, false, s => s == "!") == null)
                throw new NotImplementedException();

            var v = GetVoucher(ref expr);
            Parsing.TrimStartComment(ref expr);
            if (Parsing.Token(ref expr, false) != "}")
                throw new FormatException("格式错误");

            Parsing.Eof(expr);
            return v;
        }

        private sealed class Item : VoucherDetail
        {
            public double DiscountFund { get; set; }

            public bool UseActualFund { get; set; }
        }

        private sealed class DetailEqualityComparer : IEqualityComparer<VoucherDetail>
        {
            public bool Equals(VoucherDetail x, VoucherDetail y)
            {
                if (x == null &&
                    y == null)
                    return true;
                if (x == null ||
                    y == null)
                    return false;
                if (x.Currency != y.Currency)
                    return false;
                if (x.Title != y.Title)
                    return false;
                if (x.SubTitle != y.SubTitle)
                    return false;
                if (x.Content != y.Content)
                    return false;
                if (x.Fund.HasValue != y.Fund.HasValue)
                    return false;
                if (x.Fund.HasValue &&
                    y.Fund.HasValue)
                    if (!(x.Fund.Value - y.Fund.Value).IsZero())
                        return false;

                return x.Remark == y.Remark;
            }

            public int GetHashCode(VoucherDetail obj) => obj.Currency?.GetHashCode() | obj.Title?.GetHashCode() |
                obj.SubTitle?.GetHashCode() | obj.Content?.GetHashCode() | obj.Fund?.GetHashCode() |
                obj.Remark?.GetHashCode() ?? 0;
        }

        /// <summary>
        ///     解析记账凭证表达式
        /// </summary>
        /// <param name="expr">表达式</param>
        /// <returns>记账凭证</returns>
        private Voucher GetVoucher(ref string expr)
        {
            Parsing.TrimStartComment(ref expr);
            DateTime? date = DateTime.Today.CastUtc();
            try
            {
                date = ParsingF.UniqueTime(ref expr);
            }
            catch (Exception)
            {
                // ignore
            }

            var currency = Parsing.Token(ref expr, false, s => s.StartsWith("@", StringComparison.Ordinal))
                ?.Substring(1)
                .ToUpperInvariant();

            var lst = new List<Item>();
            List<Item> ds;
            while ((ds = ParseItem(currency, ref expr))?.Any() == true)
                lst.AddRange(ds);

            var d = 0D;
            var t = 0D;
            var reg = new Regex(@"(?<dt>[dt])(?<num>[0-9]+(?:\.[0-9]{1,2})?)");
            while (true)
            {
                var res = Parsing.Token(ref expr, false, reg.IsMatch);
                if (res == null)
                    break;

                var m = reg.Match(res);
                var num = Convert.ToDouble(m.Groups["num"].Value);
                if (m.Groups["dt"].Value == "d")
                    d += num;
                else // if (m.Groups["dt"].Value == "t")
                    t += num;
            }

            // ReSharper disable once PossibleInvalidOperationException
            var total = lst.Sum(it => it.Fund.Value);
            foreach (var item in lst)
            {
                item.Fund += t / total * item.Fund;
                // ReSharper disable once PossibleInvalidOperationException
                item.DiscountFund += d / total * item.Fund.Value;
            }

            foreach (var item in lst)
            {
                if (!item.UseActualFund)
                    continue;

                item.Fund -= item.DiscountFund;
                item.DiscountFund = 0D;
            }

            var totalD = lst.Sum(it => it.DiscountFund);

            var resLst = new List<VoucherDetail>();
            foreach (var grp in lst.GroupBy(
                it => new VoucherDetail
                    {
                        Currency = it.Currency,
                        Title = it.Title,
                        SubTitle = it.SubTitle,
                        Content = it.Content,
                        Remark = it.Remark
                    }, new DetailEqualityComparer()))
            {
                // ReSharper disable once PossibleInvalidOperationException
                grp.Key.Fund = grp.Sum(it => it.Fund.Value);
                resLst.Add(grp.Key);
            }

            if (!totalD.IsZero())
                resLst.Add(
                    new VoucherDetail
                        {
                            Currency = currency,
                            Title = 6603,
                            Fund = -totalD
                        });

            VoucherDetail vd;
            var exprS = new AbbrSerializer();
            while ((vd = exprS.ParseVoucherDetail(ref expr)) != null)
                resLst.Add(vd);

            return new Voucher
                {
                    Type = VoucherType.Ordinary,
                    Date = date,
                    Details = resLst
                };
        }

        /// <inheritdoc />
        public virtual VoucherDetail ParseVoucherDetail(string expr) => throw new NotImplementedException();

        private VoucherDetail ParseVoucherDetail(string currency, ref string expr)
        {
            var lst = new List<string>();

            Parsing.TrimStartComment(ref expr);
            var title = Parsing.Title(ref expr);
            if (title == null)
                if (!AlternativeTitle(ref expr, lst, ref title))
                    return null;

            while (true)
            {
                Parsing.TrimStartComment(ref expr);
                if (Parsing.Optional(ref expr, "+"))
                    break;

                if (Parsing.Optional(ref expr, ":"))
                {
                    expr = $": {expr}";
                    break;
                }

                if (lst.Count > 2)
                    throw new ArgumentException("语法错误", nameof(expr));

                Parsing.TrimStartComment(ref expr);
                lst.Add(Parsing.Token(ref expr));
            }

            var content = lst.Count >= 1 ? lst[0] : null;
            var remark = lst.Count >= 2 ? lst[1] : null;

            if (content == "G()")
                content = Guid.NewGuid().ToString().ToUpperInvariant();

            if (remark == "G()")
                remark = Guid.NewGuid().ToString().ToUpperInvariant();


            return new VoucherDetail
                {
                    Currency = currency,
                    Title = title.Title,
                    SubTitle = title.SubTitle,
                    Content = string.IsNullOrEmpty(content) ? null : content,
                    Remark = string.IsNullOrEmpty(remark) ? null : remark
                };
        }

        private List<Item> ParseItem(string currency, ref string expr)
        {
            var lst = new List<(VoucherDetail Detail, bool Actual)>();

            while (true)
            {
                var actual = Parsing.Optional(ref expr, "!");
                var vd = ParseVoucherDetail(currency, ref expr);
                if (vd == null)
                    break;

                lst.Add((vd, actual));
            }

            if (ParsingF.Token(ref expr, false, s => s == ":") == null)
                return null;

            var resLst = new List<Item>();

            var reg = new Regex(
                @"(?<num>[0-9]+(?:\.[0-9]+)?)(?:(?<plus>\+[0-9]+(?:\.[0-9]+)?)|(?<minus>-[0-9]+(?:\.[0-9]+)?))?");
            while (true)
            {
                var res = Parsing.Token(ref expr, false, reg.IsMatch);
                if (res == null)
                    break;

                var m = reg.Match(res);
                var fund0 = Convert.ToDouble(m.Groups["num"].Value);
                var fundd = 0D;
                if (m.Groups["plus"].Success)
                {
                    fundd = Convert.ToDouble(m.Groups["plus"].Value);
                    fund0 += fundd;
                }
                else if (m.Groups["minus"].Success)
                    fundd = -Convert.ToDouble(m.Groups["minus"].Value);

                resLst.AddRange(
                    lst.Select(
                        d => new Item
                            {
                                Currency = d.Detail.Currency,
                                Title = d.Detail.Title,
                                SubTitle = d.Detail.SubTitle,
                                Content = d.Detail.Content,
                                Fund = fund0 / lst.Count,
                                DiscountFund = fundd / lst.Count,
                                Remark = d.Detail.Remark,
                                UseActualFund = d.Actual
                            }));
            }

            ParsingF.Optional(ref expr, ";");

            return resLst;
        }

        protected virtual bool AlternativeTitle(ref string expr, ICollection<string> lst, ref ITitle title) =>
            AbbrSerializer.GetAlternativeTitle(ref expr, lst, ref title);

        public string PresentAsset(Asset asset) => throw new NotImplementedException();
        public Asset ParseAsset(string str) => throw new NotImplementedException();
        public string PresentAmort(Amortization amort) => throw new NotImplementedException();
        public Amortization ParseAmort(string str) => throw new NotImplementedException();
    }
}