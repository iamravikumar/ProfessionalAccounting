﻿using System;
using AccountingServer.Entities;

namespace AccountingServer.Shell.Parsing
{
    public partial class ShellParser
    {
        public partial class DetailQueryContext : IDetailQueryAtom
        {
            /// <inheritdoc />
            public VoucherDetail Filter
            {
                get
                {
                    var filter = new VoucherDetail
                                     {
                                         Content = SingleQuotedString().Dequotation(),
                                         Remark = DoubleQuotedString().Dequotation()
                                     };
                    if (DetailTitle() != null)
                    {
                        var t = int.Parse(DetailTitle().GetText().TrimStart('T'));
                        filter.Title = t;
                    }
                    else if (DetailTitleSubTitle() != null)
                    {
                        var t = int.Parse(DetailTitleSubTitle().GetText().TrimStart('T'));
                        filter.Title = t / 100;
                        filter.SubTitle = t % 100;
                    }
                    return filter;
                }
            }

            /// <inheritdoc />
            public int Dir
            {
                get
                {
                    if (Direction == null)
                        return 0;
                    if (Direction.Text == ">")
                        return 1;
                    if (Direction.Text == "<")
                        return -1;
                    throw new MemberAccessException("表达式错误");
                }
            }
        }

        public partial class DetailsContext : IQueryAry<IDetailQueryAtom>
        {
            /// <inheritdoc />
            public OperatorType Operator
            {
                get
                {
                    if (Op == null)
                        return OperatorType.None;
                    if (details().Count == 1)
                    {
                        if (Op.Text == "+")
                            return OperatorType.Identity;
                        if (Op.Text == "-")
                            return OperatorType.Complement;
                        throw new MemberAccessException("表达式错误");
                    }
                    if (Op.Text == "+")
                        return OperatorType.Union;
                    if (Op.Text == "-")
                        return OperatorType.Substract;
                    if (Op.Text == "*")
                        return OperatorType.Intersect;
                    throw new MemberAccessException("表达式错误");
                }
            }

            /// <inheritdoc />
            public IQueryCompunded<IDetailQueryAtom> Filter1
            {
                get
                {
                    if (detailQuery() != null)
                        return detailQuery();
                    return details(0);
                }
            }

            /// <inheritdoc />
            public IQueryCompunded<IDetailQueryAtom> Filter2 => details(1);
        }
    }
}
