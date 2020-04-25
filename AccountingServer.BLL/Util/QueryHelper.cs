using AccountingServer.Entities;

namespace AccountingServer.BLL.Util
{
    public abstract class BinaryQueries<TAtom> : IQueryAry<TAtom> where TAtom : class
    {
        protected BinaryQueries(IQueryCompounded<TAtom> filter1,
            IQueryCompounded<TAtom> filter2)
        {
            Filter1 = filter1;
            Filter2 = filter2;
        }

        public abstract OperatorType Operator { get; }

        public IQueryCompounded<TAtom> Filter1 { get; }

        public IQueryCompounded<TAtom> Filter2 { get; }

        public bool IsDangerous() => (Filter1?.IsDangerous() ?? false) || (Filter2?.IsDangerous() ?? false);

        public T Accept<T>(IQueryVisitor<TAtom, T> visitor) => visitor.Visit(this);
    }

    public sealed class IntersectQueries<TAtom> : BinaryQueries<TAtom> where TAtom : class
    {
        public IntersectQueries(IQueryCompounded<TAtom> filter1,
            IQueryCompounded<TAtom> filter2) : base(filter1, filter2) { }

        public override OperatorType Operator => OperatorType.Intersect;
    }

    public sealed class UnionQueries<TAtom> : BinaryQueries<TAtom> where TAtom : class
    {
        public UnionQueries(IQueryCompounded<TAtom> filter1,
            IQueryCompounded<TAtom> filter2) : base(filter1, filter2) { }

        public override OperatorType Operator => OperatorType.Union;
    }
}
